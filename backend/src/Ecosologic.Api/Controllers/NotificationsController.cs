using Ecosologic.Infrastructure.Crm;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Roles = "Admin")]
public sealed class NotificationsController(EcosologicDbContext db, CrmNotificationService notifications) : ControllerBase
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 100;

    [HttpGet]
    public async Task<IActionResult> List(
        bool unreadOnly = false,
        int limit = DefaultLimit,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, MaxLimit);

        try
        {
            var page = await notifications.ListAsync(unreadOnly, safeLimit, cursor, cancellationToken);
            return Ok(page);
        }
        catch (ArgumentException)
        {
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["cursor"] = ["Cursor inválido."] }));
        }
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken)
    {
        var count = await notifications.CountUnreadAsync(cancellationToken);
        return Ok(new UnreadCountResponse(count));
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var notification = await db.CrmNotifications.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (notification is null)
            return NotFound();

        if (notification.ReadAt is null)
        {
            notification.ReadAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await db.CrmNotifications
            .Where(item => item.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ReadAt, now), cancellationToken);
        return NoContent();
    }
}

public sealed record UnreadCountResponse(int Count);
