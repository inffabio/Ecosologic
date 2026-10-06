namespace Ecosologic.Infrastructure.Secrets;

public interface IOciSecretReader
{
    Task<string> ReadAsync(string secretName, CancellationToken cancellationToken);
}
