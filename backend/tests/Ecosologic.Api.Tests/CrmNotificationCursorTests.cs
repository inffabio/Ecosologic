using Ecosologic.Domain.Crm;
using Ecosologic.Infrastructure.Crm;
using Ecosologic.Infrastructure.Persistence;

namespace Ecosologic.Api.Tests;

public class CrmNotificationCursorTests
{
    private static CrmNotificationRecord NewRecord(Guid? id = null, DateTimeOffset? dueAt = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Kind = CrmNotificationKind.TaskOverdue,
        LeadId = Guid.NewGuid(),
        TaskId = Guid.NewGuid(),
        Title = "Tarefa",
        DueAt = dueAt ?? DateTimeOffset.UtcNow,
        CreatedAt = DateTimeOffset.UtcNow,
        DueDay = DateOnly.FromDateTime(DateTime.UtcNow)
    };

    [Fact]
    public void Create_returns_null_for_null_item()
    {
        Assert.Null(CrmNotificationCursor.Create(null));
    }

    [Fact]
    public void Create_and_Parse_round_trips_due_at_and_id()
    {
        var item = NewRecord(dueAt: new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero));

        var cursor = CrmNotificationCursor.Create(item);
        Assert.NotNull(cursor);

        var (dueAt, id) = CrmNotificationCursor.Parse(cursor);
        Assert.Equal(item.DueAt, dueAt);
        Assert.Equal(item.Id, id);
    }

    [Fact]
    public void Create_produces_url_safe_cursor()
    {
        var item = NewRecord();

        var cursor = CrmNotificationCursor.Create(item)!;

        Assert.DoesNotContain("+", cursor);
        Assert.DoesNotContain("/", cursor);
        Assert.DoesNotContain("=", cursor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-cursor")]
    [InlineData("!!!!")]
    public void Parse_rejects_invalid_cursor(string cursor)
    {
        Assert.ThrowsAny<ArgumentException>(() => CrmNotificationCursor.Parse(cursor));
    }
}
