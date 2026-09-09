using System.Reflection;
using Ecosologic.Domain.Crm;
using Ecosologic.Domain.Solar;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Tests;

public class SolarPersistenceInvariantsTests
{
    private static readonly Guid LeadId = Guid.NewGuid();
    private static readonly Guid SizingId = Guid.NewGuid();
    private static readonly Guid QuoteId = Guid.NewGuid();
    private static readonly Guid ProposalId = Guid.NewGuid();

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

    private static SolarSizingRecord NewSizing() =>
        SolarSizingRecord.Create(SizingId, LeadId, "Light RJ", "B", "Convencional", "1.0.0", "{\"kWh\":600}");

    private static SolarSizingRecord NewCalculatedSizing()
    {
        var sizing = NewSizing();
        sizing.Calculate("{\"kwp\":4.0}");
        return sizing;
    }

    private static SolarSizingRecord NewApprovedSizing()
    {
        var sizing = NewCalculatedSizing();
        sizing.Approve();
        return sizing;
    }

    private static SolarQuoteRecord NewQuote(SolarSizingRecord sizing) =>
        SolarQuoteRecord.Create(QuoteId, sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");

    private static SolarQuoteRecord NewApprovedQuote()
    {
        var sizing = NewApprovedSizing();
        var quote = NewQuote(sizing);
        quote.Approve(sizing);
        return quote;
    }

    private static ProposalRecord NewProposal(SolarQuoteRecord quote) =>
        ProposalRecord.Create(ProposalId, quote, "1.0.0", "{}");

    // --- Finding 1: records não permitem bypass de transições ---

    [Fact]
    public void Solar_records_expose_no_public_setters_for_status_or_identity()
    {
        AssertNonPublicSetter<SolarSizingRecord>(
            nameof(SolarSizingRecord.Id),
            nameof(SolarSizingRecord.Status),
            nameof(SolarSizingRecord.UpdatedAt));

        AssertNonPublicSetter<SolarQuoteRecord>(
            nameof(SolarQuoteRecord.Id),
            nameof(SolarQuoteRecord.Status),
            nameof(SolarQuoteRecord.UpdatedAt));

        AssertNonPublicSetter<ProposalRecord>(
            nameof(ProposalRecord.Id),
            nameof(ProposalRecord.Status),
            nameof(ProposalRecord.UpdatedAt));
    }

    // --- Finding 2: factories reutilizam validação de domínio e validam JSON ---

    [Fact]
    public void SolarSizingRecord_Create_rejects_invalid_inputs_json() =>
        Assert.Throws<ArgumentException>(() =>
            SolarSizingRecord.Create(SizingId, LeadId, "Light RJ", "B", "Convencional", "1.0.0", "not-json"));

    [Fact]
    public void SolarSizingRecord_Create_rejects_empty_inputs_json() =>
        Assert.Throws<ArgumentException>(() =>
            SolarSizingRecord.Create(SizingId, LeadId, "Light RJ", "B", "Convencional", "1.0.0", "   "));

    [Fact]
    public void SolarSizingRecord_Create_rejects_empty_engine_version() =>
        Assert.Throws<ArgumentException>(() =>
            SolarSizingRecord.Create(SizingId, LeadId, "Light RJ", "B", "Convencional", "", "{\"kWh\":600}"));

    [Fact]
    public void SolarSizingRecord_Create_rejects_empty_grupo() =>
        Assert.Throws<ArgumentException>(() =>
            SolarSizingRecord.Create(SizingId, LeadId, "Light RJ", "", "Convencional", "1.0.0", "{\"kWh\":600}"));

    [Fact]
    public void SolarSizingRecord_Create_rejects_empty_modalidade() =>
        Assert.Throws<ArgumentException>(() =>
            SolarSizingRecord.Create(SizingId, LeadId, "Light RJ", "B", "   ", "1.0.0", "{\"kWh\":600}"));

    [Fact]
    public void SolarSizingRecord_Create_rejects_empty_id() =>
        Assert.Throws<ArgumentException>(() =>
            SolarSizingRecord.Create(Guid.Empty, LeadId, "Light RJ", "B", "Convencional", "1.0.0", "{\"kWh\":600}"));

    [Fact]
    public void SolarQuoteRecord_Create_rejects_invalid_snapshot_json() =>
        Assert.Throws<ArgumentException>(() =>
            SolarQuoteRecord.Create(QuoteId, NewCalculatedSizing(), "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "not-json"));

    [Fact]
    public void SolarQuoteRecord_Create_rejects_invalid_items_json() =>
        Assert.Throws<ArgumentException>(() =>
            SolarQuoteRecord.Create(QuoteId, NewCalculatedSizing(), "not-json", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));

    [Fact]
    public void SolarQuoteRecord_Create_rejects_invalid_conditions_json() =>
        Assert.Throws<ArgumentException>(() =>
            SolarQuoteRecord.Create(QuoteId, NewCalculatedSizing(), "[]", 1000m, 20m, 10m, 1320m, "not-json", DateTimeOffset.UtcNow.AddDays(30), "{}"));

    [Fact]
    public void SolarQuoteRecord_Create_rejects_empty_conditions_json() =>
        Assert.Throws<ArgumentException>(() =>
            SolarQuoteRecord.Create(QuoteId, NewCalculatedSizing(), "[]", 1000m, 20m, 10m, 1320m, "   ", DateTimeOffset.UtcNow.AddDays(30), "{}"));

    [Fact]
    public void SolarQuoteRecord_Create_rejects_empty_id() =>
        Assert.Throws<ArgumentException>(() =>
            SolarQuoteRecord.Create(Guid.Empty, NewCalculatedSizing(), "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));

    [Fact]
    public void SolarQuoteRecord_Create_rejects_negative_total_cost() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarQuoteRecord.Create(QuoteId, NewCalculatedSizing(), "[]", -1m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));

    [Fact]
    public void SolarQuoteRecord_Create_rejects_margin_with_more_than_four_decimals() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SolarQuoteRecord.Create(QuoteId, NewCalculatedSizing(), "[]", 1000m, 20.12345m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));

    [Fact]
    public void ProposalRecord_Create_rejects_invalid_payload_json() =>
        Assert.Throws<ArgumentException>(() =>
            ProposalRecord.Create(ProposalId, NewApprovedQuote(), "1.0.0", "not-json"));

    [Fact]
    public void ProposalRecord_Create_rejects_empty_template_version() =>
        Assert.Throws<ArgumentException>(() =>
            ProposalRecord.Create(ProposalId, NewApprovedQuote(), "   ", "{}"));

    [Fact]
    public void ProposalRecord_Create_rejects_empty_id() =>
        Assert.Throws<ArgumentException>(() =>
            ProposalRecord.Create(Guid.Empty, NewApprovedQuote(), "1.0.0", "{}"));

    // --- Finding 3: invariantes aplicados pelos métodos controlados ---

    [Fact]
    public void SolarSizingRecord_Calculate_rejects_invalid_results_json() =>
        Assert.Throws<ArgumentException>(() => NewSizing().Calculate("not-json"));

    [Fact]
    public void SolarSizingRecord_cannot_approve_before_calculate() =>
        Assert.Throws<InvalidOperationException>(() => NewSizing().Approve());

    [Fact]
    public void SolarQuoteRecord_cannot_send_before_approve() =>
        Assert.Throws<InvalidOperationException>(() => NewQuote(NewCalculatedSizing()).Send());

    [Fact]
    public void SolarQuoteRecord_Create_rejects_draft_sizing() =>
        Assert.Throws<InvalidOperationException>(() =>
            SolarQuoteRecord.Create(QuoteId, NewSizing(), "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}"));

    [Fact]
    public void SolarQuoteRecord_Approve_requires_approved_sizing()
    {
        var calculated = NewCalculatedSizing();
        var quote = NewQuote(calculated);

        Assert.Throws<InvalidOperationException>(() => quote.Approve(calculated));
    }

    [Fact]
    public void SolarQuoteRecord_Approve_rejects_mismatched_sizing()
    {
        var leadId = Guid.NewGuid();

        var sizing = SolarSizingRecord.Create(Guid.NewGuid(), leadId, "Light RJ", "B", "Convencional", "1.0.0", "{}");
        sizing.Calculate("{}");
        sizing.Approve();

        var quote = SolarQuoteRecord.Create(Guid.NewGuid(), sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");

        var otherSizing = SolarSizingRecord.Create(Guid.NewGuid(), leadId, "Light RJ", "B", "Convencional", "1.0.0", "{}");
        otherSizing.Calculate("{}");
        otherSizing.Approve();

        Assert.Throws<InvalidOperationException>(() => quote.Approve(otherSizing));
    }

    [Fact]
    public void ProposalRecord_generate_requires_file_url() =>
        Assert.Throws<ArgumentException>(() => NewProposal(NewApprovedQuote()).Generate(NewApprovedQuote(), "   ", null));

    [Fact]
    public void ProposalRecord_cannot_send_before_generate() =>
        Assert.Throws<InvalidOperationException>(() => NewProposal(NewApprovedQuote()).Send());

    [Fact]
    public void ProposalRecord_Create_rejects_non_approved_quote()
    {
        var quote = NewQuote(NewCalculatedSizing());

        Assert.Throws<InvalidOperationException>(() =>
            ProposalRecord.Create(ProposalId, quote, "1.0.0", "{}"));
    }

    [Fact]
    public void ProposalRecord_Generate_requires_approved_quote()
    {
        var proposal = NewProposal(NewApprovedQuote());
        var nonApprovedQuote = NewQuote(NewCalculatedSizing());

        Assert.Throws<InvalidOperationException>(() => proposal.Generate(nonApprovedQuote, "https://cdn.example/p.pdf", null));
    }

    [Fact]
    public void ProposalRecord_Generate_rejects_mismatched_quote()
    {
        var leadId = Guid.NewGuid();

        var sizing = SolarSizingRecord.Create(Guid.NewGuid(), leadId, "Light RJ", "B", "Convencional", "1.0.0", "{}");
        sizing.Calculate("{}");
        sizing.Approve();

        var quote = SolarQuoteRecord.Create(Guid.NewGuid(), sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");
        quote.Approve(sizing);

        var proposal = ProposalRecord.Create(Guid.NewGuid(), quote, "1.0.0", "{}");

        var otherQuote = SolarQuoteRecord.Create(Guid.NewGuid(), sizing, "[]", 1000m, 20m, 10m, 1320m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");
        otherQuote.Approve(sizing);

        Assert.Throws<InvalidOperationException>(() => proposal.Generate(otherQuote, "https://cdn.example/p.pdf", null));
    }

    // --- Finding 3: CHECK constraints (SQLite EnsureCreated) ---

    [Fact]
    public async Task Solar_sizing_check_constraint_requires_results_for_calculated()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.Leads.Add(NewLeadRecord(LeadId));
        db.SolarSizings.Add(NewSizing());
        await db.SaveChangesAsync();

        await AssertThrowsCheckConstraintAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"solar_sizings\" SET \"Status\" = 'Calculated' WHERE \"Id\" = {SizingId}"));
    }

    [Fact]
    public async Task Solar_quote_check_constraint_requires_snapshot_for_approved()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var sizing = NewCalculatedSizing();
        var quote = NewQuote(sizing);

        db.Leads.Add(NewLeadRecord(LeadId));
        db.SolarSizings.Add(sizing);
        db.SolarQuotes.Add(quote);
        await db.SaveChangesAsync();

        await AssertThrowsCheckConstraintAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"solar_quotes\" SET \"Status\" = 'Approved', \"SnapshotJson\" = '' WHERE \"Id\" = {QuoteId}"));
    }

    [Fact]
    public async Task Proposal_check_constraint_requires_file_url_for_generated()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var sizing = NewApprovedSizing();
        var quote = NewQuote(sizing);
        quote.Approve(sizing);
        var proposal = NewProposal(quote);

        db.Leads.Add(NewLeadRecord(LeadId));
        db.SolarSizings.Add(sizing);
        db.SolarQuotes.Add(quote);
        db.Proposals.Add(proposal);
        await db.SaveChangesAsync();

        await AssertThrowsCheckConstraintAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"proposals\" SET \"Status\" = 'Generated' WHERE \"Id\" = {ProposalId}"));
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

    private static async Task AssertThrowsCheckConstraintAsync(Func<Task> action)
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(action);
        Assert.Contains("CHECK", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
