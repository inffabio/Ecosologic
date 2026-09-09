using Ecosologic.Api.Controllers;
using Ecosologic.Domain.Crm;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Tests;

public class LeadsControllerListTests
{
    private static EcosologicDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EcosologicDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EcosologicDbContext(options);
    }

    private static LeadRecord NewRecord(
        string name = "Fabio",
        string phone = "+5521995424027",
        string email = "fabio@ecosologic.com.br",
        string message = "Orçamento",
        LeadStage stage = LeadStage.New,
        DateTimeOffset? createdAt = null,
        Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = name,
        Phone = phone,
        Email = email,
        Message = message,
        Stage = stage,
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow
    };

    private static LeadListResponse OkValue(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<LeadListResponse>(ok.Value);
    }

    [Fact]
    public void List_endpoint_requires_admin_role()
    {
        var attributes = typeof(LeadsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);
        var authorize = Assert.Single(attributes);
        Assert.Equal("Admin", ((AuthorizeAttribute)authorize).Roles);
    }

    [Fact]
    public async Task List_returns_paginated_response_with_items_and_totals()
    {
        await using var db = NewContext();
        db.Leads.AddRange(
            NewRecord(name: "A"),
            NewRecord(name: "B"),
            NewRecord(name: "C"));
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).List(null, null, page: 1, pageSize: 2, CancellationToken.None);

        var response = OkValue(result);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(3, response.Total);
        Assert.Equal(1, response.Page);
        Assert.Equal(2, response.PageSize);
        Assert.Equal(2, response.TotalPages);
    }

    [Fact]
    public async Task List_orders_created_at_desc_then_id_desc()
    {
        await using var db = NewContext();
        var now = DateTimeOffset.UtcNow;
        var first = NewRecord(name: "Primeiro", createdAt: now.AddMinutes(-2));
        var second = NewRecord(name: "Segundo", createdAt: now.AddMinutes(-1));
        var third = NewRecord(name: "Terceiro", createdAt: now);
        db.Leads.AddRange(first, second, third);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).List(null, null, 1, 20, CancellationToken.None);

        var response = OkValue(result);
        Assert.Equal(new[] { "Terceiro", "Segundo", "Primeiro" }, response.Items.Select(l => l.Name));
    }

    [Fact]
    public async Task List_breaks_created_at_ties_by_id_desc()
    {
        await using var db = NewContext();
        var shared = DateTimeOffset.UtcNow;
        var lowerId = new Guid("00000000-0000-0000-0000-000000000001");
        var higherId = new Guid("00000000-0000-0000-0000-000000000002");
        db.Leads.AddRange(
            NewRecord(name: "A", createdAt: shared, id: lowerId),
            NewRecord(name: "B", createdAt: shared, id: higherId));
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).List(null, null, 1, 20, CancellationToken.None);

        var response = OkValue(result);
        Assert.Equal(new[] { "B", "A" }, response.Items.Select(l => l.Name));
    }

    [Fact]
    public async Task List_filters_by_q_case_insensitive_across_fields()
    {
        await using var db = NewContext();
        db.Leads.AddRange(
            NewRecord(name: "CARLOS"),
            NewRecord(email: "ANA@ecosologic.com.br"),
            NewRecord(phone: "ana telefone"),
            NewRecord(message: "quero ana solar"),
            NewRecord(name: "Bruno"));
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).List("ANA", null, 1, 20, CancellationToken.None);

        var response = OkValue(result);
        Assert.Equal(3, response.Total);
        Assert.All(response.Items, lead => Assert.True(
            lead.Name.Contains("ANA", StringComparison.OrdinalIgnoreCase)
            || lead.Email.Contains("ANA", StringComparison.OrdinalIgnoreCase)
            || lead.Phone.Contains("ANA", StringComparison.OrdinalIgnoreCase)
            || lead.Message.Contains("ANA", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task List_filters_by_stage()
    {
        await using var db = NewContext();
        db.Leads.AddRange(
            NewRecord(name: "Novo", stage: LeadStage.New),
            NewRecord(name: "Contatado", stage: LeadStage.Contacted),
            NewRecord(name: "Outro Novo", stage: LeadStage.New));
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).List(null, "New", 1, 20, CancellationToken.None);

        var response = OkValue(result);
        Assert.Equal(2, response.Total);
        Assert.All(response.Items, lead => Assert.Equal(LeadStage.New, lead.Stage));
    }

    [Fact]
    public async Task List_filters_by_stage_case_insensitive()
    {
        await using var db = NewContext();
        db.Leads.Add(NewRecord(name: "Contatado", stage: LeadStage.Contacted));
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).List(null, "contacted", 1, 20, CancellationToken.None);

        var response = OkValue(result);
        Assert.Single(response.Items);
    }

    [Fact]
    public async Task List_returns_400_for_invalid_stage()
    {
        await using var db = NewContext();

        var result = await new LeadsController(db).List(null, "InvalidStage", 1, 20, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }

    [Fact]
    public async Task List_defaults_page_and_page_size()
    {
        await using var db = NewContext();
        db.Leads.AddRange(Enumerable.Range(0, 25).Select(i => NewRecord(name: $"Lead {i}")));
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).List(null, null, page: 0, pageSize: 0, CancellationToken.None);

        var response = OkValue(result);
        Assert.Equal(1, response.Page);
        Assert.Equal(20, response.PageSize);
        Assert.Equal(20, response.Items.Count);
        Assert.Equal(25, response.Total);
        Assert.Equal(2, response.TotalPages);
    }

    [Fact]
    public async Task List_caps_page_size_at_50()
    {
        await using var db = NewContext();
        db.Leads.AddRange(Enumerable.Range(0, 60).Select(i => NewRecord(name: $"Lead {i}")));
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).List(null, null, 1, 999, CancellationToken.None);

        var response = OkValue(result);
        Assert.Equal(50, response.PageSize);
        Assert.Equal(50, response.Items.Count);
        Assert.Equal(60, response.Total);
    }

    [Fact]
    public async Task List_returns_empty_items_when_no_results()
    {
        await using var db = NewContext();
        db.Leads.Add(NewRecord(name: "Fabio"));
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).List("inexistente", null, 1, 20, CancellationToken.None);

        var response = OkValue(result);
        Assert.Empty(response.Items);
        Assert.Equal(0, response.Total);
        Assert.Equal(0, response.TotalPages);
    }

    [Fact]
    public async Task List_returns_last_page_when_requested_page_exceeds_total_pages()
    {
        await using var db = NewContext();
        db.Leads.AddRange(Enumerable.Range(0, 25).Select(i => NewRecord(name: $"Lead {i}")));
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).List(null, null, 9, 10, CancellationToken.None);

        var response = OkValue(result);
        Assert.Equal(3, response.Page);
        Assert.Equal(3, response.TotalPages);
        Assert.Equal(5, response.Items.Count);
        Assert.Equal(25, response.Total);
    }

    [Fact]
    public async Task List_returns_empty_items_when_requested_page_exceeds_empty_result()
    {
        await using var db = NewContext();

        var result = await new LeadsController(db).List("inexistente", null, 9, 20, CancellationToken.None);

        var response = OkValue(result);
        Assert.Empty(response.Items);
        Assert.Equal(0, response.Total);
        Assert.Equal(0, response.TotalPages);
    }

    [Fact]
    public async Task List_returns_page_1_when_total_is_zero()
    {
        await using var db = NewContext();

        var result = await new LeadsController(db).List(null, null, 3, 20, CancellationToken.None);

        var response = OkValue(result);
        Assert.Equal(1, response.Page);
        Assert.Equal(0, response.Total);
        Assert.Equal(0, response.TotalPages);
        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task List_returns_400_when_page_exceeds_safe_limit()
    {
        await using var db = NewContext();

        var result = await new LeadsController(db).List(null, null, int.MaxValue, 20, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }

    [Fact]
    public async Task StageCounts_returns_count_per_stage()
    {
        await using var db = NewContext();
        db.Leads.AddRange(
            NewRecord(name: "A", stage: LeadStage.New),
            NewRecord(name: "B", stage: LeadStage.New),
            NewRecord(name: "C", stage: LeadStage.Won));
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).StageCounts(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var counts = Assert.IsType<Dictionary<string, int>>(ok.Value);
        Assert.Equal(2, counts["New"]);
        Assert.Equal(1, counts["Won"]);
        Assert.Equal(0, counts["Lost"]);
    }

    [Fact]
    public async Task StageCounts_returns_zero_for_all_stages_when_empty()
    {
        await using var db = NewContext();

        var result = await new LeadsController(db).StageCounts(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var counts = Assert.IsType<Dictionary<string, int>>(ok.Value);
        Assert.Equal(Enum.GetValues<LeadStage>().Length, counts.Count);
        Assert.All(counts.Values, count => Assert.Equal(0, count));
    }

    [Fact]
    public async Task MonthlyWonCount_counts_won_leads_closed_this_month()
    {
        await using var db = NewContext();
        var now = DateTimeOffset.UtcNow;

        var wonThisMonth = NewRecord(name: "A", stage: LeadStage.Won);
        wonThisMonth.WonAt = now;
        var wonLastMonth = NewRecord(name: "B", stage: LeadStage.Won);
        wonLastMonth.WonAt = now.AddMonths(-1);
        var notWon = NewRecord(name: "C", stage: LeadStage.New);
        notWon.WonAt = now;
        var wonNoWonAt = NewRecord(name: "D", stage: LeadStage.Won);

        db.Leads.AddRange(wonThisMonth, wonLastMonth, notWon, wonNoWonAt);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).MonthlyWonCount(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, ok.Value);
    }

    [Fact]
    public async Task MonthlyWonCount_counts_by_won_at_not_updated_at()
    {
        await using var db = NewContext();
        var now = DateTimeOffset.UtcNow;

        var wonThisMonthEditedLater = NewRecord(name: "A", stage: LeadStage.Won);
        wonThisMonthEditedLater.WonAt = now;
        wonThisMonthEditedLater.UpdatedAt = now.AddMonths(1);
        var updatedThisMonthWonEarlier = NewRecord(name: "B", stage: LeadStage.Won);
        updatedThisMonthWonEarlier.WonAt = now.AddMonths(-2);
        updatedThisMonthWonEarlier.UpdatedAt = now;

        db.Leads.AddRange(wonThisMonthEditedLater, updatedThisMonthWonEarlier);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).MonthlyWonCount(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, ok.Value);
    }
}
