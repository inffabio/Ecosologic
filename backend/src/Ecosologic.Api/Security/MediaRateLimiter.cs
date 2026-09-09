using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ecosologic.Api.Security;

public static class MediaRateLimiter
{
    public const string PolicyName = "upload";
    public const int PermitLimit = 20;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static FixedWindowRateLimiterOptions UploadOptions() =>
        new()
        {
            PermitLimit = PermitLimit,
            Window = Window,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        };

    public static void Configure(RateLimiterOptions options) =>
        options.AddPolicy(PolicyName, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(context),
                _ => UploadOptions()));

    public static string PartitionKey(HttpContext context)
    {
        var userName = context.User?.Identity?.Name;
        return string.IsNullOrWhiteSpace(userName)
            ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            : userName;
    }
}
