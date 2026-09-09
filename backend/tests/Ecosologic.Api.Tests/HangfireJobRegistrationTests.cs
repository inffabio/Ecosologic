using System.Reflection;
using Ecosologic.Infrastructure.Jobs;
using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Configuration;

namespace Ecosologic.Api.Tests;

public class HangfireJobRegistrationTests
{
    private sealed class FakeRecurringJobManager : IRecurringJobManager
    {
        public List<(string Id, Job Job, string Cron, RecurringJobOptions Options)> Registrations = [];

        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options)
            => Registrations.Add((recurringJobId, job, cronExpression, options));

        public void Trigger(string recurringJobId) => throw new NotSupportedException();

        public void RemoveIfExists(string recurringJobId) => throw new NotSupportedException();
    }

    private static (string Id, Job Job, string Cron, RecurringJobOptions Options) Registration(
        FakeRecurringJobManager manager,
        string id) => manager.Registrations.Single(registration => registration.Id == id);

    private static IConfiguration EmptyConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    [Fact]
    public void Registers_both_recurring_jobs_with_stable_ids()
    {
        var manager = new FakeRecurringJobManager();

        HangfireJobRegistration.RegisterRecurringJobs(manager, EmptyConfiguration());

        Assert.Equal(2, manager.Registrations.Count);
        Assert.Contains(manager.Registrations, registration => registration.Id == "crm-notifications");
        Assert.Contains(manager.Registrations, registration => registration.Id == "aneel-tariff-sync");
    }

    [Fact]
    public void Registers_crm_job_with_stable_id_and_five_minute_cron()
    {
        var manager = new FakeRecurringJobManager();

        HangfireJobRegistration.RegisterRecurringJobs(manager, EmptyConfiguration());

        var (id, job, cron, _) = Registration(manager, "crm-notifications");
        Assert.Equal("*/5 * * * *", cron);
        Assert.Equal(typeof(CrmNotificationJob), job.Type);
        Assert.Equal(nameof(CrmNotificationJob.ExecuteAsync), job.Method.Name);
    }

    [Fact]
    public void Registers_aneel_job_with_stable_id_monthly_cron_and_timezone()
    {
        var manager = new FakeRecurringJobManager();

        HangfireJobRegistration.RegisterRecurringJobs(manager, EmptyConfiguration());

        var (id, job, cron, options) = Registration(manager, "aneel-tariff-sync");
        Assert.Equal("0 3 1 * *", cron);
        Assert.Equal(typeof(AneelTariffSyncJob), job.Type);
        Assert.Equal(nameof(AneelTariffSyncJob.ExecuteAsync), job.Method.Name);
        Assert.Equal("America/Sao_Paulo", options.TimeZone?.Id);
    }

    [Fact]
    public void Uses_configured_crm_cron_when_present()
    {
        var manager = new FakeRecurringJobManager();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Crm:NotificationCron"] = "*/10 * * * *" })
            .Build();

        HangfireJobRegistration.RegisterRecurringJobs(manager, configuration);

        Assert.Equal("*/10 * * * *", Registration(manager, "crm-notifications").Cron);
    }

    [Fact]
    public void Uses_configured_aneel_cron_and_timezone_when_present()
    {
        var manager = new FakeRecurringJobManager();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tariffs:SyncCron"] = "0 0 1 * *",
                ["Tariffs:TimeZoneId"] = "America/Sao_Paulo"
            })
            .Build();

        HangfireJobRegistration.RegisterRecurringJobs(manager, configuration);

        var (_, _, cron, options) = Registration(manager, "aneel-tariff-sync");
        Assert.Equal("0 0 1 * *", cron);
        Assert.Equal("America/Sao_Paulo", options.TimeZone?.Id);
    }

    [Fact]
    public void Registered_crm_job_carries_automatic_retry_policy()
    {
        var manager = new FakeRecurringJobManager();

        HangfireJobRegistration.RegisterRecurringJobs(manager, EmptyConfiguration());

        var job = Registration(manager, "crm-notifications").Job;
        var retry = job.Method.GetCustomAttribute<AutomaticRetryAttribute>();

        Assert.NotNull(retry);
        Assert.Equal(CrmNotificationJob.MaxRetryAttempts, retry.Attempts);
    }

    [Fact]
    public void Registered_aneel_job_carries_automatic_retry_policy()
    {
        var manager = new FakeRecurringJobManager();

        HangfireJobRegistration.RegisterRecurringJobs(manager, EmptyConfiguration());

        var job = Registration(manager, "aneel-tariff-sync").Job;
        var retry = job.Method.GetCustomAttribute<AutomaticRetryAttribute>();

        // O retry da sincronização ANEEL é configurável e aplicado via filtro global,
        // não como atributo de método (evita política duplicada).
        Assert.Null(retry);
    }

    [Fact]
    public void RegisterAneelRetryFilter_registers_global_filter_with_configured_attempts()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Tariffs:MaxRetries"] = "5" })
            .Build();

        try
        {
            var filter = HangfireJobRegistration.RegisterAneelRetryFilter(configuration);

            Assert.Equal(5, filter.Attempts);
            Assert.Equal(new[] { 60, 120, 240, 480, 960 }, filter.DelaysInSeconds);
            Assert.True(GlobalJobFilters.Filters.Contains(filter));
        }
        finally
        {
            GlobalJobFilters.Filters.Remove<AneelTariffRetryFilter>();
        }
    }

    [Fact]
    public void RegisterAneelRetryFilter_is_idempotent()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Tariffs:MaxRetries"] = "4" })
            .Build();

        try
        {
            HangfireJobRegistration.RegisterAneelRetryFilter(configuration);
            HangfireJobRegistration.RegisterAneelRetryFilter(configuration);

            Assert.Equal(1, GlobalJobFilters.Filters.Count(f => f.Instance is AneelTariffRetryFilter));
        }
        finally
        {
            GlobalJobFilters.Filters.Remove<AneelTariffRetryFilter>();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Falls_back_to_five_minute_cron_when_configured_crm_cron_is_missing_or_blank(string? cron)
    {
        var manager = new FakeRecurringJobManager();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Crm:NotificationCron"] = cron })
            .Build();

        HangfireJobRegistration.RegisterRecurringJobs(manager, configuration);

        Assert.Equal("*/5 * * * *", Registration(manager, "crm-notifications").Cron);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Falls_back_to_monthly_cron_when_configured_aneel_cron_is_missing_or_blank(string? cron)
    {
        var manager = new FakeRecurringJobManager();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Tariffs:SyncCron"] = cron })
            .Build();

        HangfireJobRegistration.RegisterRecurringJobs(manager, configuration);

        Assert.Equal("0 3 1 * *", Registration(manager, "aneel-tariff-sync").Cron);
    }

    [Fact]
    public void Falls_back_to_sao_paulo_timezone_when_aneel_timezone_is_missing()
    {
        var manager = new FakeRecurringJobManager();

        HangfireJobRegistration.RegisterRecurringJobs(manager, EmptyConfiguration());

        Assert.Equal("America/Sao_Paulo", Registration(manager, "aneel-tariff-sync").Options.TimeZone?.Id);
    }
}
