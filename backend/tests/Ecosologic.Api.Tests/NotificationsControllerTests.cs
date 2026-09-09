using Ecosologic.Api.Controllers;
using Ecosologic.Domain.Crm;
using Ecosologic.Infrastructure.Crm;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Tests;

public class NotificationsControllerTests : IAsyncLifetime
{
    private const string SaoPaulo = "America/Sao_Paulo";

    private static readonly List<SqliteConnection> Connections = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        foreach (var connection in Connections)
            connection.Dispose();
        Connections.Clear();
        return Task.CompletedTask;
    }

    private static EcosologicDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EcosologicDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EcosologicDbContext(options);
    }

    private static EcosologicDbContext NewSqliteContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        connection.Open();
        Connections.Add(connection);
        var options = new DbContextOptionsBuilder<EcosologicDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new EcosologicDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static (SqliteConnection Connection, EcosologicDbContext Db) NewFileContext(string path)
    {
        var connection = new SqliteConnection($"Data Source={path};Default Timeout=10;Foreign Keys=False;Pooling=False");
        connection.Open();
        var options = new DbContextOptionsBuilder<EcosologicDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new EcosologicDbContext(options);
        db.Database.EnsureCreated();
        return (connection, db);
    }

    private static NotificationsController NewController(EcosologicDbContext db, TimeZoneInfo? timeZone = null, TimeProvider? timeProvider = null) =>
        new(db, new CrmNotificationService(db, timeZone, timeProvider));

    private static LeadRecord NewLead(DateTimeOffset? reminderAt = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Fabio",
        Phone = "+5521995424027",
        Email = "fabio@ecosologic.com.br",
        Message = "Orçamento",
        Stage = LeadStage.New,
        CreatedAt = DateTimeOffset.UtcNow,
        ReminderAt = reminderAt
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

    private static CrmNotificationRecord NewNotification(CrmNotificationKind kind, Guid leadId, Guid? taskId, string title, DateTimeOffset dueAt, DateTimeOffset? readAt = null) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        LeadId = leadId,
        TaskId = taskId,
        Title = title,
        DueAt = dueAt,
        ReadAt = readAt,
        CreatedAt = DateTimeOffset.UtcNow,
        DueDay = DateOnly.FromDateTime(dueAt.UtcDateTime)
    };

    private static async Task<CrmNotificationPage> PageAsync(EcosologicDbContext db, bool unreadOnly = false, int limit = 100, string? cursor = null)
    {
        var result = await NewController(db).List(unreadOnly, limit, cursor, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<CrmNotificationPage>(ok.Value);
    }

    private static async Task<IReadOnlyList<CrmNotificationRecord>> ListAsync(EcosologicDbContext db, bool unreadOnly = false, int limit = 100, string? cursor = null)
        => (await PageAsync(db, unreadOnly, limit, cursor)).Items;

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    [Fact]
    public void Notifications_endpoints_require_admin_role()
    {
        var attributes = typeof(NotificationsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);
        var authorize = Assert.Single(attributes);
        Assert.Equal("Admin", ((AuthorizeAttribute)authorize).Roles);
    }

    [Fact]
    public async Task List_generates_overdue_notification_for_past_due_task()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        db.LeadTasks.Add(NewTask(lead.Id, "Ligar para cliente", DateTimeOffset.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();

        var items = await ListAsync(db);

        var notification = Assert.Single(items);
        Assert.Equal(CrmNotificationKind.TaskOverdue, notification.Kind);
        Assert.Equal(lead.Id, notification.LeadId);
        Assert.Contains("Ligar para cliente", notification.Title);
    }

    [Fact]
    public async Task List_generates_due_today_notification_for_task_due_today()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        db.LeadTasks.Add(NewTask(lead.Id, "Enviar proposta", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var items = await ListAsync(db);

        var notification = Assert.Single(items);
        Assert.Equal(CrmNotificationKind.TaskDueToday, notification.Kind);
        Assert.Contains("Enviar proposta", notification.Title);
    }

    [Fact]
    public async Task List_generates_lead_reminder_notification()
    {
        await using var db = NewContext();
        var lead = NewLead(DateTimeOffset.UtcNow.AddHours(-2));
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        var items = await ListAsync(db);

        var notification = Assert.Single(items);
        Assert.Equal(CrmNotificationKind.LeadReminder, notification.Kind);
        Assert.Equal(lead.Id, notification.LeadId);
        Assert.Null(notification.TaskId);
    }

    [Fact]
    public async Task List_does_not_generate_for_future_task_or_reminder()
    {
        await using var db = NewContext();
        var lead = NewLead(DateTimeOffset.UtcNow.AddDays(2));
        db.Leads.Add(lead);
        db.LeadTasks.Add(NewTask(lead.Id, "Tarefa futura", DateTimeOffset.UtcNow.AddDays(3)));
        await db.SaveChangesAsync();

        var items = await ListAsync(db);

        Assert.Empty(items);
    }

    [Fact]
    public async Task List_does_not_generate_for_completed_task()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        db.LeadTasks.Add(NewTask(lead.Id, "Concluída", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var items = await ListAsync(db);

        Assert.Empty(items);
    }

    [Fact]
    public async Task List_does_not_create_duplicates_on_repeated_sync()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        db.LeadTasks.Add(NewTask(lead.Id, "Ligar para cliente", DateTimeOffset.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();

        _ = await ListAsync(db);
        var items = await ListAsync(db);

        Assert.Single(items);
    }

    [Fact]
    public async Task List_does_not_delete_read_notifications_when_source_changes()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        var task = NewTask(lead.Id, "Ligar para cliente", DateTimeOffset.UtcNow.AddDays(-1));
        db.LeadTasks.Add(task);
        await db.SaveChangesAsync();

        var items = await ListAsync(db);
        var id = Assert.Single(items).Id;

        var markResult = await NewController(db).MarkRead(id, CancellationToken.None);
        Assert.IsType<NoContentResult>(markResult);

        task.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var after = await ListAsync(db);
        var notification = Assert.Single(after);
        Assert.Equal(id, notification.Id);
        Assert.NotNull(notification.ReadAt);
    }

    [Fact]
    public async Task List_reuses_notification_when_task_moves_from_due_today_to_overdue()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        var task = NewTask(lead.Id, "Enviar proposta", DateTimeOffset.UtcNow);
        db.LeadTasks.Add(task);
        await db.SaveChangesAsync();

        var dueToday = await ListAsync(db);
        var id = Assert.Single(dueToday).Id;
        Assert.Equal(CrmNotificationKind.TaskDueToday, dueToday[0].Kind);

        task.DueAt = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var after = await ListAsync(db);
        var notification = Assert.Single(after);
        Assert.Equal(id, notification.Id);
        Assert.Equal(CrmNotificationKind.TaskOverdue, notification.Kind);
        Assert.Contains("Tarefa vencida", notification.Title);
    }

    [Fact]
    public async Task List_syncs_existing_notification_when_title_changes()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        var task = NewTask(lead.Id, "Ligar", DateTimeOffset.UtcNow.AddDays(-1));
        db.LeadTasks.Add(task);
        await db.SaveChangesAsync();

        var first = await ListAsync(db);
        var id = Assert.Single(first).Id;

        task.Title = "Ligar para o cliente";
        await db.SaveChangesAsync();

        var after = await ListAsync(db);
        var notification = Assert.Single(after);
        Assert.Equal(id, notification.Id);
        Assert.Contains("Ligar para o cliente", notification.Title);
    }

    [Fact]
    public async Task List_uses_configured_timezone_for_local_day()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        var now = new DateTimeOffset(2026, 9, 2, 1, 0, 0, TimeSpan.Zero);
        var dueAt = new DateTimeOffset(2026, 9, 1, 4, 0, 0, TimeSpan.Zero);
        db.LeadTasks.Add(NewTask(lead.Id, "Tarefa", dueAt));
        await db.SaveChangesAsync();

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(SaoPaulo);
        var controller = NewController(db, timeZone, new FixedTimeProvider(now));
        var result = await controller.List(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<CrmNotificationPage>(ok.Value);
        var notification = Assert.Single(page.Items);
        Assert.Equal(CrmNotificationKind.TaskDueToday, notification.Kind);
        Assert.Equal(new DateOnly(2026, 9, 1), notification.DueDay);
        Assert.Equal(TimeSpan.Zero, notification.DueAt.Offset);
    }

    [Fact]
    public async Task List_orders_by_due_at_ascending_then_id()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        var now = DateTimeOffset.UtcNow;
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "A", now.AddDays(-2)));
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "B", now.AddDays(-1), now));
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.LeadReminder, lead.Id, null, "C", now.AddDays(-3)));
        await db.SaveChangesAsync();

        var items = await ListAsync(db);

        Assert.Equal(new[] { "C", "A", "B" }, items.Select(n => n.Title));
    }

    [Fact]
    public async Task List_respects_unread_only_filter()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        var now = DateTimeOffset.UtcNow;
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "Unread", now.AddDays(-2)));
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "Read", now.AddDays(-1), now));
        await db.SaveChangesAsync();

        var items = await ListAsync(db, unreadOnly: true);

        Assert.Equal(["Unread"], items.Select(n => n.Title));
    }

    [Fact]
    public async Task List_caps_limit_and_paginates_with_stable_cursor()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        var now = DateTimeOffset.UtcNow;
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "A", now.AddDays(-3)));
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "B", now.AddDays(-2)));
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "C", now.AddDays(-1)));
        await db.SaveChangesAsync();

        var first = await PageAsync(db, limit: 2);
        Assert.Equal(new[] { "A", "B" }, first.Items.Select(n => n.Title));
        Assert.NotNull(first.NextCursor);

        var second = await PageAsync(db, limit: 2, cursor: first.NextCursor);
        Assert.Equal(new[] { "C" }, second.Items.Select(n => n.Title));
        Assert.Null(second.NextCursor);
    }

    [Theory]
    [InlineData("not-a-cursor")]
    [InlineData("!!!!")]
    [InlineData("")]
    [InlineData("QXxC")]
    public async Task List_returns_400_for_malformed_cursor(string cursor)
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        var result = await NewController(db).List(cursor: cursor, cancellationToken: CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Contains("cursor", details.Errors.Keys);
    }

    [Fact]
    public async Task List_cursor_pagination_has_no_duplicates_or_gaps_after_mark_read()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        var now = DateTimeOffset.UtcNow;
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "A", now.AddDays(-4)));
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "B", now.AddDays(-3)));
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "C", now.AddDays(-2)));
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "D", now.AddDays(-1)));
        await db.SaveChangesAsync();

        var first = await PageAsync(db, limit: 2);
        Assert.Equal(new[] { "A", "B" }, first.Items.Select(n => n.Title));

        var markResult = await NewController(db).MarkRead(first.Items[0].Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(markResult);

        var second = await PageAsync(db, limit: 2, cursor: first.NextCursor);
        Assert.Equal(new[] { "C", "D" }, second.Items.Select(n => n.Title));

        var combined = first.Items.Concat(second.Items).Select(n => n.Id).ToList();
        Assert.Equal(combined.Distinct().Count(), combined.Count);
        Assert.Equal(4, combined.Count);
    }

    [Fact]
    public async Task UnreadCount_returns_total_unread_beyond_list_limit()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 105; i++)
        {
            db.CrmNotifications.Add(NewNotification(
                CrmNotificationKind.TaskOverdue,
                lead.Id,
                Guid.NewGuid(),
                $"Notificação {i}",
                now.AddDays(-1)));
        }
        await db.SaveChangesAsync();

        var result = await NewController(db).UnreadCount(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UnreadCountResponse>(ok.Value);
        Assert.Equal(105, response.Count);
    }

    [Fact]
    public async Task MarkRead_returns_204_and_sets_read_at()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        var notification = NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "A", DateTimeOffset.UtcNow.AddDays(-1));
        db.CrmNotifications.Add(notification);
        await db.SaveChangesAsync();

        var result = await NewController(db).MarkRead(notification.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var stored = await db.CrmNotifications.SingleAsync(n => n.Id == notification.Id);
        Assert.NotNull(stored.ReadAt);
    }

    [Fact]
    public async Task MarkRead_is_idempotent()
    {
        await using var db = NewContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        var readAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notification = NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "A", DateTimeOffset.UtcNow.AddDays(-1), readAt);
        db.CrmNotifications.Add(notification);
        await db.SaveChangesAsync();

        var result = await NewController(db).MarkRead(notification.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var stored = await db.CrmNotifications.SingleAsync(n => n.Id == notification.Id);
        Assert.Equal(readAt, stored.ReadAt);
    }

    [Fact]
    public async Task MarkRead_returns_404_for_missing_notification()
    {
        await using var db = NewContext();

        var result = await NewController(db).MarkRead(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task MarkAllRead_marks_all_unread_as_read()
    {
        await using var db = NewSqliteContext();
        var lead = NewLead();
        db.Leads.Add(lead);
        var now = DateTimeOffset.UtcNow;
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "A", now.AddDays(-2)));
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.TaskOverdue, lead.Id, Guid.NewGuid(), "B", now.AddDays(-1)));
        db.CrmNotifications.Add(NewNotification(CrmNotificationKind.LeadReminder, lead.Id, null, "C", now.AddDays(-1), now));
        await db.SaveChangesAsync();

        var result = await NewController(db).MarkAllRead(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        db.ChangeTracker.Clear();
        Assert.All(await db.CrmNotifications.AsNoTracking().ToListAsync(), n => Assert.NotNull(n.ReadAt));
    }

    [Fact]
    public async Task Sync_is_idempotent_under_concurrent_calls()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ecosologic-{Guid.NewGuid():N}.db");
        try
        {
            var lead = NewLead();
            var task = NewTask(lead.Id, "Tarefa", DateTimeOffset.UtcNow.AddDays(-1));

            var (setupConnection, setupDb) = NewFileContext(dbPath);
            setupDb.Leads.Add(lead);
            setupDb.LeadTasks.Add(task);
            await setupDb.SaveChangesAsync();
            await setupDb.DisposeAsync();
            await setupConnection.DisposeAsync();

            var (connection1, db1) = NewFileContext(dbPath);
            var (connection2, db2) = NewFileContext(dbPath);
            try
            {
                var service1 = new CrmNotificationService(db1);
                var service2 = new CrmNotificationService(db2);

                await Task.WhenAll(
                    service1.SyncAsync(CancellationToken.None),
                    service2.SyncAsync(CancellationToken.None));

                var count = await db1.CrmNotifications.CountAsync();
                Assert.Equal(1, count);
            }
            finally
            {
                await db1.DisposeAsync();
                await db2.DisposeAsync();
                await connection1.DisposeAsync();
                await connection2.DisposeAsync();
            }
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }
}
