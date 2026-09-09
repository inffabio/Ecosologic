using System.Data.Common;
using Ecosologic.Application.Solar;
using Ecosologic.Infrastructure.Persistence;
using Ecosologic.Infrastructure.Solar;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ecosologic.Api.Tests;

public class AneelTariffImportServiceTests
{
    private const string SourceUrl = "https://www.aneel.gov.br/relatorio-tarifas";
    private const string Hash1 = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string Hash2 = "2222222222222222222222222222222222222222222222222222222222222222";

    private static AneelTariffSyncOptions Options() => new(SourceUrl);

    private static EcosologicDbContext NewSqliteContext(out SqliteConnection connection, params IInterceptor[] interceptors)
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();

        var options = new DbContextOptionsBuilder<EcosologicDbContext>()
            .UseSqlite(connection);
        if (interceptors.Length > 0)
            options.AddInterceptors(interceptors);

        var db = new EcosologicDbContext(options.Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static AneelTariffRecord Row(string distributor, string subgroup, string validityStart, string? validityEnd, string value = "41.20") =>
        new(distributor, "B", subgroup, "Convencional", "Único", "TUSD Fio B", "R$/MWh", value, "RES 3.242/2024", validityStart, validityEnd, null);

    private static IReadOnlyList<AneelTariffRecord> FullCoverage(string validityStart, string? validityEnd, string value = "41.20") =>
        [.. from distributor in new[] { "Light", "Enel RJ" }
           from subgroup in new[] { "B1", "B2", "B3" }
           select Row(distributor, subgroup, validityStart, validityEnd, value)];

    private sealed class FakeSource(
        Func<AneelTariffSyncOptions, CancellationToken, Task<AneelTariffFetchResult>> fetch) : IAneelTariffSource
    {
        public Task<AneelTariffFetchResult> FetchAsync(AneelTariffSyncOptions options, CancellationToken cancellationToken = default) =>
            fetch(options, cancellationToken);
    }

    private sealed class CancelOnTerminalImportUpdateInterceptor(CancellationTokenSource cts) : DbCommandInterceptor
    {
        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            CancelOnTerminalSuccess(command);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CancelOnTerminalSuccess(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            CancelOnTerminalSuccess(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            CancelOnTerminalSuccess(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void CancelOnTerminalSuccess(DbCommand command)
        {
            var text = command.CommandText;
            if (text.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                && text.Contains("aneel_tariff_imports", StringComparison.OrdinalIgnoreCase)
                && text.Contains("CompletedAt", StringComparison.OrdinalIgnoreCase))
            {
                cts.Cancel();
            }
        }
    }

    private static IAneelTariffSource Sequence(params AneelTariffFetchResult[] results)
    {
        var queue = new Queue<AneelTariffFetchResult>(results);
        return new FakeSource((_, _) => Task.FromResult(queue.Dequeue()));
    }

    private static AneelTariffImportService NewService(EcosologicDbContext db, IAneelTariffSource source) =>
        new(db, source, new AneelTariffNormalizer(), new TariffCatalog(db));

    [Fact]
    public async Task Import_first_run_inserts_all_profiles_and_marks_succeeded()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var source = new FakeSource((_, _) => Task.FromResult(
            new AneelTariffFetchResult(FullCoverage("2024-01-01", "2024-12-31"), Hash1, SourceUrl, DateTimeOffset.UtcNow)));
        var service = NewService(db, source);

        var runId = await service.ImportAsync(Options());

        Assert.Equal(6, await db.TariffProfiles.CountAsync());

        var run = await db.AneelTariffImports.SingleAsync();
        Assert.Equal(runId, run.Id);
        Assert.Equal(AneelTariffImportStatus.Succeeded, run.Status);
        Assert.Equal(Hash1, run.SourceHash);
        Assert.Equal(6, run.RawRecordCount);
        Assert.Equal(6, run.AcceptedProfileCount);
        Assert.Equal(0, run.RejectedRecordCount);
        Assert.Equal(6, run.InsertedProfileCount);
        Assert.Equal(0, run.ClosedProfileCount);
        Assert.Equal(0, run.UnchangedProfileCount);
        Assert.Null(run.ErrorMessage);
    }

    [Fact]
    public async Task Import_identical_rerun_is_idempotent()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var source = new FakeSource((_, _) => Task.FromResult(
            new AneelTariffFetchResult(FullCoverage("2024-01-01", "2024-12-31"), Hash1, SourceUrl, DateTimeOffset.UtcNow)));
        var service = NewService(db, source);

        await service.ImportAsync(Options());
        await service.ImportAsync(Options());

        Assert.Equal(6, await db.TariffProfiles.CountAsync());

        var runs = (await db.AneelTariffImports.ToListAsync()).OrderBy(r => r.StartedAt).ToList();
        Assert.Equal(2, runs.Count);
        Assert.All(runs, r => Assert.Equal(AneelTariffImportStatus.Succeeded, r.Status));
        Assert.Equal(6, runs[1].UnchangedProfileCount);
        Assert.Equal(0, runs[1].InsertedProfileCount);
        Assert.Equal(0, runs[1].ClosedProfileCount);
    }

    [Fact]
    public async Task Import_changed_source_hash_rolls_validity_without_overlap()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var source = Sequence(
            new AneelTariffFetchResult(FullCoverage("2024-01-01", null), Hash1, SourceUrl, DateTimeOffset.UtcNow),
            new AneelTariffFetchResult(FullCoverage("2025-01-01", null), Hash2, SourceUrl, DateTimeOffset.UtcNow));
        var service = NewService(db, source);

        await service.ImportAsync(Options());
        await service.ImportAsync(Options());

        Assert.Equal(12, await db.TariffProfiles.CountAsync());

        var previous = await db.TariffProfiles.Where(p => p.ValidityStart == new DateOnly(2024, 1, 1)).ToListAsync();
        var current = await db.TariffProfiles.Where(p => p.ValidityStart == new DateOnly(2025, 1, 1)).ToListAsync();

        Assert.Equal(6, previous.Count);
        Assert.Equal(6, current.Count);
        Assert.All(previous, p => Assert.Equal(new DateOnly(2024, 12, 31), p.ValidityEnd));
        Assert.All(current, p => Assert.Null(p.ValidityEnd));
    }

    [Fact]
    public async Task Import_multiple_periods_inserts_all_profiles()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var records = new List<AneelTariffRecord>();
        records.AddRange(FullCoverage("2024-01-01", "2024-12-31"));
        records.AddRange(FullCoverage("2025-01-01", null));

        var source = new FakeSource((_, _) => Task.FromResult(
            new AneelTariffFetchResult(records, Hash1, SourceUrl, DateTimeOffset.UtcNow)));
        var service = NewService(db, source);

        await service.ImportAsync(Options());

        Assert.Equal(12, await db.TariffProfiles.CountAsync());
        var run = await db.AneelTariffImports.SingleAsync();
        Assert.Equal(12, run.AcceptedProfileCount);
        Assert.Equal(12, run.InsertedProfileCount);
    }

