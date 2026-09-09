using Ecosologic.Api;
using Ecosologic.Api.Controllers;
using Ecosologic.Domain.Crm;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Tests;

public class LeadsControllerActivitiesTests
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
    public void Activities_endpoints_require_admin_role()
    {
        var attributes = typeof(LeadsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);
        var authorize = Assert.Single(attributes);
        Assert.Equal("Admin", ((AuthorizeAttribute)authorize).Roles);
    }

    [Fact]
    public async Task ListActivities_returns_empty_list_for_existing_lead()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).ListActivities(record.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Empty(Assert.IsType<List<LeadActivityRecord>>(ok.Value));
    }

    [Fact]
    public async Task ListActivities_returns_404_for_missing_lead()
    {
        await using var db = NewContext();

        var result = await new LeadsController(db).ListActivities(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ListActivities_orders_most_recent_first()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        db.LeadActivities.Add(new LeadActivityRecord { Id = Guid.NewGuid(), LeadId = record.Id, Type = "Note", Description = "Mais antiga", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) });
        db.LeadActivities.Add(new LeadActivityRecord { Id = Guid.NewGuid(), LeadId = record.Id, Type = "Note", Description = "Mais recente", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).ListActivities(record.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var activities = Assert.IsType<List<LeadActivityRecord>>(ok.Value);
        Assert.Equal("Mais recente", activities[0].Description);
    }

    [Fact]
    public async Task AddActivity_persists_note_and_updates_lead_updated_at()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).AddActivity(record.Id, new CreateActivityRequest("Note", "Cliente pediu retorno"), CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        var activity = Assert.IsType<LeadActivityRecord>(created.Value);
        Assert.Equal("Note", activity.Type);
        Assert.Equal("Cliente pediu retorno", activity.Description);
        Assert.Equal(record.Id, activity.LeadId);

        var stored = await db.Leads.SingleAsync(l => l.Id == record.Id);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task AddActivity_returns_404_for_missing_lead()
    {
        await using var db = NewContext();

        var result = await new LeadsController(db).AddActivity(Guid.NewGuid(), new CreateActivityRequest("Note", "Texto"), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AddActivity_requires_description()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).AddActivity(record.Id, new CreateActivityRequest("Note", "   "), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }

    [Fact]
    public async Task AddActivity_rejects_description_longer_than_4000()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).AddActivity(record.Id, new CreateActivityRequest("Note", new string('a', 4001)), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }

    [Fact]
    public async Task AddActivity_defaults_type_to_note_when_missing()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).AddActivity(record.Id, new CreateActivityRequest(null, "Nota sem tipo"), CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        var activity = Assert.IsType<LeadActivityRecord>(created.Value);
        Assert.Equal("Note", activity.Type);
    }

    [Fact]
    public async Task Update_registers_stage_changed_activity_with_previous_and_new_stage()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        await new LeadsController(db).Update(record.Id, new UpdateLeadRequest("Contacted", null), CancellationToken.None);

        var activity = await db.LeadActivities.SingleAsync();
        Assert.Equal("StageChanged", activity.Type);
        Assert.Contains("New", activity.Description);
        Assert.Contains("Contacted", activity.Description);
    }

    [Fact]
    public async Task Update_does_not_register_activity_when_stage_unchanged()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        await new LeadsController(db).Update(record.Id, new UpdateLeadRequest("New", null), CancellationToken.None);

        Assert.Empty(await db.LeadActivities.ToListAsync());
    }

    [Fact]
    public async Task Update_sets_and_clears_reminder()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var reminder = DateTimeOffset.UtcNow.AddDays(1);
        await new LeadsController(db).Update(record.Id, new UpdateLeadRequest(null, null, new Optional<DateTimeOffset>(reminder)), CancellationToken.None);
        var withReminder = await db.Leads.AsNoTracking().SingleAsync(l => l.Id == record.Id);
        Assert.Equal(reminder, withReminder.ReminderAt);

        await new LeadsController(db).Update(record.Id, new UpdateLeadRequest(null, null, new Optional<DateTimeOffset>(null)), CancellationToken.None);
        var cleared = await db.Leads.AsNoTracking().SingleAsync(l => l.Id == record.Id);
        Assert.Null(cleared.ReminderAt);
    }

    [Fact]
    public async Task AddActivity_rejects_type_longer_than_32()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).AddActivity(record.Id, new CreateActivityRequest(new string('a', 33), "Nota válida"), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }
}
