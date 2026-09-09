namespace Ecosologic.Infrastructure.Persistence;

/// <summary>
/// Executa uma migração de banco com retry e backoff exponencial para absorver
/// indisponibilidade temporária do banco durante o startup do host.
/// </summary>
public sealed class DatabaseMigrator
{
    public const int DefaultMaxAttempts = 5;
    public static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan MaxBaseDelay = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public DatabaseMigrator(Func<TimeSpan, CancellationToken, Task>? delay = null) =>
        _delay = delay ?? Task.Delay;

    public async Task MigrateAsync(
        Func<CancellationToken, Task> migrate,
        int maxAttempts = DefaultMaxAttempts,
        TimeSpan? baseDelay = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migrate);
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        var delay = baseDelay ?? DefaultBaseDelay;
        if (delay <= TimeSpan.Zero || delay > MaxBaseDelay)
            throw new ArgumentOutOfRangeException(nameof(baseDelay), delay, "baseDelay deve ser positivo e no máximo " + MaxBaseDelay + ".");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await migrate(cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception) when (attempt < maxAttempts)
            {
                await _delay(Backoff(delay, attempt), cancellationToken);
            }
        }
    }

    private static TimeSpan Backoff(TimeSpan baseDelay, int attempt)
    {
        var multiplier = Math.Pow(2, attempt - 1);
        var totalMilliseconds = baseDelay.TotalMilliseconds * multiplier;
        return TimeSpan.FromMilliseconds(Math.Min(totalMilliseconds, MaxBackoff.TotalMilliseconds));
    }
}
