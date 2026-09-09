using System.Security.Cryptography;
using System.Text;
using Ecosologic.Infrastructure.Solar;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecosologic.Infrastructure.Jobs;

/// <summary>
/// Job Hangfire da sincronização mensal ANEEL. A execução recorrente e o disparo
/// manual enfileirado pela API compartilham o MESMO lock distribuído
/// (<see cref="DistributedLockResource"/>) para que nunca se sobreponham, mesmo em
/// múltiplas instâncias do servidor. Falhas não são engolidas: o Hangfire aplica a
/// política de retry configurável (ver <see cref="AneelTariffRetryPolicy"/>) com
/// backoff limitado.
/// </summary>
/// <remarks>
/// A execução recorrente deriva um identificador de execução DETERMINÍSTICO a partir do
/// identificador do background job (<see cref="PerformContext"/>), estável entre retries
/// do MESMO job. Assim, retentar um job agendado reutiliza o MESMO registro de auditoria
/// em vez de criar duplicatas <c>Running</c>/<c>Failed</c>. O disparo manual usa o
/// identificador de execução previamente reservado pela API.
/// </remarks>
public sealed class AneelTariffSyncJob(IServiceScopeFactory scopeFactory)
{
    public const string DistributedLockResource = "aneel-tariff-sync";

    [DisableConcurrentExecution(DistributedLockResource, 600)]
    public Task ExecuteAsync(PerformContext context, CancellationToken cancellationToken)
    {
        var jobId = context.BackgroundJob.Id;
        return ExecuteCoreAsync(DeriveRunId(jobId), jobId, cancellationToken);
    }

    [DisableConcurrentExecution(DistributedLockResource, 600)]
    public Task ExecuteAsync(Guid runId, PerformContext context, CancellationToken cancellationToken)
        => ExecuteCoreAsync(runId, context.BackgroundJob.Id, cancellationToken);

    private async Task ExecuteCoreAsync(Guid runId, string jobId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var import = provider.GetRequiredService<IAneelTariffImportService>();
        var options = AneelTariffSyncOptionsFactory.Build(provider.GetRequiredService<IConfiguration>());

        await import.ImportAsync(options, runId, jobId, cancellationToken);
    }

    /// <summary>
    /// Deriva um <see cref="Guid"/> determinístico a partir do identificador do background
    /// job Hangfire. O mesmo job (retry incluído) produz o mesmo <see cref="Guid"/>; jobs
    /// distintos produzem identificadores distintos.
    /// </summary>
    public static Guid DeriveRunId(string jobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(jobId));
        Span<byte> bytes = stackalloc byte[16];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = hash[i];

        return new Guid(bytes);
    }
}
