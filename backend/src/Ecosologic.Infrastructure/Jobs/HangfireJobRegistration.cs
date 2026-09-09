using Hangfire;
using Microsoft.Extensions.Configuration;

namespace Ecosologic.Infrastructure.Jobs;

public static class HangfireJobRegistration
{
    public const string CrmNotificationsJobId = "crm-notifications";
    public const string DefaultCrmNotificationCron = "*/5 * * * *";

    public const string AneelTariffSyncJobId = "aneel-tariff-sync";
    public const string DefaultAneelTariffCron = "0 3 1 * *";
    public const string DefaultAneelTariffTimeZoneId = "America/Sao_Paulo";

    public static void RegisterRecurringJobs(IRecurringJobManager recurringJobManager, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(recurringJobManager);
        ArgumentNullException.ThrowIfNull(configuration);

        RegisterCrmNotifications(recurringJobManager, configuration);
        RegisterAneelTariffSync(recurringJobManager, configuration);
    }

    /// <summary>
    /// Registra (idempotentemente) o filtro de retry configurável da sincronização ANEEL
    /// a partir de <c>Tariffs:MaxRetries</c>. Deve ser chamado uma vez na inicialização,
    /// antes de o servidor Hangfire processar qualquer job.
    /// </summary>
    public static AneelTariffRetryFilter RegisterAneelRetryFilter(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        GlobalJobFilters.Filters.Remove<AneelTariffRetryFilter>();
        var filter = new AneelTariffRetryFilter(AneelTariffRetryPolicy.Resolve(configuration));
        GlobalJobFilters.Filters.Add(filter);
        return filter;
    }

    private static void RegisterCrmNotifications(IRecurringJobManager manager, IConfiguration configuration)
    {
        var cron = configuration["Crm:NotificationCron"];
        if (string.IsNullOrWhiteSpace(cron))
            cron = DefaultCrmNotificationCron;

        manager.AddOrUpdate<CrmNotificationJob>(
            CrmNotificationsJobId,
            job => job.ExecuteAsync(CancellationToken.None),
            cron);
    }

    private static void RegisterAneelTariffSync(IRecurringJobManager manager, IConfiguration configuration)
    {
        var cron = configuration["Tariffs:SyncCron"];
        if (string.IsNullOrWhiteSpace(cron))
            cron = DefaultAneelTariffCron;

        var timeZoneId = configuration["Tariffs:TimeZoneId"];
        if (string.IsNullOrWhiteSpace(timeZoneId))
            timeZoneId = DefaultAneelTariffTimeZoneId;

        manager.AddOrUpdate<AneelTariffSyncJob>(
            AneelTariffSyncJobId,
            job => job.ExecuteAsync(null!, CancellationToken.None),
            cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId) });
    }
}