    [Fact]
    public async Task Import_incomplete_source_fails_and_preserves_last_valid_rows()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var incomplete = FullCoverage("2024-01-01", "2024-12-31")
            .Where(r => !(r.DistributorName == "Enel RJ" && r.Subgroup == "B3"))
            .ToList();

        var source = Sequence(
            new AneelTariffFetchResult(FullCoverage("2024-01-01", "2024-12-31"), Hash1, SourceUrl, DateTimeOffset.UtcNow),
            new AneelTariffFetchResult(incomplete, Hash2, SourceUrl, DateTimeOffset.UtcNow));
        var service = NewService(db, source);

        await service.ImportAsync(Options());

        await Assert.ThrowsAsync<AneelCoverageException>(() => service.ImportAsync(Options()));

        Assert.Equal(6, await db.TariffProfiles.CountAsync());

        var runs = (await db.AneelTariffImports.ToListAsync()).OrderBy(r => r.StartedAt).ToList();
        Assert.Equal(AneelTariffImportStatus.Failed, runs[1].Status);
        Assert.NotNull(runs[1].ErrorMessage);
        Assert.DoesNotContain("Enel", runs[1].ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("B3", runs[1].ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Import_source_failure_marks_failed_with_sanitized_error()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var source = new FakeSource((_, _) => throw new AneelTariffSourceException(
            AneelErrorCode.Transport,
            "Falha de transporte ao consultar a fonte ANEEL."));
        var service = NewService(db, source);

        await Assert.ThrowsAsync<AneelTariffSourceException>(() => service.ImportAsync(Options()));

        Assert.Equal(0, await db.TariffProfiles.CountAsync());

        var run = await db.AneelTariffImports.SingleAsync();
        Assert.Equal(AneelTariffImportStatus.Failed, run.Status);
        Assert.Equal(AneelErrorCode.Transport, run.ErrorMessage);
    }

    [Fact]
    public async Task Import_unknown_failure_does_not_leak_raw_error_text()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var source = new FakeSource((_, _) => throw new InvalidOperationException("SECRET_TOKEN=super_secret"));
        var service = NewService(db, source);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportAsync(Options()));

