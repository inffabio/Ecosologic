using Ecosologic.Api.Controllers;
using Ecosologic.Application.Solar;
using Ecosologic.Infrastructure.Jobs;
using Ecosologic.Infrastructure.Persistence;
using Ecosologic.Infrastructure.Solar;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosologic.Api.Tests;

public class AdminTariffsControllerTests
{
    private const string SourceUrl = "https://www.aneel.gov.br/relatorio-tarifas";

    private sealed class FakeImportService : IAneelTariffImportService
    {
        public int ImportCalls;
        public int ReserveCalls;
        public Guid ReservedRunId;
        public AneelTariffSyncOptions? ReservedOptions;
        public Guid? AttachedRunId;
        public string? AttachedJobId;
        public Guid? FailedRunId;
        public string? FailedErrorMessage;
        public Func<Guid, string, Task>? OnFailRun;

        public Task<Guid> ImportAsync(AneelTariffSyncOptions options, CancellationToken cancellationToken = default)
        {
            ImportCalls++;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<Guid> ImportAsync(AneelTariffSyncOptions options, Guid runId, string? jobId, CancellationToken cancellationToken = default)
        {
            ImportCalls++;
            return Task.FromResult(runId);
        }

        public Task<Guid> ReserveRunAsync(AneelTariffSyncOptions options, CancellationToken cancellationToken = default)
        {
            ReserveCalls++;
            ReservedOptions = options;
            ReservedRunId = Guid.NewGuid();
            return Task.FromResult(ReservedRunId);
        }

        public Task AttachJobIdAsync(Guid runId, string jobId, CancellationToken cancellationToken = default)
        {
            AttachedRunId = runId;
            AttachedJobId = jobId;
            return Task.CompletedTask;
        }

        public Task FailRunAsync(Guid runId, string errorMessage, CancellationToken cancellationToken = default)
        {
            FailedRunId = runId;
            FailedErrorMessage = errorMessage;
            return OnFailRun is null
                ? Task.CompletedTask
                : OnFailRun(runId, errorMessage);
        }
    }

    private sealed class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public List<(Job Job, IState State)> Created = [];
        public Func<Job, IState, string>? OnCreate;

        public string Create(Job job, IState state)
        {
            if (OnCreate is not null)
                return OnCreate(job, state);

            Created.Add((job, state));
            return "hangfire-job-id";
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }

    private sealed class FakeAneelSource(
        Func<AneelTariffSyncOptions, CancellationToken, Task<AneelTariffFetchResult>> fetch) : IAneelTariffSource
    {
        public Task<AneelTariffFetchResult> FetchAsync(AneelTariffSyncOptions options, CancellationToken cancellationToken = default)
            => fetch(options, cancellationToken);
    }

    private static IConfiguration Config()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Tariffs:SourceUrl"] = SourceUrl })
            .Build();

    private static EcosologicDbContext NewSqliteContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var db = new EcosologicDbContext(
            new DbContextOptionsBuilder<EcosologicDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static AdminTariffsController NewController(
        EcosologicDbContext db,
        IAneelTariffImportService import,
        IBackgroundJobClient backgroundJobClient,
        IConfiguration? configuration = null)
        => new(import, backgroundJobClient, configuration ?? Config(), db, NullLogger<AdminTariffsController>.Instance);

    private static IReadOnlyList<AneelTariffRecord> FullCoverage(string start, string? end) =>
        [.. from distributor in new[] { "Light", "Enel RJ" }
           from subgroup in new[] { "B1", "B2", "B3" }
           select new AneelTariffRecord(distributor, "B", subgroup, "Convencional", "Único", "TUSD Fio B", "R$/MWh", "41.20", "RES 3.242/2024", start, end, null)];

    [Fact]
    public void Sync_endpoints_require_admin_role()
    {
        var attributes = typeof(AdminTariffsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);
        var authorize = Assert.Single(attributes);
        Assert.Equal("Admin", ((AuthorizeAttribute)authorize).Roles);
    }

    [Fact]
    public async Task Sync_reserves_run_and_enqueues_job_returning_202_without_synchronous_import()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;
        var import = new FakeImportService();
        var client = new FakeBackgroundJobClient();

        var result = await NewController(db, import, client).Sync(CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.Equal(202, accepted.StatusCode);
        var response = Assert.IsType<TariffSyncAcceptedResponse>(accepted.Value);
        Assert.Equal(import.ReservedRunId, response.RunId);

        // A requisição manual não executa a importação de forma síncrona: apenas
        // reserva a execução e enfileira o job.
        Assert.Equal(1, import.ReserveCalls);
        Assert.Equal(0, import.ImportCalls);
        Assert.Equal(SourceUrl, import.ReservedOptions!.SourceUrl);

        var (job, state) = Assert.Single(client.Created);
        Assert.IsType<EnqueuedState>(state);
        Assert.Equal(typeof(AneelTariffSyncJob), job.Type);
        Assert.Equal(nameof(AneelTariffSyncJob.ExecuteAsync), job.Method.Name);
        Assert.Equal(3, job.Args.Count);
        Assert.Equal(import.ReservedRunId, Assert.IsType<Guid>(job.Args[0]));
        Assert.Null(job.Args[1]);
    }

    [Fact]
    public async Task Sync_attaches_hangfire_job_id_after_enqueue()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;
        var import = new FakeImportService();
        var client = new FakeBackgroundJobClient();

        await NewController(db, import, client).Sync(CancellationToken.None);

        Assert.Equal(import.ReservedRunId, import.AttachedRunId);
        Assert.Equal("hangfire-job-id", import.AttachedJobId);
    }

