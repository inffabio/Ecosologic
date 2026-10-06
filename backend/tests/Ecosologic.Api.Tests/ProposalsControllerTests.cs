using Ecosologic.Api.Controllers;
using Ecosologic.Api.Contracts;
using Ecosologic.Api.Media;
using Ecosologic.Application.Solar;
using Ecosologic.Domain.Crm;
using Ecosologic.Domain.Solar;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Ecosologic.Api.Tests;

public sealed class ProposalsControllerTests
{
    private sealed class FakeStorage : IMediaStorage
    {
        public byte[] Content { get; private set; } = [];

        public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Content = buffer.ToArray();
            return "proposal-test.pdf";
        }
    }

    private static EcosologicDbContext NewDb(out SqliteConnection connection)
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var db = new EcosologicDbContext(new DbContextOptionsBuilder<EcosologicDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<SolarQuoteRecord> ApprovedQuote(EcosologicDbContext db)
    {
        var leadId = Guid.NewGuid();
        var sizingId = Guid.NewGuid();
        db.Leads.Add(new LeadRecord { Id = leadId, Name = "Lead", Phone = "123", Email = "", Message = "", Stage = LeadStage.New, CreatedAt = DateTimeOffset.UtcNow });
        var sizing = SolarSizingRecord.Create(sizingId, leadId, "Light RJ", "B", "Convencional", "1.0.0", "{}");
        sizing.Calculate("{}");
        sizing.Approve();
        var quote = SolarQuoteRecord.Create(Guid.NewGuid(), sizing, "[]", 100m, 20m, 0m, 120m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");
        quote.Approve(sizing);
        db.SolarSizings.Add(sizing);
        db.SolarQuotes.Add(quote);
        await db.SaveChangesAsync();
        return quote;
    }

    [Fact]
    public async Task Generate_creates_pdf_and_hash_for_approved_quote()
    {
        using var db = NewDb(out var connection);
        using var _ = connection;
        var quote = await ApprovedQuote(db);
        var storage = new FakeStorage();
        var result = await new ProposalsController(db, storage, new ProposalRenderer()).Generate(
            new ProposalGenerateRequest { QuoteId = quote.Id, TemplateVersion = "proposal-1.0", PayloadJson = "{\"title\":\"Teste\"}" },
            CancellationToken.None);

        var response = Assert.IsType<ProposalResponse>(Assert.IsType<CreatedAtActionResult>(result).Value);
        var saved = await db.Proposals.SingleAsync();
        Assert.Equal("Generated", response.Status);
        Assert.StartsWith("%PDF-1.4", System.Text.Encoding.ASCII.GetString(storage.Content));
        Assert.Equal(response.FileHash, saved.FileHash);
        Assert.Equal("/uploads/proposal-test.pdf", saved.FileUrl);
    }

    [Fact]
    public async Task Generate_blocks_draft_quote()
    {
        using var db = NewDb(out var connection);
        using var _ = connection;
        var leadId = Guid.NewGuid();
        var sizing = SolarSizingRecord.Create(Guid.NewGuid(), leadId, "Light RJ", "B", "Convencional", "1.0.0", "{}");
        sizing.Calculate("{}");
        db.Leads.Add(new LeadRecord { Id = leadId, Name = "Lead", Phone = "123", Email = "", Message = "", Stage = LeadStage.New, CreatedAt = DateTimeOffset.UtcNow });
        db.SolarSizings.Add(sizing);
        await db.SaveChangesAsync();
        var quote = SolarQuoteRecord.Create(Guid.NewGuid(), sizing, "[]", 100m, 20m, 0m, 120m, "{}", DateTimeOffset.UtcNow.AddDays(30), "{}");
        db.SolarQuotes.Add(quote);
        await db.SaveChangesAsync();

        var result = await new ProposalsController(db, new FakeStorage(), new ProposalRenderer()).Generate(
            new ProposalGenerateRequest { QuoteId = quote.Id, PayloadJson = "{}" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.Proposals);
    }
}
