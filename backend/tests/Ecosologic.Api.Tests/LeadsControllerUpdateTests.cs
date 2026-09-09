using System.Reflection;
using System.Text.Json;
using Ecosologic.Api;
using Ecosologic.Api.Controllers;
using Ecosologic.Domain.Crm;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Tests;

public class LeadsControllerUpdateTests
{
    private static EcosologicDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EcosologicDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EcosologicDbContext(options);
    }

    private static LeadRecord NewRecord() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Fabio",
        Phone = "+5521995424027",
        Email = "fabio@ecosologic.com.br",
        Message = "Orçamento",
        Stage = LeadStage.New,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public void Update_endpoint_requires_admin_role()
    {
        var attributes = typeof(LeadsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);
        var authorize = Assert.Single(attributes);
        Assert.Equal("Admin", ((AuthorizeAttribute)authorize).Roles);
    }

    [Fact]
    public void Create_endpoint_is_anonymous()
    {
        var create = typeof(LeadsController).GetMethod(nameof(LeadsController.Create));
        Assert.NotNull(create!.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public async Task Update_patches_stage_and_notes_and_sets_updated_at()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).Update(record.Id, new UpdateLeadRequest("Contacted", "Cliente pediu retorno"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var updated = Assert.IsType<LeadRecord>(ok.Value);
        Assert.Equal(LeadStage.Contacted, updated.Stage);
        Assert.Equal("Cliente pediu retorno", updated.Notes);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_returns_404_for_missing_id()
    {
        await using var db = NewContext();

        var result = await new LeadsController(db).Update(Guid.NewGuid(), new UpdateLeadRequest("Contacted", null), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_sets_won_at_when_stage_changes_to_won()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).Update(record.Id, new UpdateLeadRequest("Won", null), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var stored = await db.Leads.AsNoTracking().SingleAsync(l => l.Id == record.Id);
        Assert.Equal(LeadStage.Won, stored.Stage);
        Assert.NotNull(stored.WonAt);
        Assert.Equal(TimeSpan.Zero, stored.WonAt!.Value.Offset);
    }

    [Fact]
    public async Task Update_clears_won_at_when_stage_leaves_won()
    {
        await using var db = NewContext();
        var record = NewRecord();
        record.Stage = LeadStage.Won;
        record.WonAt = DateTimeOffset.UtcNow.AddDays(-1);
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).Update(record.Id, new UpdateLeadRequest("Lost", null), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var stored = await db.Leads.AsNoTracking().SingleAsync(l => l.Id == record.Id);
        Assert.Equal(LeadStage.Lost, stored.Stage);
        Assert.Null(stored.WonAt);
    }

    [Fact]
    public async Task Update_preserves_won_at_when_editing_notes_or_reminder()
    {
        await using var db = NewContext();
        var record = NewRecord();
        record.Stage = LeadStage.Won;
        var wonAt = DateTimeOffset.UtcNow.AddDays(-3);
        record.WonAt = wonAt;
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).Update(record.Id, new UpdateLeadRequest(null, "Nova observação"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var stored = await db.Leads.AsNoTracking().SingleAsync(l => l.Id == record.Id);
        Assert.Equal("Nova observação", stored.Notes);
        Assert.Equal(wonAt, stored.WonAt);
    }

    [Fact]
    public async Task Update_returns_400_for_invalid_stage()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).Update(record.Id, new UpdateLeadRequest("InvalidStage", null), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }

    [Fact]
    public async Task Update_preserves_reminder_when_not_sent()
    {
        await using var db = NewContext();
        var record = NewRecord();
        var originalReminder = DateTimeOffset.UtcNow.AddDays(2);
        record.ReminderAt = originalReminder;
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).Update(record.Id, new UpdateLeadRequest(null, "Nova observação"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var stored = await db.Leads.AsNoTracking().SingleAsync(l => l.Id == record.Id);
        Assert.Equal(originalReminder, stored.ReminderAt);
    }

    [Fact]
    public async Task Update_clears_reminder_when_explicitly_null()
    {
        await using var db = NewContext();
        var record = NewRecord();
        record.ReminderAt = DateTimeOffset.UtcNow.AddDays(2);
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).Update(record.Id, new UpdateLeadRequest(null, null, new Optional<DateTimeOffset>(null)), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var stored = await db.Leads.AsNoTracking().SingleAsync(l => l.Id == record.Id);
        Assert.Null(stored.ReminderAt);
    }

    [Fact]
    public async Task Update_rejects_notes_longer_than_4000()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).Update(record.Id, new UpdateLeadRequest(null, new string('a', 4001)), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }

    [Fact]
    public async Task Update_rejects_non_utc_reminder_offset()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var reminder = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.FromHours(-3));
        var result = await new LeadsController(db).Update(record.Id, new UpdateLeadRequest(null, null, new Optional<DateTimeOffset>(reminder)), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }

    [Fact]
    public async Task Update_sets_reminder_when_utc()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var reminder = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var result = await new LeadsController(db).Update(record.Id, new UpdateLeadRequest(null, null, new Optional<DateTimeOffset>(reminder)), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var stored = await db.Leads.AsNoTracking().SingleAsync(l => l.Id == record.Id);
        Assert.Equal(reminder, stored.ReminderAt);
        Assert.Equal(TimeSpan.Zero, stored.ReminderAt!.Value.Offset);
    }

    [Fact]
    public void UpdateLeadRequest_distinguishes_absent_from_null_reminder()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var absent = JsonSerializer.Deserialize<UpdateLeadRequest>("{}", options)!;
        Assert.False(absent.ReminderAt.HasValue);

        var explicitNull = JsonSerializer.Deserialize<UpdateLeadRequest>("{\"reminderAt\":null}", options)!;
        Assert.True(explicitNull.ReminderAt.HasValue);
        Assert.Null(explicitNull.ReminderAt.Value);

        var withValue = JsonSerializer.Deserialize<UpdateLeadRequest>("{\"reminderAt\":\"2026-09-03T10:00:00Z\"}", options)!;
        Assert.True(withValue.ReminderAt.HasValue);
        Assert.Equal(DateTimeOffset.Parse("2026-09-03T10:00:00Z"), withValue.ReminderAt.Value);
    }
}
