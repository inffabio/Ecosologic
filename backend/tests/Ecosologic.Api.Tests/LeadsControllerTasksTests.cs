using Ecosologic.Api;
using Ecosologic.Api.Controllers;
using Ecosologic.Domain.Crm;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Tests;

public class LeadsControllerTasksTests
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

    private static LeadTaskRecord NewTask(Guid leadId, string title, DateTimeOffset dueAt, DateTimeOffset? completedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        LeadId = leadId,
        Title = title,
        DueAt = dueAt,
        CompletedAt = completedAt,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static LeadTaskRecord NewTaskAt(Guid leadId, Guid id, string title, DateTimeOffset dueAt, DateTimeOffset createdAt, DateTimeOffset? completedAt = null) => new()
    {
        Id = id,
        LeadId = leadId,
        Title = title,
        DueAt = dueAt,
        CompletedAt = completedAt,
        CreatedAt = createdAt,
        UpdatedAt = createdAt
    };

    [Fact]
    public void Tasks_endpoints_require_admin_role()
    {
        var attributes = typeof(LeadsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);
        var authorize = Assert.Single(attributes);
        Assert.Equal("Admin", ((AuthorizeAttribute)authorize).Roles);
    }

    [Fact]
    public async Task ListTasks_returns_empty_list_for_existing_lead()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).ListTasks(record.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Empty(Assert.IsType<List<LeadTaskRecord>>(ok.Value));
    }

    [Fact]
    public async Task ListTasks_returns_404_for_missing_lead()
    {
        await using var db = NewContext();

        var result = await new LeadsController(db).ListTasks(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ListTasks_excludes_completed_by_default()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        db.LeadTasks.Add(NewTask(record.Id, "Pendente", DateTimeOffset.UtcNow.AddDays(1)));
        db.LeadTasks.Add(NewTask(record.Id, "Concluída", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).ListTasks(record.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var tasks = Assert.IsType<List<LeadTaskRecord>>(ok.Value);
        Assert.Equal(["Pendente"], tasks.Select(t => t.Title));
    }

    [Fact]
    public async Task ListTasks_includes_completed_when_requested()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        db.LeadTasks.Add(NewTask(record.Id, "Pendente", DateTimeOffset.UtcNow.AddDays(1)));
        db.LeadTasks.Add(NewTask(record.Id, "Concluída", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).ListTasks(record.Id, CancellationToken.None, includeCompleted: true);

        var ok = Assert.IsType<OkObjectResult>(result);
        var tasks = Assert.IsType<List<LeadTaskRecord>>(ok.Value);
        Assert.Equal(2, tasks.Count);
    }

    [Fact]
    public async Task ListTasks_orders_pending_by_due_at_then_completed()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        var now = DateTimeOffset.UtcNow;
        db.LeadTasks.Add(NewTask(record.Id, "T1", now.AddDays(2)));
        db.LeadTasks.Add(NewTask(record.Id, "T2", now.AddDays(1)));
        db.LeadTasks.Add(NewTask(record.Id, "T3", now.AddDays(3), now.AddHours(-1)));
        db.LeadTasks.Add(NewTask(record.Id, "T4", now.AddDays(3), now.AddHours(-2)));
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).ListTasks(record.Id, CancellationToken.None, includeCompleted: true);

        var ok = Assert.IsType<OkObjectResult>(result);
        var tasks = Assert.IsType<List<LeadTaskRecord>>(ok.Value);
        Assert.Equal(new[] { "T2", "T1", "T3", "T4" }, tasks.Select(t => t.Title));
    }

    [Fact]
    public async Task AddTask_persists_task_and_updates_lead_updated_at()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var dueAt = DateTimeOffset.UtcNow.AddDays(1);
        var result = await new LeadsController(db).AddTask(record.Id, new CreateTaskRequest("Ligar para o cliente", "Confirmar dados", dueAt), CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        var task = Assert.IsType<LeadTaskRecord>(created.Value);
        Assert.Equal("Ligar para o cliente", task.Title);
        Assert.Equal("Confirmar dados", task.Description);
        Assert.Equal(record.Id, task.LeadId);
        Assert.Null(task.CompletedAt);

        var stored = await db.Leads.SingleAsync(l => l.Id == record.Id);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task AddTask_returns_404_for_missing_lead()
    {
        await using var db = NewContext();

        var result = await new LeadsController(db).AddTask(Guid.NewGuid(), new CreateTaskRequest("Tarefa", null, DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AddTask_requires_title()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).AddTask(record.Id, new CreateTaskRequest("   ", null, DateTimeOffset.UtcNow), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }

    [Fact]
    public async Task AddTask_rejects_title_longer_than_160()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).AddTask(record.Id, new CreateTaskRequest(new string('a', 161), null, DateTimeOffset.UtcNow), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }

    [Fact]
    public async Task AddTask_rejects_description_longer_than_2000()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).AddTask(record.Id, new CreateTaskRequest("Tarefa", new string('a', 2001), DateTimeOffset.UtcNow), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }

    [Fact]
    public async Task AddTask_rejects_missing_due_at()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).AddTask(record.Id, new CreateTaskRequest("Tarefa", null, null), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Empty(await db.LeadTasks.ToListAsync());
    }

    [Fact]
    public async Task AddTask_rejects_min_value_due_at()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).AddTask(record.Id, new CreateTaskRequest("Tarefa", null, DateTimeOffset.MinValue), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }

    [Fact]
    public async Task AddTask_rejects_out_of_range_year()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).AddTask(record.Id, new CreateTaskRequest("Tarefa", null, new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero)), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }

    [Fact]
    public async Task AddTask_rejects_non_utc_offset()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).AddTask(record.Id, new CreateTaskRequest("Tarefa", null, new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.FromHours(-3))), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }

    [Fact]
    public async Task AddTask_normalizes_due_at_to_utc()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var dueAt = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var result = await new LeadsController(db).AddTask(record.Id, new CreateTaskRequest("Tarefa", null, dueAt), CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        var task = Assert.IsType<LeadTaskRecord>(created.Value);
        Assert.Equal(dueAt.UtcDateTime, task.DueAt.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, task.DueAt.Offset);
    }

    [Fact]
    public async Task ListTasks_breaks_ties_by_created_at_then_id()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        var now = DateTimeOffset.UtcNow;
        var sharedDue = now.AddDays(1);
        var laterId = Guid.NewGuid();
        var earlierId = Guid.NewGuid();
        db.LeadTasks.Add(NewTaskAt(record.Id, laterId, "B", sharedDue, now.AddMinutes(1)));
        db.LeadTasks.Add(NewTaskAt(record.Id, earlierId, "A", sharedDue, now));
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).ListTasks(record.Id, CancellationToken.None, includeCompleted: true);

        var ok = Assert.IsType<OkObjectResult>(result);
        var tasks = Assert.IsType<List<LeadTaskRecord>>(ok.Value);
        Assert.Equal(new[] { "A", "B" }, tasks.Select(t => t.Title));
    }

    [Fact]
    public async Task DeleteTask_updates_lead_updated_at()
    {
        await using var db = NewContext();
        var record = NewRecord();
        var previousUpdatedAt = DateTimeOffset.UtcNow.AddDays(-1);
        record.UpdatedAt = previousUpdatedAt;
        db.Leads.Add(record);
        var task = NewTask(record.Id, "Tarefa", DateTimeOffset.UtcNow.AddDays(1));
        db.LeadTasks.Add(task);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).DeleteTask(record.Id, task.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(await db.LeadTasks.ToListAsync());

        var stored = await db.Leads.SingleAsync(l => l.Id == record.Id);
        Assert.NotNull(stored.UpdatedAt);
        Assert.True(stored.UpdatedAt > previousUpdatedAt);
    }

    [Fact]
    public async Task UpdateTask_completes_task_and_updates_lead_updated_at()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        var task = NewTask(record.Id, "Tarefa", DateTimeOffset.UtcNow.AddDays(1));
        db.LeadTasks.Add(task);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).UpdateTask(record.Id, task.Id, new UpdateTaskRequest(true), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var updated = Assert.IsType<LeadTaskRecord>(ok.Value);
        Assert.NotNull(updated.CompletedAt);

        var stored = await db.Leads.SingleAsync(l => l.Id == record.Id);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task UpdateTask_reopens_task_and_updates_lead_updated_at()
    {
        await using var db = NewContext();
        var record = NewRecord();
        var previousUpdatedAt = DateTimeOffset.UtcNow.AddDays(-1);
        record.UpdatedAt = previousUpdatedAt;
        db.Leads.Add(record);
        var task = NewTask(record.Id, "Tarefa", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow);
        db.LeadTasks.Add(task);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).UpdateTask(record.Id, task.Id, new UpdateTaskRequest(false), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var updated = Assert.IsType<LeadTaskRecord>(ok.Value);
        Assert.Null(updated.CompletedAt);

        var stored = await db.Leads.SingleAsync(l => l.Id == record.Id);
        Assert.NotNull(stored.UpdatedAt);
        Assert.True(stored.UpdatedAt > previousUpdatedAt);
    }

    [Fact]
    public async Task UpdateTask_returns_404_for_missing_task()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).UpdateTask(record.Id, Guid.NewGuid(), new UpdateTaskRequest(true), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateTask_returns_404_when_task_belongs_to_other_lead()
    {
        await using var db = NewContext();
        var first = NewRecord();
        var second = NewRecord();
        db.Leads.AddRange(first, second);
        var task = NewTask(first.Id, "Tarefa", DateTimeOffset.UtcNow.AddDays(1));
        db.LeadTasks.Add(task);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).UpdateTask(second.Id, task.Id, new UpdateTaskRequest(true), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteTask_removes_task()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        var task = NewTask(record.Id, "Tarefa", DateTimeOffset.UtcNow.AddDays(1));
        db.LeadTasks.Add(task);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).DeleteTask(record.Id, task.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(await db.LeadTasks.ToListAsync());
    }

    [Fact]
    public async Task DeleteTask_returns_404_for_missing_task()
    {
        await using var db = NewContext();
        var record = NewRecord();
        db.Leads.Add(record);
        await db.SaveChangesAsync();

        var result = await new LeadsController(db).DeleteTask(record.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
