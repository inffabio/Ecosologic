using System.Reflection;
using Ecosologic.Domain.Crm;
using Ecosologic.Domain.Solar;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Ecosologic.Api.Tests;

public class SolarCommercialModelsTests
{
    private static readonly Guid LeadId = Guid.NewGuid();
    private static readonly Guid SizingId = Guid.NewGuid();
    private static readonly Guid QuoteId = Guid.NewGuid();

    private static EcosologicDbContext NewNpgsqlModelContext() =>
        new(new DbContextOptionsBuilder<EcosologicDbContext>()
            .UseNpgsql("Host=localhost;Database=ecosologic;Username=postgres;Password=postgres")
            .Options);

    private static EcosologicDbContext NewSqliteContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        var db = new EcosologicDbContext(
            new DbContextOptionsBuilder<EcosologicDbContext>()
                .UseSqlite(connection)
                .Options);
        db.Database.EnsureCreated();
        return db;
    }

    // --- Domain helpers ---

    private static SolarSizing NewDraftSizing() =>
        SolarSizing.CreateDraft(LeadId, "Light RJ", "B", "Convencional", "1.0.0", "{\"kWh\":600}");

    private static SolarSizing NewCalculatedSizing()
    {
        var sizing = NewDraftSizing();
        sizing.Calculate("{}");
        return sizing;
    }

    private static SolarSizing NewApprovedSizing()
    {
        var sizing = NewCalculatedSizing();
        sizing.Approve();
        return sizing;
    }

    private static SolarQuote NewQuote()
    {
        var sizing = NewApprovedSizing();
        return SolarQuote.CreateDraft(sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");
    }

    private static SolarQuote NewApprovedQuote()
    {
        var sizing = NewApprovedSizing();
        var quote = SolarQuote.CreateDraft(sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");
        quote.Approve(sizing);
        return quote;
    }

    private static Proposal NewProposal() =>
        Proposal.CreateDraft(NewApprovedQuote(), "1.0.0", "{}");

    private static Proposal NewProposalWithQuote(out SolarQuote quote)
    {
        quote = NewApprovedQuote();
        return Proposal.CreateDraft(quote, "1.0.0", "{}");
    }

    private static LeadRecord NewLeadRecord(Guid id) =>
        new()
        {
            Id = id,
            Name = "Lead",
            Phone = "123",
            Email = "",
            Message = "",
            Stage = LeadStage.New,
            CreatedAt = DateTimeOffset.UtcNow
        };

    // --- SolarSizing domain transitions ---

    [Fact]
    public void SolarSizing_CreateDraft_starts_draft_and_snapshots_inputs()
    {
        var sizing = NewDraftSizing();

        Assert.Equal(SolarSizingStatus.Draft, sizing.Status);
        Assert.Equal(LeadId, sizing.LeadId);
        Assert.Equal("Light RJ", sizing.Concessionaria);
        Assert.Equal("B", sizing.Grupo);
        Assert.Equal("Convencional", sizing.Modalidade);
        Assert.Equal("1.0.0", sizing.EngineVersion);
        Assert.Equal("{\"kWh\":600}", sizing.InputsJson);
        Assert.Null(sizing.ResultsJson);
        Assert.NotEqual(Guid.Empty, sizing.Id);
    }

    [Fact]
    public void SolarSizing_Calculate_sets_results_and_status()
    {
        var sizing = NewDraftSizing();

        sizing.Calculate("{\"kwp\":4.0}");

        Assert.Equal(SolarSizingStatus.Calculated, sizing.Status);
        Assert.Equal("{\"kwp\":4.0}", sizing.ResultsJson);
    }

    [Fact]
    public void SolarSizing_Approve_requires_calculated()
    {
        var sizing = NewDraftSizing();
        sizing.Calculate("{}");

        sizing.Approve();

        Assert.Equal(SolarSizingStatus.Approved, sizing.Status);
    }

    [Fact]
    public void SolarSizing_cannot_approve_draft()
    {
        var sizing = NewDraftSizing();

        Assert.False(sizing.CanTransitionTo(SolarSizingStatus.Approved));
        Assert.Throws<InvalidOperationException>(() => sizing.Approve());
    }

    [Fact]
    public void SolarSizing_cancel_from_draft_is_terminal()
    {
        var sizing = NewDraftSizing();

        sizing.Cancel();

        Assert.Equal(SolarSizingStatus.Cancelled, sizing.Status);
        Assert.False(sizing.CanTransitionTo(SolarSizingStatus.Calculated));
        Assert.Throws<InvalidOperationException>(() => sizing.Calculate("{}"));
    }

    // --- SolarQuote domain transitions ---

    [Fact]
    public void SolarQuote_CreateDraft_starts_draft_and_snapshots_conditions()
    {
        var quote = NewQuote();

        Assert.Equal(SolarQuoteStatus.Draft, quote.Status);
        Assert.NotEqual(Guid.Empty, quote.SizingId);
        Assert.Equal(1000m, quote.TotalCost);
        Assert.Equal(20m, quote.MarginPercent);
        Assert.Equal(10m, quote.TaxPercent);
        Assert.Equal(1320m, quote.TotalPrice);
        Assert.Equal("[]", quote.ItemsJson);
        Assert.Equal("{}", quote.ConditionsJson);
        Assert.Equal("{}", quote.SnapshotJson);
    }

    [Fact]
    public void SolarQuote_flows_from_draft_to_accepted()
    {
        var sizing = NewApprovedSizing();
        var quote = SolarQuote.CreateDraft(sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");

        quote.Approve(sizing);
        Assert.Equal(SolarQuoteStatus.Approved, quote.Status);

        quote.Send();
        Assert.Equal(SolarQuoteStatus.Sent, quote.Status);

        quote.Accept();
        Assert.Equal(SolarQuoteStatus.Accepted, quote.Status);
    }

    [Fact]
    public void SolarQuote_cannot_send_before_approve()
    {
        var quote = NewQuote();

        Assert.False(quote.CanTransitionTo(SolarQuoteStatus.Sent));
        Assert.Throws<InvalidOperationException>(() => quote.Send());
    }

    [Fact]
    public void SolarQuote_rejects_and_expires_from_sent()
    {
        var sizing = NewApprovedSizing();

        var rejected = SolarQuote.CreateDraft(sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");
        rejected.Approve(sizing);
        rejected.Send();
        rejected.Reject();
        Assert.Equal(SolarQuoteStatus.Rejected, rejected.Status);

        var expired = SolarQuote.CreateDraft(sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");
        expired.Approve(sizing);
        expired.Send();
        expired.Expire();
        Assert.Equal(SolarQuoteStatus.Expired, expired.Status);
    }

    [Fact]
    public void SolarQuote_can_expire_without_being_sent()
    {
        var quote = NewQuote();

        quote.Expire();

        Assert.Equal(SolarQuoteStatus.Expired, quote.Status);
    }

    [Fact]
    public void SolarQuote_cannot_be_created_from_draft_sizing()
    {
        var draft = NewDraftSizing();

        Assert.Throws<InvalidOperationException>(() =>
            SolarQuote.CreateDraft(draft, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));
    }

    [Fact]
    public void SolarQuote_can_be_created_from_calculated_sizing()
    {
        var calculated = NewCalculatedSizing();

        var quote = SolarQuote.CreateDraft(calculated, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");

        Assert.Equal(SolarQuoteStatus.Draft, quote.Status);
        Assert.Equal(calculated.Id, quote.SizingId);
    }

    [Fact]
    public void SolarQuote_can_be_created_from_approved_sizing()
    {
        var approved = NewApprovedSizing();

        var quote = SolarQuote.CreateDraft(approved, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");

        Assert.Equal(approved.Id, quote.SizingId);
    }

    [Fact]
    public void SolarQuote_cannot_be_approved_from_calculated_sizing()
    {
        var calculated = NewCalculatedSizing();
        var quote = SolarQuote.CreateDraft(calculated, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");

        Assert.Throws<InvalidOperationException>(() => quote.Approve(calculated));
    }

    [Fact]
    public void SolarQuote_can_be_approved_from_approved_sizing()
    {
        var approved = NewApprovedSizing();
        var quote = SolarQuote.CreateDraft(approved, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");

        quote.Approve(approved);

        Assert.Equal(SolarQuoteStatus.Approved, quote.Status);
    }

    [Fact]
    public void SolarQuote_Approve_rejects_mismatched_sizing()
    {
        var quote = SolarQuote.CreateDraft(NewApprovedSizing(), "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");
        var otherSizing = NewApprovedSizing();

        Assert.Throws<InvalidOperationException>(() => quote.Approve(otherSizing));
    }

    // --- SolarQuote money/percent validation ---

    [Fact]
    public void SolarQuote_rejects_negative_total_cost()
    {
        var sizing = NewApprovedSizing();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarQuote.CreateDraft(sizing, "[]", -1m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));
    }

    [Fact]
    public void SolarQuote_rejects_negative_total_price()
    {
        var sizing = NewApprovedSizing();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarQuote.CreateDraft(sizing, "[]", 1000m, 20m, 10m, -1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));
    }

    [Fact]
    public void SolarQuote_rejects_negative_margin_percent()
    {
        var sizing = NewApprovedSizing();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarQuote.CreateDraft(sizing, "[]", 1000m, -20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));
    }

    [Fact]
    public void SolarQuote_rejects_negative_tax_percent()
    {
        var sizing = NewApprovedSizing();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarQuote.CreateDraft(sizing, "[]", 1000m, 20m, -10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));
    }

    [Fact]
    public void SolarQuote_rejects_total_cost_with_more_than_two_decimals()
    {
        var sizing = NewApprovedSizing();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarQuote.CreateDraft(sizing, "[]", 1000.005m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));
    }

    [Fact]
    public void SolarQuote_rejects_total_price_with_more_than_two_decimals()
    {
        var sizing = NewApprovedSizing();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarQuote.CreateDraft(sizing, "[]", 1000m, 20m, 10m, 1320.005m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));
    }

    [Fact]
    public void SolarQuote_rejects_margin_percent_with_more_than_four_decimals()
    {
        var sizing = NewApprovedSizing();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarQuote.CreateDraft(sizing, "[]", 1000m, 20.12345m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));
    }

    [Fact]
    public void SolarQuote_rejects_tax_percent_with_more_than_four_decimals()
    {
        var sizing = NewApprovedSizing();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarQuote.CreateDraft(sizing, "[]", 1000m, 20m, 10.12345m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));
    }

    // --- Domain JSON snapshot validation ---

    [Fact]
    public void SolarSizing_CreateDraft_rejects_empty_grupo() =>
        Assert.Throws<ArgumentException>(() =>
            SolarSizing.CreateDraft(LeadId, "Light RJ", "", "Convencional", "1.0.0", "{\"kWh\":600}"));

    [Fact]
    public void SolarSizing_CreateDraft_rejects_empty_modalidade() =>
        Assert.Throws<ArgumentException>(() =>
            SolarSizing.CreateDraft(LeadId, "Light RJ", "B", "   ", "1.0.0", "{\"kWh\":600}"));

    [Fact]
    public void SolarSizing_CreateDraft_rejects_invalid_inputs_json() =>
        Assert.Throws<ArgumentException>(() =>
            SolarSizing.CreateDraft(LeadId, "Light RJ", "B", "Convencional", "1.0.0", "not-json"));

    [Fact]
    public void SolarSizing_Calculate_rejects_invalid_results_json() =>
        Assert.Throws<ArgumentException>(() => NewDraftSizing().Calculate("not-json"));

    [Fact]
    public void SolarQuote_CreateDraft_rejects_invalid_items_json() =>
        Assert.Throws<ArgumentException>(() =>
            SolarQuote.CreateDraft(NewApprovedSizing(), "not-json", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));

    [Fact]
    public void SolarQuote_CreateDraft_rejects_invalid_conditions_json() =>
        Assert.Throws<ArgumentException>(() =>
            SolarQuote.CreateDraft(NewApprovedSizing(), "[]", 1000m, 20m, 10m, 1320m, "not-json", DateTimeOffset.UtcNow.AddDays(30), "{}"));

    [Fact]
    public void SolarQuote_CreateDraft_rejects_empty_conditions_json() =>
        Assert.Throws<ArgumentException>(() =>
            SolarQuote.CreateDraft(NewApprovedSizing(), "[]", 1000m, 20m, 10m, 1320m, "   ", DateTimeOffset.UtcNow.AddDays(30), "{}"));

    [Fact]
    public void SolarQuote_CreateDraft_rejects_invalid_snapshot_json() =>
        Assert.Throws<ArgumentException>(() =>
            SolarQuote.CreateDraft(NewApprovedSizing(), "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "not-json"));

    [Fact]
    public void Proposal_CreateDraft_rejects_invalid_payload_json() =>
        Assert.Throws<ArgumentException>(() =>
            Proposal.CreateDraft(NewApprovedQuote(), "1.0.0", "not-json"));

    // --- Proposal domain transitions ---

    [Fact]
    public void Proposal_CreateDraft_starts_draft()
    {
        var proposal = NewProposal();

        Assert.Equal(ProposalStatus.Draft, proposal.Status);
        Assert.NotEqual(Guid.Empty, proposal.QuoteId);
        Assert.Equal("1.0.0", proposal.TemplateVersion);
        Assert.Equal("{}", proposal.PayloadJson);
        Assert.Null(proposal.FileUrl);
        Assert.Null(proposal.FileHash);
    }

    [Fact]
    public void Proposal_generate_sets_file_and_status()
    {
        var proposal = NewProposalWithQuote(out var quote);

        proposal.Generate(quote, "https://cdn.example/proposal.pdf", "abc123");

        Assert.Equal(ProposalStatus.Generated, proposal.Status);
        Assert.Equal("https://cdn.example/proposal.pdf", proposal.FileUrl);
        Assert.Equal("abc123", proposal.FileHash);
    }

    [Fact]
    public void Proposal_cannot_send_before_generate()
    {
        var proposal = NewProposal();

        Assert.False(proposal.CanTransitionTo(ProposalStatus.Sent));
        Assert.Throws<InvalidOperationException>(() => proposal.Send());
    }

    [Fact]
    public void Proposal_accepts_and_expires()
    {
        var accepted = NewProposalWithQuote(out var acceptedQuote);
        accepted.Generate(acceptedQuote, "https://cdn.example/a.pdf", null);
        accepted.Send();
        accepted.Accept();
        Assert.Equal(ProposalStatus.Accepted, accepted.Status);

        var expired = NewProposalWithQuote(out var expiredQuote);
        expired.Generate(expiredQuote, "https://cdn.example/b.pdf", null);
        expired.Expire();
        Assert.Equal(ProposalStatus.Expired, expired.Status);
    }

    [Fact]
    public void Proposal_generate_requires_approved_quote()
    {
        var sizing = NewApprovedSizing();
        var draftQuote = SolarQuote.CreateDraft(sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");
        var proposal = Proposal.CreateDraft(NewApprovedQuote(), "1.0.0", "{}");

        Assert.Throws<InvalidOperationException>(() => proposal.Generate(draftQuote, "https://cdn.example/p.pdf", null));
    }

    [Fact]
    public void Proposal_cannot_be_created_from_non_approved_quote()
    {
        var sizing = NewApprovedSizing();
        var draftQuote = SolarQuote.CreateDraft(sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");

        Assert.Throws<InvalidOperationException>(() => Proposal.CreateDraft(draftQuote, "1.0.0", "{}"));
    }

    [Fact]
    public void Proposal_can_be_created_from_approved_quote()
    {
        var quote = NewApprovedQuote();

        var proposal = Proposal.CreateDraft(quote, "1.0.0", "{}");

        Assert.Equal(quote.Id, proposal.QuoteId);
        Assert.Equal(ProposalStatus.Draft, proposal.Status);
    }

    [Fact]
    public void Proposal_Generate_rejects_mismatched_quote()
    {
        var proposal = NewProposalWithQuote(out _);
        var otherQuote = NewApprovedQuote();

        Assert.Throws<InvalidOperationException>(() => proposal.Generate(otherQuote, "https://cdn.example/p.pdf", null));
    }

    // --- Persistence record snapshot immutability ---

    [Fact]
    public void Solar_records_protect_snapshot_fields_from_post_creation_edits()
    {
        AssertNonPublicSetter<SolarSizingRecord>(
            nameof(SolarSizingRecord.LeadId),
            nameof(SolarSizingRecord.Concessionaria),
            nameof(SolarSizingRecord.Grupo),
            nameof(SolarSizingRecord.Modalidade),
            nameof(SolarSizingRecord.EngineVersion),
            nameof(SolarSizingRecord.InputsJson),
            nameof(SolarSizingRecord.ResultsJson),
            nameof(SolarSizingRecord.Status),
            nameof(SolarSizingRecord.UpdatedAt));

        AssertNonPublicSetter<SolarQuoteRecord>(
            nameof(SolarQuoteRecord.SizingId),
            nameof(SolarQuoteRecord.ItemsJson),
            nameof(SolarQuoteRecord.TotalCost),
            nameof(SolarQuoteRecord.MarginPercent),
            nameof(SolarQuoteRecord.TaxPercent),
            nameof(SolarQuoteRecord.TotalPrice),
            nameof(SolarQuoteRecord.ConditionsJson),
            nameof(SolarQuoteRecord.ValidUntil),
            nameof(SolarQuoteRecord.SnapshotJson),
            nameof(SolarQuoteRecord.Status),
            nameof(SolarQuoteRecord.UpdatedAt));

        AssertNonPublicSetter<ProposalRecord>(
            nameof(ProposalRecord.QuoteId),
            nameof(ProposalRecord.TemplateVersion),
            nameof(ProposalRecord.PayloadJson),
            nameof(ProposalRecord.FileUrl),
            nameof(ProposalRecord.FileHash),
            nameof(ProposalRecord.Status),
            nameof(ProposalRecord.UpdatedAt));
    }

    private static void AssertNonPublicSetter<T>(params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            var property = typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);
            Assert.True(
                property!.SetMethod is null or { IsPublic: false },
                $"{typeof(T).Name}.{name} expõe um setter público.");
        }
    }

    // --- EF model snapshot ---

    [Fact]
    public void Model_maps_solar_entities_with_expected_tables_and_string_enums()
    {
        using var db = NewNpgsqlModelContext();

        var sizing = db.Model.FindEntityType(typeof(SolarSizingRecord))!;
        Assert.Equal("solar_sizings", sizing.GetTableName());
        Assert.Equal("character varying(32)", sizing.FindProperty(nameof(SolarSizingRecord.Status))!.GetColumnType());
        Assert.Equal("text", sizing.FindProperty(nameof(SolarSizingRecord.InputsJson))!.GetColumnType());
        Assert.Equal("text", sizing.FindProperty(nameof(SolarSizingRecord.ResultsJson))!.GetColumnType());

        var quote = db.Model.FindEntityType(typeof(SolarQuoteRecord))!;
        Assert.Equal("solar_quotes", quote.GetTableName());
        Assert.Equal("character varying(32)", quote.FindProperty(nameof(SolarQuoteRecord.Status))!.GetColumnType());
        Assert.Equal("text", quote.FindProperty(nameof(SolarQuoteRecord.ItemsJson))!.GetColumnType());
        Assert.Equal("text", quote.FindProperty(nameof(SolarQuoteRecord.SnapshotJson))!.GetColumnType());

        var proposal = db.Model.FindEntityType(typeof(ProposalRecord))!;
        Assert.Equal("proposals", proposal.GetTableName());
        Assert.Equal("character varying(32)", proposal.FindProperty(nameof(ProposalRecord.Status))!.GetColumnType());
        Assert.Equal("text", proposal.FindProperty(nameof(ProposalRecord.PayloadJson))!.GetColumnType());
    }

    [Fact]
    public void Model_cascades_only_on_owned_chain()
    {
        using var db = NewNpgsqlModelContext();

        var sizing = db.Model.FindEntityType(typeof(SolarSizingRecord))!;
        var quotesNavigation = sizing.FindNavigation(nameof(SolarSizingRecord.Quotes))!;
        Assert.Equal(DeleteBehavior.Cascade, quotesNavigation.ForeignKey.DeleteBehavior);

        var leadFk = sizing.GetForeignKeys()
            .Single(fk => fk.Properties.Any(p => p.Name == nameof(SolarSizingRecord.LeadId)));
        Assert.Equal(DeleteBehavior.Restrict, leadFk.DeleteBehavior);

        var quote = db.Model.FindEntityType(typeof(SolarQuoteRecord))!;
        var proposalsNavigation = quote.FindNavigation(nameof(SolarQuoteRecord.Proposals))!;
        Assert.Equal(DeleteBehavior.Cascade, proposalsNavigation.ForeignKey.DeleteBehavior);
    }

    [Fact]
    public void Model_has_expected_indexes()
    {
        using var db = NewNpgsqlModelContext();

        var sizing = db.Model.FindEntityType(typeof(SolarSizingRecord))!;
        Assert.Contains(sizing.GetIndexes(), index => index.Properties.Any(p => p.Name == nameof(SolarSizingRecord.LeadId)));
        Assert.Contains(sizing.GetIndexes(), index => index.Properties.Any(p => p.Name == nameof(SolarSizingRecord.CreatedAt)));

        var quote = db.Model.FindEntityType(typeof(SolarQuoteRecord))!;
        Assert.Contains(quote.GetIndexes(), index => index.Properties.Any(p => p.Name == nameof(SolarQuoteRecord.SizingId)));

        var proposal = db.Model.FindEntityType(typeof(ProposalRecord))!;
        Assert.Contains(proposal.GetIndexes(), index => index.Properties.Any(p => p.Name == nameof(ProposalRecord.QuoteId)));
    }

    [Fact]
    public void Model_money_uses_precision_18_scale_2()
    {
        using var db = NewNpgsqlModelContext();

        var quote = db.Model.FindEntityType(typeof(SolarQuoteRecord))!;
        var totalCost = quote.FindProperty(nameof(SolarQuoteRecord.TotalCost))!;
        var totalPrice = quote.FindProperty(nameof(SolarQuoteRecord.TotalPrice))!;

        Assert.Equal(18, totalCost.GetPrecision());
        Assert.Equal(2, totalCost.GetScale());
        Assert.Equal(18, totalPrice.GetPrecision());
        Assert.Equal(2, totalPrice.GetScale());
    }

    [Fact]
    public void Model_percent_uses_precision_8_scale_4()
    {
        using var db = NewNpgsqlModelContext();

        var quote = db.Model.FindEntityType(typeof(SolarQuoteRecord))!;
        var margin = quote.FindProperty(nameof(SolarQuoteRecord.MarginPercent))!;
        var tax = quote.FindProperty(nameof(SolarQuoteRecord.TaxPercent))!;

        Assert.Equal(8, margin.GetPrecision());
        Assert.Equal(4, margin.GetScale());
        Assert.Equal(8, tax.GetPrecision());
        Assert.Equal(4, tax.GetScale());
    }

    [Fact]
    public void Model_defines_check_constraints_for_status_money_and_percent()
    {
        using var db = NewNpgsqlModelContext();

        var model = db.GetService<IDesignTimeModel>().Model;
        var sizing = model.FindEntityType(typeof(SolarSizingRecord))!;
        var quote = model.FindEntityType(typeof(SolarQuoteRecord))!;
        var proposal = model.FindEntityType(typeof(ProposalRecord))!;

        Assert.Contains(sizing.GetCheckConstraints(), c => c.Name == "CK_solar_sizings_status");
        Assert.Contains(sizing.GetCheckConstraints(), c => c.Name == "CK_solar_sizings_results_for_calculated_approved");
        Assert.Contains(sizing.GetCheckConstraints(), c => c.Name == "CK_solar_sizings_grupo_nonempty");
        Assert.Contains(sizing.GetCheckConstraints(), c => c.Name == "CK_solar_sizings_modalidade_nonempty");
        Assert.Contains(quote.GetCheckConstraints(), c => c.Name == "CK_solar_quotes_status");
        Assert.Contains(quote.GetCheckConstraints(), c => c.Name == "CK_solar_quotes_approved_requires_snapshot");
        Assert.Contains(quote.GetCheckConstraints(), c => c.Name == "CK_solar_quotes_total_cost_nonnegative");
        Assert.Contains(quote.GetCheckConstraints(), c => c.Name == "CK_solar_quotes_total_price_nonnegative");
        Assert.Contains(quote.GetCheckConstraints(), c => c.Name == "CK_solar_quotes_margin_percent_nonnegative");
        Assert.Contains(quote.GetCheckConstraints(), c => c.Name == "CK_solar_quotes_tax_percent_nonnegative");
        Assert.Contains(proposal.GetCheckConstraints(), c => c.Name == "CK_proposals_status");
        Assert.Contains(proposal.GetCheckConstraints(), c => c.Name == "CK_proposals_generated_requires_file");
    }

    // --- Persistence round-trip (relational SQLite) ---

    [Fact]
    public async Task Solar_models_roundtrip_with_relations_and_snapshots()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var now = DateTimeOffset.UtcNow;
        var proposalId = Guid.NewGuid();

        var sizing = SolarSizingRecord.Create(SizingId, LeadId, "Light RJ", "B", "Convencional", "1.0.0", "{\"kWh\":600}");
        sizing.Calculate("{\"kwp\":4.0}");
        sizing.Approve();
        var quote = SolarQuoteRecord.Create(QuoteId, sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", now.AddDays(30), "{}");
        quote.Approve(sizing);
        var proposal = ProposalRecord.Create(proposalId, quote, "1.0.0", "{}");
        proposal.Generate(quote, "https://cdn.example/p.pdf", "abc123");

        db.Leads.Add(NewLeadRecord(LeadId));
        db.SolarSizings.Add(sizing);
        db.SolarQuotes.Add(quote);
        db.Proposals.Add(proposal);

        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        var loadedSizing = await db.SolarSizings.SingleAsync(s => s.Id == SizingId);
        var loadedQuote = await db.SolarQuotes.SingleAsync(q => q.Id == QuoteId);
        var loadedProposal = await db.Proposals.SingleAsync(p => p.Id == proposalId);

        Assert.Equal(SolarSizingStatus.Approved, loadedSizing.Status);
        Assert.Equal("{\"kwp\":4.0}", loadedSizing.ResultsJson);
        Assert.Equal(SizingId, loadedQuote.SizingId);
        Assert.Equal(SolarQuoteStatus.Approved, loadedQuote.Status);
        Assert.Equal(1320m, loadedQuote.TotalPrice);
        Assert.Equal(QuoteId, loadedProposal.QuoteId);
        Assert.Equal(ProposalStatus.Generated, loadedProposal.Status);
        Assert.Equal("abc123", loadedProposal.FileHash);
    }

    // --- Relational constraints (SQLite) ---

    [Fact]
    public async Task Solar_delete_lead_with_sizing_is_restricted()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var leadId = Guid.NewGuid();
        var sizingId = Guid.NewGuid();

        db.Leads.Add(NewLeadRecord(leadId));
        db.SolarSizings.Add(SolarSizingRecord.Create(sizingId, leadId, "Light RJ", "B", "Convencional", "1.0.0", "{}"));
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        await AssertThrowsForeignKeyConstraintAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"leads\" WHERE \"Id\" = {leadId}"));
    }

    [Fact]
    public async Task Solar_delete_sizing_cascades_quotes_and_proposals()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var proposalId = Guid.NewGuid();

        var sizing = SolarSizingRecord.Create(SizingId, LeadId, "Light RJ", "B", "Convencional", "1.0.0", "{}");
        sizing.Calculate("{}");
        sizing.Approve();
        var quote = SolarQuoteRecord.Create(QuoteId, sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");
        quote.Approve(sizing);
        var proposal = ProposalRecord.Create(proposalId, quote, "1.0.0", "{}");

        db.Leads.Add(NewLeadRecord(LeadId));
        db.SolarSizings.Add(sizing);
        db.SolarQuotes.Add(quote);
        db.Proposals.Add(proposal);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"solar_sizings\" WHERE \"Id\" = {SizingId}");

        Assert.Equal(0, await db.SolarQuotes.CountAsync());
        Assert.Equal(0, await db.Proposals.CountAsync());
    }

    [Fact]
    public async Task Solar_quote_status_check_constraint_rejects_invalid_value()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var sizing = SolarSizingRecord.Create(SizingId, LeadId, "Light RJ", "B", "Convencional", "1.0.0", "{}");
        sizing.Calculate("{}");
        var quote = SolarQuoteRecord.Create(QuoteId, sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");

        db.Leads.Add(NewLeadRecord(LeadId));
        db.SolarSizings.Add(sizing);
        db.SolarQuotes.Add(quote);
        await db.SaveChangesAsync();

        await AssertThrowsCheckConstraintAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"solar_quotes\" SET \"Status\" = 'Bogus' WHERE \"Id\" = {QuoteId}"));
    }

    [Fact]
    public async Task Solar_quote_money_check_constraint_rejects_negative_total_cost()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var sizing = SolarSizingRecord.Create(SizingId, LeadId, "Light RJ", "B", "Convencional", "1.0.0", "{}");
        sizing.Calculate("{}");
        var quote = SolarQuoteRecord.Create(QuoteId, sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");

        db.Leads.Add(NewLeadRecord(LeadId));
        db.SolarSizings.Add(sizing);
        db.SolarQuotes.Add(quote);
        await db.SaveChangesAsync();

        await AssertThrowsCheckConstraintAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"solar_quotes\" SET \"TotalCost\" = -1 WHERE \"Id\" = {QuoteId}"));
    }

    [Fact]
    public async Task Solar_quote_percent_check_constraint_rejects_negative_margin()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var sizing = SolarSizingRecord.Create(SizingId, LeadId, "Light RJ", "B", "Convencional", "1.0.0", "{}");
        sizing.Calculate("{}");
        var quote = SolarQuoteRecord.Create(QuoteId, sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");

        db.Leads.Add(NewLeadRecord(LeadId));
        db.SolarSizings.Add(sizing);
        db.SolarQuotes.Add(quote);
        await db.SaveChangesAsync();

        await AssertThrowsCheckConstraintAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"solar_quotes\" SET \"MarginPercent\" = -1 WHERE \"Id\" = {QuoteId}"));
    }

    private static async Task AssertThrowsCheckConstraintAsync(Func<Task> action)
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(action);
        Assert.Contains("CHECK", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertThrowsForeignKeyConstraintAsync(Func<Task> action)
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(action);
        Assert.Contains("FOREIGN KEY", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
