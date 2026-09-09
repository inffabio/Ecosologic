using System.Reflection;
using Ecosologic.Domain.Crm;
using Ecosologic.Infrastructure.Crm;
using Ecosologic.Infrastructure.Jobs;
using Ecosologic.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecosologic.Api.Tests;

public class CrmNotificationJobTests
{
    private sealed class FakeSyncService : ICrmNotificationSync
    {
        public int Count;
        public Func<CancellationToken, Task> OnSync = _ => Task.CompletedTask;

        public Task SyncAsync(CancellationToken cancellationToken)
        {
            Count++;
            return OnSync(cancellationToken);
        }
    }

    private static CrmNotificationJob CreateJob(ICrmNotificationSync sync)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => sync);
        var provider = services.BuildServiceProvider();
        return new CrmNotificationJob(provider.GetRequiredService<IServiceScopeFactory>());
    }

    [Fact]
    public async Task Execute_resolves_service_and_syncs_once()
    {
        var sync = new FakeSyncService();
        var job = CreateJob(sync);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(1, sync.Count);
    }

    [Fact]
    public async Task Execute_passes_the_cancellation_token_to_sync()
    {
        using var cts = new CancellationTokenSource();
        var received = CancellationToken.None;
        var sync = new FakeSyncService
        {
            OnSync = token =>
            {
                received = token;
                return Task.CompletedTask;
            }
        };
        var job = CreateJob(sync);

        await job.ExecuteAsync(cts.Token);

        Assert.Equal(cts.Token, received);
    }

    [Fact]
    public async Task Execute_propagates_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sync = new FakeSyncService
        {
            OnSync = token => Task.FromCanceled(token)
        };
        var job = CreateJob(sync);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.ExecuteAsync(cts.Token));
    }

    [Fact]
    public async Task Execute_does_not_swallow_exceptions_so_hangfire_can_retry()
    {
        var sync = new FakeSyncService
        {
            OnSync = _ => throw new InvalidOperationException("boom")
        };
        var job = CreateJob(sync);

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.ExecuteAsync(CancellationToken.None));
    }

    [Fact]
    public void Execute_declares_automatic_retry_policy()
    {
        var retry = typeof(CrmNotificationJob)
            .GetMethod(nameof(CrmNotificationJob.ExecuteAsync))!
            .GetCustomAttribute<AutomaticRetryAttribute>();

        Assert.NotNull(retry);
        Assert.Equal(CrmNotificationJob.MaxRetryAttempts, retry.Attempts);
        Assert.Equal(new[] { 30, 60, 120 }, retry.DelaysInSeconds);
    }

    [Fact]
    public async Task Resolves_scoped_service_in_a_new_scope_per_execution()
    {
        var created = 0;
        var services = new ServiceCollection();
        services.AddScoped<ICrmNotificationSync>(_ =>
        {
            Interlocked.Increment(ref created);
            return new FakeSyncService();
        });
        var provider = services.BuildServiceProvider();
        var job = new CrmNotificationJob(provider.GetRequiredService<IServiceScopeFactory>());

        await job.ExecuteAsync(CancellationToken.None);
        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(2, created);
    }

    [Fact]
    public async Task Creates_notification_from_real_service_without_duplicates()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ecosologic-notifications-{Guid.NewGuid():N}.db");
        try
        {
            var connectionString = $"Data Source={dbPath};Default Timeout=10;Foreign Keys=False;Pooling=False";
            var options = new DbContextOptionsBuilder<EcosologicDbContext>()
                .UseSqlite(connectionString)
                .Options;

            var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
            await using (var seed = new EcosologicDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                seed.Leads.Add(new LeadRecord
                {
                    Id = Guid.NewGuid(),
                    Name = "Fabio",
                    Phone = "+5521995424027",
                    Email = "fabio@ecosologic.com.br",
                    Message = "Orçamento",
                    Stage = LeadStage.New,
                    CreatedAt = now,
                    ReminderAt = now.AddHours(-1)
                });
                await seed.SaveChangesAsync();
            }

            var services = new ServiceCollection();
            services.AddDbContext<EcosologicDbContext>(o => o.UseSqlite(connectionString));
            services.AddSingleton(TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
            services.AddScoped<ICrmNotificationSync, CrmNotificationService>();
            var provider = services.BuildServiceProvider();

            var job = new CrmNotificationJob(provider.GetRequiredService<IServiceScopeFactory>());

            await job.ExecuteAsync(CancellationToken.None);
            await job.ExecuteAsync(CancellationToken.None);

            await using var db = new EcosologicDbContext(options);
            Assert.Equal(1, await db.CrmNotifications.CountAsync());
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }
}
