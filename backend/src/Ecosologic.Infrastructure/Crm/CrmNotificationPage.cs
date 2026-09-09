using Ecosologic.Infrastructure.Persistence;

namespace Ecosologic.Infrastructure.Crm;

public sealed record CrmNotificationPage(
    IReadOnlyList<CrmNotificationRecord> Items,
    string? NextCursor);
