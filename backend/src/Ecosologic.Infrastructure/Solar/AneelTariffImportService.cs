using System.Data;
using System.Text.Json;
using Ecosologic.Application.Solar;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Infrastructure.Solar;

/// <summary>
/// Orquestra uma sincronização ANEEL ponta a ponta. Antes de consultar a fonte ou
/// normalizar, este serviço garante (de forma idempotente) a existência de um registro
/// de auditoria <c>Running</c> para o identificador de execução — a execução é
/// observável desde o primeiro instante e, em um retry do Hangfire para o MESMO
/// <c>runId</c>, o MESMO registro é reutilizado (nunca duplicado). A consulta externa e
/// a normalização acontecem sem escrita adicional no banco; em seguida uma ÚNICA
/// transação Serializable, de propriedade deste serviço, carrega o MESMO registro
/// <c>Running</c> e reconcilia o catálogo tarifário. Somente após o commit o registro é
/// marcado como <c>Succeeded</c>; qualquer falha desfaz tarifas e auditoria juntas. Em
/// uma transação separada e segura, a falha (inclusive cancelamento) marca o MESMO
/// registro como <c>Failed</c> preservando hash e contagens da fonte quando disponíveis,
/// e a exceção é reapresentada para retry pelo Hangfire. Existe exatamente um registro
/// terminal por execução; nunca restam registros <c>Running</c> órfãos nem duplicatas
/// <c>Running</c>/<c>Failed</c>.
/// </summary>
/// <remarks>
/// Isolamento e providers: a transação usa <see cref="IsolationLevel.Serializable"/>.
/// Em SQLite (usado nos testes), o write-lock já serializa o commit, então a checagem
/// de sobreposição é suficiente. Em PostgreSQL, uma falha de serialização (SQLSTATE
/// 40001) deve ser reapresentada ao chamador para retry pelo Hangfire; não há
/// cobertura de integração PostgreSQL automatizada neste repositório (nenhum banco
/// local/remoto disponível), então essa limitação permanece explícita aqui e a
/// abstração transacional é exercitada pelos testes em SQLite.
/// </remarks>
public sealed class AneelTariffImportService(
    EcosologicDbContext db,
    IAneelTariffSource source,
    AneelTariffNormalizer normalizer,
    TariffCatalog catalog) : IAneelTariffImportService
{
    public Task<Guid> ImportAsync(AneelTariffSyncOptions options, CancellationToken cancellationToken = default)
        => ImportAsync(options, Guid.NewGuid(), jobId: null, cancellationToken);

    public async Task<Guid> ImportAsync(
        AneelTariffSyncOptions options,
        Guid runId,
        string? jobId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (runId == Guid.Empty)
            throw new ArgumentException("O identificador da importação é obrigatório.", nameof(runId));

        string? sourceHash = null;
        var rawRecordCount = 0;
        int? acceptedProfileCount = null;
        int? rejectedRecordCount = null;

        await EnsureRunningRecordAsync(runId, jobId, options);

        try
        {
            var fetch = await source.FetchAsync(options, cancellationToken);
            sourceHash = fetch.SourceHash;
            rawRecordCount = fetch.Records.Count;

            var normalization = normalizer.NormalizeWithCounts(fetch.Records, fetch.SourceHash, options);
            acceptedProfileCount = normalization.Profiles.Count;
            rejectedRecordCount = normalization.RejectedRawRecordCount;

            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var run = await db.AneelTariffImports.SingleAsync(record => record.Id == runId, cancellationToken);
                run.SetSource(fetch.SourceHash, fetch.Records.Count);
                run.SetValidationCounts(normalization.Profiles.Count, normalization.RejectedRawRecordCount);

                var result = await catalog.ReconcileImportedProfilesInTransactionAsync(
                    normalization.Profiles,
                    cancellationToken);

                run.MarkSucceeded(result.InsertedProfiles, result.ClosedProfiles, result.UnchangedProfiles);

                // O token do chamador NÃO é usado aqui: uma vez alcançado o ponto de
                // persistência do sucesso, a conclusão é autoritativa. Um cancelamento
                // tardio (durante ou após o commit) não pode marcar a execução como
                // Failed nem desfazer tarifas já reconciliadas.
                await db.SaveChangesAsync(CancellationToken.None);
                await transaction.CommitAsync(CancellationToken.None);

                return runId;
            }
            catch
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // A transação já pode ter sido desfeita pelo provider.
                }

                db.ChangeTracker.Clear();
                throw;
            }
        }
        catch (Exception exception)
        {
            await RecordFailureAsync(runId, options, sourceHash, rawRecordCount, acceptedProfileCount, rejectedRecordCount, exception);
            throw;
        }
    }

    public async Task<Guid> ReserveRunAsync(AneelTariffSyncOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var runId = Guid.NewGuid();
        var run = AneelTariffImportRecord.Create(runId, jobId: null, BuildFiltersJson(), options.SourceUrl);
        db.AneelTariffImports.Add(run);

        // A reserva manual deve ser durável mesmo sob cancelamento da requisição:
        // persistir com CancellationToken.None evita um registro Running órfão quando o
        // token do chamador já foi cancelado no momento da gravação.
        await db.SaveChangesAsync(CancellationToken.None);

        return runId;
    }

    public async Task AttachJobIdAsync(Guid runId, string jobId, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty)
            throw new ArgumentException("O identificador da importação é obrigatório.", nameof(runId));
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("O identificador do job Hangfire é obrigatório.", nameof(jobId));

        var run = await db.AneelTariffImports.SingleOrDefaultAsync(record => record.Id == runId, cancellationToken);
        if (run is null)
            return;

        run.SetJobId(jobId);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailRunAsync(Guid runId, string errorMessage, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty)
            throw new ArgumentException("O identificador da importação é obrigatório.", nameof(runId));

        var run = await db.AneelTariffImports.SingleOrDefaultAsync(record => record.Id == runId, cancellationToken);
        if (run is null)
            return;

        run.MarkFailed(errorMessage);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureRunningRecordAsync(Guid runId, string? jobId, AneelTariffSyncOptions options)
    {
        var run = await db.AneelTariffImports.SingleOrDefaultAsync(record => record.Id == runId, CancellationToken.None);

        if (run is null)
        {
            run = AneelTariffImportRecord.Create(runId, jobId, BuildFiltersJson(), options.SourceUrl);
            db.AneelTariffImports.Add(run);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(jobId))
                run.SetJobId(jobId);

            run.MarkRunning();
        }

        // O token do chamador NÃO é usado aqui: mesmo que a execução já tenha sido
        // cancelada, o registro Running deve persistir para que o cancelamento seja
        // auditado como Failed na transação de falha.
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private async Task RecordFailureAsync(
        Guid runId,
        AneelTariffSyncOptions options,
        string? sourceHash,
        int rawRecordCount,
        int? acceptedProfileCount,
        int? rejectedRecordCount,
        Exception exception)
    {
        try
        {
            db.ChangeTracker.Clear();

            // Transação separada e segura: não reutiliza a transação Serializable que
            // pode ter falhado e mantém o registro de falha mesmo sob cancelamento.
            await using var transaction = await db.Database.BeginTransactionAsync(CancellationToken.None);

            try
            {
                var run = await db.AneelTariffImports
                    .SingleOrDefaultAsync(record => record.Id == runId, CancellationToken.None);

                if (run is null)
                {
                    run = AneelTariffImportRecord.Create(runId, jobId: null, BuildFiltersJson(), options.SourceUrl);
                    db.AneelTariffImports.Add(run);
                }

                if (sourceHash is not null || rawRecordCount > 0)
                    run.SetSource(sourceHash, rawRecordCount);

                if (acceptedProfileCount.HasValue && rejectedRecordCount.HasValue)
                    run.SetValidationCounts(acceptedProfileCount.Value, rejectedRecordCount.Value);

                run.MarkFailed(SanitizeError(exception));
                await db.SaveChangesAsync(CancellationToken.None);
                await transaction.CommitAsync(CancellationToken.None);
            }
            catch
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // A transação já pode ter sido desfeita pelo provider.
                }

                db.ChangeTracker.Clear();
                throw;
            }
        }
        catch
        {
            // Persistir a falha de auditoria nunca deve mascarar a exceção original.
        }
    }

    private static string SanitizeError(Exception exception) => exception switch
    {
        AneelTariffSourceException source => source.Code,
        AneelNormalizationException => "ANEEL_NORMALIZATION",
        InvalidOperationException => "ANEEL_RECONCILIATION",
        _ => exception.GetType().Name
    };

    private static string BuildFiltersJson() =>
        JsonSerializer.Serialize(new
        {
            distributors = AneelDistributors.All.Select(identity => identity.ReportLabel).ToArray(),
            subgroups = new[] { "B1", "B2", "B3" },
            component = "TUSD Fio B",
            unit = "R$/MWh"
        });
}