    [Fact]
    public async Task Sync_marks_reserved_run_failed_when_enqueue_throws()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;
        var import = new FakeImportService();
        var client = new FakeBackgroundJobClient
        {
            OnCreate = (_, _) => throw new InvalidOperationException("storage indisponível")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewController(db, import, client).Sync(CancellationToken.None));

        Assert.Equal(import.ReservedRunId, import.FailedRunId);
        Assert.Equal("HANGFIRE_ENQUEUE_FAILED", import.FailedErrorMessage);
        Assert.Equal(0, import.ImportCalls);
        Assert.Empty(client.Created);
    }

    [Fact]
    public async Task Sync_marks_reserved_run_failed_when_enqueue_is_cancelled()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;
        var import = new FakeImportService();
        var client = new FakeBackgroundJobClient
        {
            OnCreate = (_, _) => throw new OperationCanceledException()
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => NewController(db, import, client).Sync(CancellationToken.None));

        Assert.Equal(import.ReservedRunId, import.FailedRunId);
        Assert.Equal("HANGFIRE_ENQUEUE_CANCELLED", import.FailedErrorMessage);
    }

    [Fact]
    public async Task Sync_preserves_original_enqueue_failure_when_fail_run_also_fails()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;
        var import = new FakeImportService
        {
            OnFailRun = (_, _) => throw new InvalidOperationException("falha ao marcar Failed")
        };
        var client = new FakeBackgroundJobClient
        {
            OnCreate = (_, _) => throw new InvalidOperationException("storage indisponível")
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewController(db, import, client).Sync(CancellationToken.None));

        Assert.Equal("storage indisponível", exception.Message);
        Assert.Equal(import.ReservedRunId, import.FailedRunId);
        Assert.Equal("HANGFIRE_ENQUEUE_FAILED", import.FailedErrorMessage);
    }

    [Fact]
    public async Task Status_returns_latest_run_when_exists()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;
        var runId = Guid.NewGuid();
        db.AneelTariffImports.Add(AneelTariffImportRecord.Create(runId, null, "{}", SourceUrl));
        await db.SaveChangesAsync();

        var result = await NewController(db, new FakeImportService(), new FakeBackgroundJobClient()).Status(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TariffSyncStatusResponse>(ok.Value);
        Assert.Equal(runId, response.RunId);
        Assert.Equal("Running", response.Status);
        Assert.Equal(SourceUrl, response.SourceUrl);
        Assert.Equal(0, response.RawRecordCount);
        Assert.Null(response.ErrorMessage);
    }

    [Fact]
    public async Task Status_returns_latest_run_ordered_by_started_at()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;
        db.AneelTariffImports.Add(AneelTariffImportRecord.Create(Guid.NewGuid(), null, "{}", SourceUrl));
        await db.SaveChangesAsync();

        await Task.Delay(10);

        var latest = AneelTariffImportRecord.Create(Guid.NewGuid(), null, "{}", SourceUrl);
        db.AneelTariffImports.Add(latest);
        await db.SaveChangesAsync();

        var result = await NewController(db, new FakeImportService(), new FakeBackgroundJobClient()).Status(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TariffSyncStatusResponse>(ok.Value);
        Assert.Equal(latest.Id, response.RunId);
    }

    [Fact]
    public async Task Status_returns_404_when_no_run_exists()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var result = await NewController(db, new FakeImportService(), new FakeBackgroundJobClient()).Status(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Status_reports_succeeded_counters_after_import()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var source = new FakeAneelSource((_, _) => Task.FromResult(
            new AneelTariffFetchResult(FullCoverage("2024-01-01", "2024-12-31"), new string('1', 64), SourceUrl, DateTimeOffset.UtcNow)));
        var service = new AneelTariffImportService(db, source, new AneelTariffNormalizer(), new TariffCatalog(db));

        await service.ImportAsync(new AneelTariffSyncOptions(SourceUrl));

        var result = await NewController(db, new FakeImportService(), new FakeBackgroundJobClient()).Status(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TariffSyncStatusResponse>(ok.Value);
        Assert.Equal("Succeeded", response.Status);
        Assert.Equal(6, response.RawRecordCount);
        Assert.Equal(6, response.AcceptedProfileCount);
        Assert.Equal(6, response.InsertedProfileCount);
        Assert.Null(response.ErrorMessage);
    }
}