        var run = await db.AneelTariffImports.SingleAsync();
        Assert.Equal(AneelTariffImportStatus.Failed, run.Status);
        Assert.DoesNotContain("SECRET_TOKEN", run.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("super_secret", run.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Import_reconciliation_failure_after_one_distributor_rolls_back_entire_batch()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var source = Sequence(
            new AneelTariffFetchResult(FullCoverage("2024-01-01", "2024-12-31"), Hash1, SourceUrl, DateTimeOffset.UtcNow),
            new AneelTariffFetchResult(BatchWithRetroactiveEnel(), Hash2, SourceUrl, DateTimeOffset.UtcNow));
        var service = NewService(db, source);

        await service.ImportAsync(Options());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportAsync(Options()));

        // Nenhuma parte do lote é publicada: a distribuidora já preparada (Light 2025)
        // é desfeita junto com a falha da Enel.
        Assert.Equal(6, await db.TariffProfiles.CountAsync());
        Assert.Empty(await db.TariffProfiles
            .Where(p => p.ValidityStart == new DateOnly(2025, 1, 1))
            .ToListAsync());

        var runs = (await db.AneelTariffImports.ToListAsync()).OrderBy(r => r.StartedAt).ToList();
        Assert.Equal(2, runs.Count);
        Assert.Equal(AneelTariffImportStatus.Succeeded, runs[0].Status);
        Assert.Equal(AneelTariffImportStatus.Failed, runs[1].Status);
        Assert.Equal("ANEEL_RECONCILIATION", runs[1].ErrorMessage);
    }

    [Fact]
    public async Task Import_normalization_failure_preserves_source_hash_and_raw_count()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var incomplete = FullCoverage("2024-01-01", "2024-12-31")
            .Where(r => !(r.DistributorName == "Enel RJ" && r.Subgroup == "B3"))
            .ToList();

        var source = new FakeSource((_, _) => Task.FromResult(
            new AneelTariffFetchResult(incomplete, Hash2, SourceUrl, DateTimeOffset.UtcNow)));
        var service = NewService(db, source);

        await Assert.ThrowsAsync<AneelCoverageException>(() => service.ImportAsync(Options()));

