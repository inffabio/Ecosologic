using Ecosologic.Infrastructure.Jobs;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecosologic.Api.Configuration;

public static class HangfireServiceRegistration
{
    public static void AddHangfireServices(
        IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
        => AddHangfireServices(
            services,
            HangfireStorageConfiguration.BuildStorageOptions(configuration),
            connectionString);

    public static void AddHangfireServices(
        IServiceCollection services,
        PostgreSqlStorageOptions options,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Hangfire connection string is required.", nameof(connectionString));

        services.AddHangfire(hangfire => hangfire
            .UsePostgreSqlStorage(
                factory => factory.UseNpgsqlConnection(connectionString),
                options));
        services.AddHangfireServer();
    }
}
