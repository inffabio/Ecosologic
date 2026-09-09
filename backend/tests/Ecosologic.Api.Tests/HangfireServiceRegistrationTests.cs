using System.Reflection;
using Ecosologic.Api.Configuration;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ecosologic.Api.Tests;

public class HangfireServiceRegistrationTests
{
    private static IConfiguration EmptyConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    [Fact]
    public void AddHangfireServices_registers_hangfire_and_server_services()
    {
        var services = new ServiceCollection();

        HangfireServiceRegistration.AddHangfireServices(
            services,
            EmptyConfiguration(),
            "Host=localhost;Database=ecosologic");

        Assert.Contains(services, d => d.ServiceType == typeof(JobStorage));
        Assert.Contains(services, d => d.ServiceType == typeof(IGlobalConfiguration));
        Assert.Contains(services, d => d.ServiceType == typeof(IRecurringJobManager));
        Assert.Contains(services, d => d.ServiceType == typeof(IBackgroundJobClient));
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddHangfireServices_wires_postgresql_storage_with_configured_schema()
    {
        var services = new ServiceCollection();
        var options = new PostgreSqlStorageOptions
        {
            SchemaName = "hangfire",
            PrepareSchemaIfNecessary = false
        };

        HangfireServiceRegistration.AddHangfireServices(
            services,
            options,
            "Host=localhost;Database=ecosologic;Username=postgres;Password=postgres");

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<JobStorage>();

        Assert.IsType<PostgreSqlStorage>(storage);
        Assert.Equal("hangfire", ReadSchema(storage));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddHangfireServices_rejects_blank_connection_string(string? connectionString)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            HangfireServiceRegistration.AddHangfireServices(services, EmptyConfiguration(), connectionString!));
    }

    private static string ReadSchema(JobStorage storage)
    {
        var property = storage.GetType().GetProperty("Options", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var options = (PostgreSqlStorageOptions)property.GetValue(storage)!;
        return options.SchemaName;
    }
}
