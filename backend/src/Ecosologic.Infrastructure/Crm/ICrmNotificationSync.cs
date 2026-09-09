namespace Ecosologic.Infrastructure.Crm;

public interface ICrmNotificationSync
{
    Task SyncAsync(CancellationToken cancellationToken);
}