        var run = await db.AneelTariffImports.SingleAsync();
        Assert.Equal(AneelTariffImportStatus.Failed, run.Status);
        Assert.Equal(Hash2, run.SourceHash);
        Assert.Equal(5, run.RawRecordCount);
    }

    [Fact]
    public async Task Import_reports_accurate_rejected_counts_when_rows_collapse()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        // 7 linhas brutas → 6 perfis (a duplicada colapsa), zero rejeições.
        var rows = FullCoverage("2024-01-01", "2024-12-31").ToList();
        rows.Add(Row("Light", "B1", "2024-01-01", "2024-12-31"));

        var source = new FakeSource((_, _) => Task.FromResult(
            new AneelTariffFetchResult(rows, Hash1, SourceUrl, DateTimeOffset.UtcNow)));
        var service = NewService(db, source);

        await service.ImportAsync(Options());

        var run = await db.AneelTariffImports.SingleAsync();
        Assert.Equal(7, run.RawRecordCount);
        Assert.Equal(6, run.AcceptedProfileCount);
        Assert.Equal(0, run.RejectedRecordCount);
    }

    [Fact]
    public async Task Import_same_period_correction_supersedes_without_overlap()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var source = Sequence(
            new AneelTariffFetchResult(FullCoverage("2024-01-01", null), Hash1, SourceUrl, DateTimeOffset.UtcNow),
            new AneelTariffFetchResult(FullCoverage("2024-01-01", null, "42.00"), Hash2, SourceUrl, DateTimeOffset.UtcNow));
        var service = NewService(db, source);

        await service.ImportAsync(Options());
        await service.ImportAsync(Options());

        Assert.Equal(12, await db.TariffProfiles.CountAsync());
        Assert.Equal(6, await db.TariffProfiles.CountAsync(p => p.IsCurrent));
        Assert.Equal(6, await db.TariffProfiles.CountAsync(p => !p.IsCurrent));

        var run = (await db.AneelTariffImports.ToListAsync()).OrderBy(r => r.StartedAt).Last();
        Assert.Equal(6, run.ClosedProfileCount);
        Assert.Equal(6, run.InsertedProfileCount);
        Assert.Equal(0, run.UnchangedProfileCount);
    }

    [Fact]
    public async Task Import_fetch_failure_persists_running_record_before_fetch_then_single_failed_record()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var source = new FakeSource((_, _) =>
        {
            Assert.True(db.AneelTariffImports.Any(r => r.Status == AneelTariffImportStatus.Running),
                "A execução deve persistir um registro Running antes de consultar a fonte ANEEL.");
            return Task.FromException<AneelTariffFetchResult>(
                new AneelTariffSourceException(AneelErrorCode.Transport, "Falha de transporte ao consultar a fonte ANEEL."));
        });
        var service = NewService(db, source);

        await Assert.ThrowsAsync<AneelTariffSourceException>(() => service.ImportAsync(Options()));

        var run = Assert.Single(await db.AneelTariffImports.ToListAsync());
        Assert.Equal(AneelTariffImportStatus.Failed, run.Status);
        Assert.Equal(AneelErrorCode.Transport, run.ErrorMessage);
        Assert.Equal(0, await db.TariffProfiles.CountAsync());
    }

    [Fact]
    public async Task Import_normalization_failure_records_single_failed_record_preserving_source()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var incomplete = FullCoverage("2024-01-01", "2024-12-31")
            .Where(r => !(r.DistributorName == "Enel RJ" && r.Subgroup == "B3"))
            .ToList();

        var source = new FakeSource((_, _) =>
        {
            Assert.True(db.AneelTariffImports.Any(r => r.Status == AneelTariffImportStatus.Running),
                "A execução deve persistir um registro Running antes de normalizar a fonte ANEEL.");
            return Task.FromResult(
                new AneelTariffFetchResult(incomplete, Hash2, SourceUrl, DateTimeOffset.UtcNow));
        });
        var service = NewService(db, source);

        await Assert.ThrowsAsync<AneelCoverageException>(() => service.ImportAsync(Options()));

        var run = Assert.Single(await db.AneelTariffImports.ToListAsync());
        Assert.Equal(AneelTariffImportStatus.Failed, run.Status);
        Assert.Equal(Hash2, run.SourceHash);
        Assert.Equal(5, run.RawRecordCount);
    }

    [Fact]
    public async Task Import_cancellation_before_reconciliation_marks_single_failed_record_and_rethrows()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var source = new FakeSource((_, token) =>
        {
            Assert.True(db.AneelTariffImports.Any(r => r.Status == AneelTariffImportStatus.Running),
                "A execução deve persistir um registro Running antes de consultar a fonte ANEEL.");
            token.ThrowIfCancellationRequested();
            return Task.FromResult(
                new AneelTariffFetchResult(FullCoverage("2024-01-01", "2024-12-31"), Hash1, SourceUrl, DateTimeOffset.UtcNow));
        });
        var service = NewService(db, source);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ImportAsync(Options(), cts.Token));

        var run = Assert.Single(await db.AneelTariffImports.ToListAsync());
        Assert.Equal(AneelTariffImportStatus.Failed, run.Status);
        Assert.NotNull(run.ErrorMessage);
        Assert.Equal(0, await db.TariffProfiles.CountAsync());
    }

    [Fact]
    public async Task Import_cancellation_during_terminal_commit_still_marks_succeeded()
    {
        using var cts = new CancellationTokenSource();
        var interceptor = new CancelOnTerminalImportUpdateInterceptor(cts);

        using var db = NewSqliteContext(out var connection, interceptor);
        using var _ = connection;

        var source = new FakeSource((_, _) => Task.FromResult(
            new AneelTariffFetchResult(FullCoverage("2024-01-01", "2024-12-31"), Hash1, SourceUrl, DateTimeOffset.UtcNow)));
        var service = NewService(db, source);

        var runId = await service.ImportAsync(Options(), cts.Token);

        Assert.NotEqual(Guid.Empty, runId);
        Assert.Equal(6, await db.TariffProfiles.CountAsync());

        var run = await db.AneelTariffImports.SingleAsync();
        Assert.Equal(AneelTariffImportStatus.Succeeded, run.Status);
        Assert.Null(run.ErrorMessage);
    }

    [Fact]
    public async Task ReserveRun_persists_running_record_even_when_request_is_cancelled()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var source = new FakeSource((_, _) => Task.FromResult(
            new AneelTariffFetchResult(FullCoverage("2024-01-01", "2024-12-31"), Hash1, SourceUrl, DateTimeOffset.UtcNow)));
        var service = NewService(db, source);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var runId = await service.ReserveRunAsync(Options(), cts.Token);

        Assert.NotEqual(Guid.Empty, runId);
        var run = await db.AneelTariffImports.SingleAsync();
        Assert.Equal(AneelTariffImportStatus.Running, run.Status);
    }

    [Fact]
    public async Task Import_retry_with_same_run_id_reuses_single_audit_record()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var runId = Guid.NewGuid();
        var jobId = "hangfire-12345";
        var attempts = 0;
        var source = new FakeSource((_, _) =>
        {
            attempts++;
            if (attempts == 1)
                throw new AneelTariffSourceException(AneelErrorCode.Transport, "Falha de transporte ao consultar a fonte ANEEL.");
            return Task.FromResult(
                new AneelTariffFetchResult(FullCoverage("2024-01-01", "2024-12-31"), Hash1, SourceUrl, DateTimeOffset.UtcNow));
        });
        var service = NewService(db, source);

        await Assert.ThrowsAsync<AneelTariffSourceException>(
            () => service.ImportAsync(Options(), runId, jobId, CancellationToken.None));

        var failed = Assert.Single(await db.AneelTariffImports.ToListAsync());
        Assert.Equal(AneelTariffImportStatus.Failed, failed.Status);
        Assert.Equal(jobId, failed.JobId);

        var returned = await service.ImportAsync(Options(), runId, jobId, CancellationToken.None);

        Assert.Equal(runId, returned);
        var run = Assert.Single(await db.AneelTariffImports.ToListAsync());
        Assert.Equal(AneelTariffImportStatus.Succeeded, run.Status);
        Assert.Equal(jobId, run.JobId);
        Assert.Equal(6, await db.TariffProfiles.CountAsync());
    }

    [Fact]
    public async Task Import_persists_hangfire_job_id_on_created_record()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var source = new FakeSource((_, _) => Task.FromResult(
            new AneelTariffFetchResult(FullCoverage("2024-01-01", "2024-12-31"), Hash1, SourceUrl, DateTimeOffset.UtcNow)));
        var service = NewService(db, source);
        var runId = Guid.NewGuid();

        await service.ImportAsync(Options(), runId, "hangfire-job-1", CancellationToken.None);

        var run = await db.AneelTariffImports.SingleAsync();
        Assert.Equal("hangfire-job-1", run.JobId);
    }

    [Fact]
    public async Task FailRun_marks_reserved_run_failed_without_leaving_running()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var source = new FakeSource((_, _) => Task.FromResult(
            new AneelTariffFetchResult(FullCoverage("2024-01-01", "2024-12-31"), Hash1, SourceUrl, DateTimeOffset.UtcNow)));
        var service = NewService(db, source);

        var runId = await service.ReserveRunAsync(Options());

        await service.FailRunAsync(runId, "HANGFIRE_ENQUEUE_FAILED");

        var run = await db.AneelTariffImports.SingleAsync();
        Assert.Equal(AneelTariffImportStatus.Failed, run.Status);
        Assert.Equal("HANGFIRE_ENQUEUE_FAILED", run.ErrorMessage);
        Assert.NotNull(run.CompletedAt);
        Assert.Empty(await db.AneelTariffImports
            .Where(r => r.Status == AneelTariffImportStatus.Running)
            .ToListAsync());
    }

    [Fact]
    public async Task AttachJobId_sets_job_id_when_null_and_is_idempotent()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var source = new FakeSource((_, _) => Task.FromResult(
            new AneelTariffFetchResult(FullCoverage("2024-01-01", "2024-12-31"), Hash1, SourceUrl, DateTimeOffset.UtcNow)));
        var service = NewService(db, source);

        var runId = await service.ReserveRunAsync(Options());

        await service.AttachJobIdAsync(runId, "hangfire-job-a");
        await service.AttachJobIdAsync(runId, "hangfire-job-b");

        var run = await db.AneelTariffImports.SingleAsync();
        Assert.Equal("hangfire-job-a", run.JobId);
    }

    private static IReadOnlyList<AneelTariffRecord> BatchWithRetroactiveEnel()
    {
        var rows = new List<AneelTariffRecord>();
        foreach (var subgroup in new[] { "B1", "B2", "B3" })
            rows.Add(Row("Light", subgroup, "2025-01-01", "2025-12-31"));
        foreach (var subgroup in new[] { "B1", "B2", "B3" })
            rows.Add(Row("Enel RJ", subgroup, "2023-06-01", "2024-06-30"));
        return rows;
    }
}
