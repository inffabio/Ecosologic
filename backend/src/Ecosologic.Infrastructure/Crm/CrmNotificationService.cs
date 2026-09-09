using Ecosologic.Domain.Crm;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Infrastructure.Crm;

public sealed class CrmNotificationService(
    EcosologicDbContext db,
    TimeZoneInfo? timeZone = null,
    TimeProvider? timeProvider = null) : ICrmNotificationSync
{
    private const string DefaultTimeZoneId = "America/Sao_Paulo";
    private const int MaxConflictRetries = 1;

    private readonly TimeZoneInfo _timeZone =
        timeZone ?? TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<CrmNotificationPage> ListAsync(
        bool unreadOnly,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        await SyncAsync(cancellationToken);

        var query = db.CrmNotifications.AsNoTracking().AsQueryable();
        if (unreadOnly)
            query = query.Where(notification => notification.ReadAt == null);

        if (cursor is not null)
        {
            var (dueAt, id) = CrmNotificationCursor.Parse(cursor);
            query = query.Where(notification =>
                notification.DueAt > dueAt ||
                (notification.DueAt == dueAt && notification.Id.CompareTo(id) > 0));
        }

        var items = await query
            .OrderBy(notification => notification.DueAt)
            .ThenBy(notification => notification.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;
        if (hasMore)
            items = items.Take(limit).ToList();

        return new CrmNotificationPage(items, hasMore ? CrmNotificationCursor.Create(items[^1]) : null);
    }

    public async Task<int> CountUnreadAsync(CancellationToken cancellationToken)
    {
        await SyncAsync(cancellationToken);
        return await db.CrmNotifications.CountAsync(notification => notification.ReadAt == null, cancellationToken);
    }

    public Task SyncAsync(CancellationToken cancellationToken) => SyncCoreAsync(cancellationToken, 0);

    private async Task SyncCoreAsync(CancellationToken cancellationToken, int attempt)
    {
        var now = _timeProvider.GetUtcNow();
        var localNow = TimeZoneInfo.ConvertTime(now, _timeZone);
        var startOfToday = LocalToUtc(localNow.Date);
        var endOfToday = LocalToUtc(localNow.Date.AddDays(1));

        var tasks = await db.LeadTasks.AsNoTracking()
            .Where(task => task.CompletedAt == null)
            .Select(task => new { task.Id, task.LeadId, task.Title, task.DueAt })
            .ToListAsync(cancellationToken);
        tasks = tasks.Where(task => task.DueAt < endOfToday).ToList();

        var existingByTaskId = new Dictionary<Guid, CrmNotificationRecord>();
        if (tasks.Count > 0)
        {
            var taskIds = tasks.Select(task => task.Id).ToList();
            var existing = await db.CrmNotifications
                .Where(notification => notification.TaskId != null && taskIds.Contains(notification.TaskId!.Value))
                .ToListAsync(cancellationToken);
            foreach (var notification in existing)
                RegisterExisting(existingByTaskId, notification.TaskId!.Value, notification);
        }

        foreach (var task in tasks)
        {
            var overdue = task.DueAt < startOfToday;
            var kind = overdue ? CrmNotificationKind.TaskOverdue : CrmNotificationKind.TaskDueToday;
            var title = overdue ? $"Tarefa vencida: {task.Title}" : $"Tarefa vence hoje: {task.Title}";
            Upsert(kind, task.LeadId, task.Id, title, task.DueAt, existingByTaskId);
        }

        var leads = await db.Leads.AsNoTracking()
            .Where(lead => lead.ReminderAt != null)
            .Select(lead => new { lead.Id, lead.Name, lead.ReminderAt })
            .ToListAsync(cancellationToken);
        leads = leads.Where(lead => lead.ReminderAt!.Value < endOfToday).ToList();

        var existingByLeadId = new Dictionary<Guid, CrmNotificationRecord>();
        if (leads.Count > 0)
        {
            var leadIds = leads.Select(lead => lead.Id).ToList();
            var existing = await db.CrmNotifications
                .Where(notification => notification.TaskId == null && leadIds.Contains(notification.LeadId))
                .ToListAsync(cancellationToken);
            foreach (var notification in existing)
                RegisterExisting(existingByLeadId, notification.LeadId, notification);
        }

        foreach (var lead in leads)
        {
            Upsert(
                CrmNotificationKind.LeadReminder,
                lead.Id,
                null,
                $"Lembrete: {lead.Name}",
                lead.ReminderAt!.Value,
                existingByLeadId);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (attempt < MaxConflictRetries)
        {
            db.ChangeTracker.Clear();
            await SyncCoreAsync(cancellationToken, attempt + 1);
        }
    }

    private void RegisterExisting(Dictionary<Guid, CrmNotificationRecord> map, Guid origin, CrmNotificationRecord notification)
    {
        if (!map.TryGetValue(origin, out var kept))
        {
            map[origin] = notification;
            return;
        }

        if (kept.ReadAt is not null && notification.ReadAt is null)
        {
            db.CrmNotifications.Remove(kept);
            map[origin] = notification;
        }
        else
        {
            db.CrmNotifications.Remove(notification);
        }
    }

    private void Upsert(
        CrmNotificationKind kind,
        Guid leadId,
        Guid? taskId,
        string title,
        DateTimeOffset dueAt,
        Dictionary<Guid, CrmNotificationRecord> existing)
    {
        var origin = taskId ?? leadId;
        var dueDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(dueAt, _timeZone).Date);

        if (existing.TryGetValue(origin, out var notification))
        {
            notification.Kind = kind;
            notification.Title = title;
            notification.DueAt = dueAt.ToUniversalTime();
            notification.DueDay = dueDay;
            return;
        }

        db.CrmNotifications.Add(new CrmNotificationRecord
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            LeadId = leadId,
            TaskId = taskId,
            Title = title,
            DueAt = dueAt.ToUniversalTime(),
            DueDay = dueDay,
            CreatedAt = _timeProvider.GetUtcNow()
        });
    }

    private DateTimeOffset LocalToUtc(DateTime local)
    {
        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _timeZone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
