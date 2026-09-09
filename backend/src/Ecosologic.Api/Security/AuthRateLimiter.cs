using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ecosologic.Api.Security;

public static class AuthRateLimiter
{
    public const string PolicyName = "login";
    public const int PermitLimit = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static FixedWindowRateLimiterOptions LoginOptions() =>
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
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => LoginOptions()));
}
