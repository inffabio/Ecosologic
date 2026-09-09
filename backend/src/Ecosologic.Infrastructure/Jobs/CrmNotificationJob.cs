using Ecosologic.Infrastructure.Crm;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace Ecosologic.Infrastructure.Jobs;

public sealed class CrmNotificationJob(IServiceScopeFactory scopeFactory)
{
    public const int MaxRetryAttempts = 3;

    [AutomaticRetry(Attempts = MaxRetryAttempts, DelaysInSeconds = new[] { 30, 60, 120 })]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sync = scope.ServiceProvider.GetRequiredService<ICrmNotificationSync>();
        await sync.SyncAsync(cancellationToken);
    }
}
