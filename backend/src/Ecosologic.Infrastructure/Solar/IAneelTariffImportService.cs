using Ecosologic.Application.Solar;

namespace Ecosologic.Infrastructure.Solar;

/// <summary>
/// Contrato de sincronização tarifária ANEEL exposto ao job Hangfire e ao endpoint
/// administrativo. Separa a execução (importação ponta a ponta, reexecutável para o
/// MESMO identificador de execução) da reserva de uma execução pendente, permitindo
/// que o disparo manual devolva o identificador da execução sem realizar a importação
/// de forma síncrona.
/// </summary>
public interface IAneelTariffImportService
{
    /// <summary>
    /// Executa uma importação ponta a ponta com um identificador de execução novo
    /// (não reutilizável em retries). Usado por testes e consumidores que desejam uma
    /// execução isolada; os jobs Hangfire usam a sobrecarga com <paramref name="runId"/>.
    /// </summary>
    Task<Guid> ImportAsync(AneelTariffSyncOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa (ou reexecuta) a importação para o identificador de execução informado,
    /// reutilizando o registro de auditoria existente quando já existir um para o mesmo
    /// <paramref name="runId"/> (retry-safe). O <paramref name="jobId"/> Hangfire é
    /// persistido no registro quando ainda não definido.
    /// </summary>
    Task<Guid> ImportAsync(AneelTariffSyncOptions options, Guid runId, string? jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reserva uma nova execução (persistindo um registro <c>Running</c>) e devolve seu
    /// identificador, sem executar a importação.
    /// </summary>
    Task<Guid> ReserveRunAsync(AneelTariffSyncOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste o identificador do job Hangfire no registro de execução, de forma
    /// idempotente (apenas define quando ainda nulo), para o disparo manual.
    /// </summary>
    Task AttachJobIdAsync(Guid runId, string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca a execução reservada como <c>Failed</c> com uma mensagem de erro sanitizada,
    /// usada quando o enfileiramento do job Hangfire falha ou é cancelado.
    /// </summary>
    Task FailRunAsync(Guid runId, string errorMessage, CancellationToken cancellationToken = default);
}
