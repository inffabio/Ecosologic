using Ecosologic.Infrastructure.Persistence;

namespace Ecosologic.Api.Tests;

public class DatabaseMigratorTests
{
    [Fact]
    public async Task Migrate_succeeds_on_first_attempt_without_delay()
    {
        var delays = new List<TimeSpan>();
        var migrator = new DatabaseMigrator((delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });
        var calls = 0;

        await migrator.MigrateAsync(
            _ => { calls++; return Task.CompletedTask; },
            maxAttempts: 3,
            baseDelay: TimeSpan.FromSeconds(1));

        Assert.Equal(1, calls);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task Migrate_retries_with_exponential_backoff_until_success()
    {
        var delays = new List<TimeSpan>();
        var migrator = new DatabaseMigrator((delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });
        var calls = 0;

        await migrator.MigrateAsync(
            _ =>
            {
                calls++;
                if (calls < 3)
                    throw new InvalidOperationException("boom");
                return Task.CompletedTask;
            },
            maxAttempts: 5,
            baseDelay: TimeSpan.FromSeconds(1));

        Assert.Equal(3, calls);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], delays);
    }

    [Fact]
    public async Task Migrate_rethrows_last_exception_after_attempts_exhausted()
    {
        var migrator = new DatabaseMigrator((_, _) => Task.CompletedTask);
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => migrator.MigrateAsync(
            _ =>
            {
                calls++;
                throw new InvalidOperationException("boom");
            },
            maxAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(1)));

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Migrate_honors_cancellation_during_backoff()
    {
        var migrator = new DatabaseMigrator(Task.Delay);
        var calls = 0;
        using var cts = new CancellationTokenSource(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => migrator.MigrateAsync(
            _ =>
            {
                calls++;
                throw new InvalidOperationException("boom");
            },
            maxAttempts: 5,
            baseDelay: TimeSpan.FromSeconds(5),
            cancellationToken: cts.Token));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Migrate_does_not_retry_when_cancelled_during_migration()
    {
        var migrator = new DatabaseMigrator((_, _) => Task.CompletedTask);
        var calls = 0;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => migrator.MigrateAsync(
            token =>
            {
                calls++;
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            maxAttempts: 5,
            cancellationToken: cts.Token));

        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Migrate_rejects_non_positive_base_delay(double seconds)
    {
        var migrator = new DatabaseMigrator((_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => migrator.MigrateAsync(
            _ => Task.CompletedTask,
            baseDelay: TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public async Task Migrate_rejects_base_delay_above_maximum()
    {
        var migrator = new DatabaseMigrator((_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => migrator.MigrateAsync(
            _ => Task.CompletedTask,
            baseDelay: DatabaseMigrator.MaxBaseDelay + TimeSpan.FromSeconds(1)));
    }
}
