using Ecosologic.Infrastructure.Jobs;
using Ecosologic.Infrastructure.Persistence;
using Ecosologic.Infrastructure.Solar;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Controllers;

[ApiController]
[Route("api/admin/tariffs")]
[Authorize(Roles = "Admin")]
public sealed class AdminTariffsController(
    IAneelTariffImportService importService,
    IBackgroundJobClient backgroundJobClient,
    IConfiguration configuration,
    EcosologicDbContext db,
    ILogger<AdminTariffsController> logger) : ControllerBase
{
    private const string EnqueueFailedError = "HANGFIRE_ENQUEUE_FAILED";
    private const string EnqueueCancelledError = "HANGFIRE_ENQUEUE_CANCELLED";

    /// <summary>
    /// Reserva uma execução de sincronização ANEEL (persistindo um registro
    /// <c>Running</c>) e enfileira o job no Hangfire. Não executa a importação de
    /// forma síncrona nem aceita valores tarifários no corpo. Se o enfileiramento
    /// falhar ou for cancelado, a MESMA execução reservada é marcada como <c>Failed</c>
    /// com um erro sanitizado, evitando registros <c>Running</c> órfãos.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        var options = AneelTariffSyncOptionsFactory.Build(configuration);

        // A reserva é durável e desacoplada do token da requisição: o registro Running
        // é persistido com CancellationToken.None para que um cancelamento não deixe um
        // registro órfão nem perca o identificador da execução.
        var runId = await importService.ReserveRunAsync(options, CancellationToken.None);

        string jobId;
        try
        {
            jobId = backgroundJobClient.Enqueue<AneelTariffSyncJob>(
                job => job.ExecuteAsync(runId, null!, CancellationToken.None));
        }
        catch (Exception exception)
        {
            try
            {
                await importService.FailRunAsync(runId, SanitizeEnqueueError(exception), CancellationToken.None);
            }
            catch (Exception failException)
            {
                // Preserva a falha ORIGINAL do enfileiramento para o chamador e registra
                // apenas um diagnóstico seguro: a execução reservada permanece Running e
                // recuperável pelo status/retry/reconciliação existentes.
                logger.LogError(
                    failException,
                    "Falha ao marcar a execução {RunId} como Failed após falha de enfileiramento.",
                    runId);
            }

            throw;
        }

        // Corrida segura: o job pode já ter gravado o JobId via PerformContext; esta
        // gravação é idempotente (define apenas quando nulo) e usa o MESMO valor.
        await importService.AttachJobIdAsync(runId, jobId, CancellationToken.None);

        return Accepted(new TariffSyncAcceptedResponse(runId));
    }

    /// <summary>
    /// Retorna a última execução de sincronização ANEEL (status, contadores e
    /// metadados) sem rastrear a entidade. A consulta é ordenada no banco com
    /// desempate determinístico pelo identificador, sem carregar todas as linhas.
    /// </summary>
    [HttpGet("sync/status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var run = await db.AneelTariffImports
            .AsNoTracking()
            .OrderByDescending(record => record.StartedAt)
            .ThenByDescending(record => record.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return run is null
            ? NotFound()
            : Ok(new TariffSyncStatusResponse(
                run.Id,
                run.Status.ToString(),
                run.JobId,
                run.SourceUrl,
                run.SourceHash,
                run.StartedAt,
                run.CompletedAt,
                run.RawRecordCount,
                run.AcceptedProfileCount,
                run.RejectedRecordCount,
                run.InsertedProfileCount,
                run.ClosedProfileCount,
                run.UnchangedProfileCount,
                run.ErrorMessage));
    }

    private static string SanitizeEnqueueError(Exception exception) =>
        exception is OperationCanceledException ? EnqueueCancelledError : EnqueueFailedError;
}

public sealed record TariffSyncAcceptedResponse(Guid RunId);

public sealed record TariffSyncStatusResponse(
    Guid RunId,
    string Status,
    string? JobId,
    string SourceUrl,
    string? SourceHash,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int RawRecordCount,
    int AcceptedProfileCount,
    int RejectedRecordCount,
    int InsertedProfileCount,
    int ClosedProfileCount,
    int UnchangedProfileCount,
    string? ErrorMessage);
