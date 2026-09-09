using Hangfire;
using Hangfire.States;
using Hangfire.Storage;

namespace Ecosologic.Infrastructure.Jobs;

/// <summary>
/// Filtro Hangfire que aplica a política de retry configurável da sincronização ANEEL.
/// Delega ao <see cref="AutomaticRetryAttribute"/> (com as tentativas e atrasos
/// resolvidos em tempo de execução a partir de <see cref="AneelTariffRetryPolicy"/>),
/// mas restringe sua atuação ao <see cref="AneelTariffSyncJob"/> — os demais jobs
/// (ex.: notificações CRM) mantêm suas próprias políticas de retry declaradas.
/// </summary>
public sealed class AneelTariffRetryFilter : IElectStateFilter, IApplyStateFilter
{
    private readonly AutomaticRetryAttribute _inner;

    public AneelTariffRetryFilter(AneelTariffRetryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        _inner = new AutomaticRetryAttribute
        {
            Attempts = policy.MaxRetries,
            DelaysInSeconds = policy.DelaysInSeconds.ToArray()
        };
    }

    public int Attempts => _inner.Attempts;

    public int[] DelaysInSeconds => _inner.DelaysInSeconds;

    public void OnStateElection(ElectStateContext context)
    {
        if (AppliesToAneel(context.BackgroundJob?.Job?.Type))
            _inner.OnStateElection(context);
    }

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (AppliesToAneel(context.BackgroundJob?.Job?.Type))
            _inner.OnStateApplied(context, transaction);
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (AppliesToAneel(context.BackgroundJob?.Job?.Type))
            _inner.OnStateUnapplied(context, transaction);
    }

    internal static bool AppliesToAneel(Type? jobType) => jobType == typeof(AneelTariffSyncJob);
}
