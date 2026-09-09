using System.Reflection;
using Ecosologic.Application.Solar;
using Ecosologic.Infrastructure.Jobs;
using Ecosologic.Infrastructure.Solar;
using Hangfire;
using Hangfire.Common;
using Hangfire.MemoryStorage;
using Hangfire.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecosologic.Api.Tests;

public class AneelTariffSyncJobTests
{
    private sealed class FakeImportService : IAneelTariffImportService
    {
        public int ImportCalls;
        public Guid? ReservedRunId;
        public string? JobId;
        public AneelTariffSyncOptions? Options;
        public Func<CancellationToken, Task> OnImport = _ => Task.CompletedTask;

        public Task<Guid> ImportAsync(AneelTariffSyncOptions options, CancellationToken cancellationToken = default)
        {
            ImportCalls++;
            Options = options;
            return Run(cancellationToken);
        }

        public Task<Guid> ImportAsync(AneelTariffSyncOptions options, Guid runId, string? jobId, CancellationToken cancellationToken = default)
        {
            ImportCalls++;
            ReservedRunId = runId;
            JobId = jobId;
            Options = options;
            return Run(cancellationToken);
        }

        public Task<Guid> ReserveRunAsync(AneelTariffSyncOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());

        public Task AttachJobIdAsync(Guid runId, string jobId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task FailRunAsync(Guid runId, string errorMessage, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        private async Task<Guid> Run(CancellationToken cancellationToken)
        {
            await OnImport(cancellationToken);
            return Guid.NewGuid();
        }
    }

    private static AneelTariffSyncJob CreateJob(IAneelTariffImportService import, IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(import);
        services.AddSingleton(configuration ?? Config());
        var provider = services.BuildServiceProvider();
        return new AneelTariffSyncJob(provider.GetRequiredService<IServiceScopeFactory>());
    }

    private static IConfiguration Config(string sourceUrl = "https://www.aneel.gov.br/relatorio-tarifas")
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Tariffs:SourceUrl"] = sourceUrl })
            .Build();

    private sealed class FakeJobCancellationToken : IJobCancellationToken
    {
        public CancellationToken ShutdownToken => CancellationToken.None;

        public void ThrowIfCancellationRequested()
        {
        }
    }

    private static PerformContext NewContext(string jobId)
    {
        var storage = new MemoryStorage();
        var connection = storage.GetConnection();
        var method = typeof(AneelTariffSyncJob).GetMethod(
            nameof(AneelTariffSyncJob.ExecuteAsync),
            new[] { typeof(PerformContext), typeof(CancellationToken) })!;
        var job = new Job(typeof(AneelTariffSyncJob), method, new object[method.GetParameters().Length]);
        return new PerformContext(storage, connection, new BackgroundJob(jobId, job, DateTime.UtcNow), new FakeJobCancellationToken());
    }

    [Fact]
    public void DeriveRunId_is_deterministic()
    {
        Assert.Equal(AneelTariffSyncJob.DeriveRunId("12345"), AneelTariffSyncJob.DeriveRunId("12345"));
    }

    [Fact]
    public void DeriveRunId_differs_across_distinct_job_ids()
    {
        Assert.NotEqual(AneelTariffSyncJob.DeriveRunId("12345"), AneelTariffSyncJob.DeriveRunId("67890"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeriveRunId_rejects_blank_job_id(string? jobId)
    {
        Assert.ThrowsAny<ArgumentException>(() => AneelTariffSyncJob.DeriveRunId(jobId!));
    }

    [Fact]
    public async Task Execute_scheduled_derives_run_id_and_passes_job_id()
    {
        var import = new FakeImportService();
        var job = CreateJob(import);

        await job.ExecuteAsync(NewContext("hangfire-999"), CancellationToken.None);

        Assert.Equal(1, import.ImportCalls);
        Assert.Equal(AneelTariffSyncJob.DeriveRunId("hangfire-999"), import.ReservedRunId);
        Assert.Equal("hangfire-999", import.JobId);
        Assert.NotNull(import.Options);
        Assert.Equal("https://www.aneel.gov.br/relatorio-tarifas", import.Options!.SourceUrl);
    }

    [Fact]
    public async Task Execute_manual_uses_reserved_run_id_and_passes_job_id()
    {
        var import = new FakeImportService();
        var job = CreateJob(import);
        var runId = Guid.NewGuid();

        await job.ExecuteAsync(runId, NewContext("hangfire-888"), CancellationToken.None);

        Assert.Equal(1, import.ImportCalls);
        Assert.Equal(runId, import.ReservedRunId);
        Assert.Equal("hangfire-888", import.JobId);
    }

    [Fact]
    public async Task Execute_passes_the_cancellation_token_to_import()
    {
        using var cts = new CancellationTokenSource();
        var received = CancellationToken.None;
        var import = new FakeImportService
        {
            OnImport = token =>
            {
                received = token;
                return Task.CompletedTask;
            }
        };
        var job = CreateJob(import);

        await job.ExecuteAsync(NewContext("hangfire-777"), cts.Token);

        Assert.Equal(cts.Token, received);
    }

    [Fact]
    public async Task Execute_does_not_swallow_exceptions_so_hangfire_can_retry()
    {
        var import = new FakeImportService { OnImport = _ => throw new InvalidOperationException("boom") };
        var job = CreateJob(import);

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.ExecuteAsync(NewContext("hangfire-666"), CancellationToken.None));
    }

    [Fact]
    public void Execute_declares_distributed_lock()
    {
        var lockAttribute = ScheduledMethod()
            .GetCustomAttribute<DisableConcurrentExecutionAttribute>();

        Assert.NotNull(lockAttribute);
        Assert.True(lockAttribute.TimeoutSec > 0);
    }

    [Fact]
    public void Execute_overloads_share_the_same_distributed_lock_resource()
    {
        var scheduled = ScheduledMethod().GetCustomAttribute<DisableConcurrentExecutionAttribute>();
        var manual = ManualMethod().GetCustomAttribute<DisableConcurrentExecutionAttribute>();

        Assert.NotNull(scheduled);
        Assert.NotNull(manual);
        Assert.Equal(AneelTariffSyncJob.DistributedLockResource, scheduled!.Resource);
        Assert.Equal(scheduled.Resource, manual!.Resource);
    }

    [Fact]
    public void Execute_does_not_declare_method_level_automatic_retry()
    {
        // O retry configurável é aplicado pelo filtro global AneelTariffRetryFilter;
        // um atributo no método causaria política duplicada/conflitante.
        Assert.Null(ScheduledMethod().GetCustomAttribute<AutomaticRetryAttribute>());
        Assert.Null(ManualMethod().GetCustomAttribute<AutomaticRetryAttribute>());
    }

    [Fact]
    public void BuildOptions_uses_configured_source_url_and_timeout()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tariffs:SourceUrl"] = "https://example.com/tariffs",
                ["Tariffs:HttpTimeoutSeconds"] = "60"
            })
            .Build();

        var options = AneelTariffSyncOptionsFactory.Build(configuration);

        Assert.Equal("https://example.com/tariffs", options.SourceUrl);
        Assert.Equal(TimeSpan.FromSeconds(60), options.HttpTimeout);
    }

    [Fact]
    public void BuildOptions_falls_back_to_default_timeout()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Tariffs:SourceUrl"] = "https://example.com/tariffs" })
            .Build();

        var options = AneelTariffSyncOptionsFactory.Build(configuration);

        Assert.Equal(AneelTariffSyncOptions.DefaultHttpTimeout, options.HttpTimeout);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildOptions_rejects_blank_source_url(string? sourceUrl)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Tariffs:SourceUrl"] = sourceUrl })
            .Build();

        Assert.Throws<ArgumentException>(() => AneelTariffSyncOptionsFactory.Build(configuration));
    }

    private static MethodInfo ScheduledMethod() =>
        typeof(AneelTariffSyncJob).GetMethod(nameof(AneelTariffSyncJob.ExecuteAsync), new[] { typeof(PerformContext), typeof(CancellationToken) })!;

    private static MethodInfo ManualMethod() =>
        typeof(AneelTariffSyncJob).GetMethod(nameof(AneelTariffSyncJob.ExecuteAsync), new[] { typeof(Guid), typeof(PerformContext), typeof(CancellationToken) })!;
}
