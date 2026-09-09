using Ecosologic.Api.Configuration;
using Microsoft.Extensions.Configuration;

namespace Ecosologic.Api.Tests;

public class HangfireDashboardConfigurationTests
{
    private const string ComposeFileName = "docker-compose.server.yml";

    private static IConfiguration ConfigurationWith(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static string LocateComposeFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ComposeFileName);
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Nao foi possivel localizar {ComposeFileName} a partir de {AppContext.BaseDirectory}.");
    }

    [Fact]
    public void IsDashboardEnabled_is_false_by_default()
    {
        var configuration = ConfigurationWith(new Dictionary<string, string?>());

        Assert.False(HangfireDashboardConfiguration.IsDashboardEnabled(configuration));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void IsDashboardEnabled_is_true_when_enabled(string value)
    {
        var configuration = ConfigurationWith(new Dictionary<string, string?>
        {
            [HangfireDashboardConfiguration.DashboardEnabledConfigKey] = value
        });

        Assert.True(HangfireDashboardConfiguration.IsDashboardEnabled(configuration));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("yes")]
    public void IsDashboardEnabled_is_false_when_not_explicitly_true(string value)
    {
        var configuration = ConfigurationWith(new Dictionary<string, string?>
        {
            [HangfireDashboardConfiguration.DashboardEnabledConfigKey] = value
        });

        Assert.False(HangfireDashboardConfiguration.IsDashboardEnabled(configuration));
    }

    [Fact]
    public void IsDashboardEnabled_rejects_null_configuration()
    {
        Assert.Throws<ArgumentNullException>(() => HangfireDashboardConfiguration.IsDashboardEnabled(null!));
    }

    [Fact]
    public void Dashboard_path_is_hangfire()
    {
        Assert.Equal("/hangfire", HangfireDashboardConfiguration.DashboardPath);
    }

    [Fact]
    public void Compose_maps_dashboard_enabled_from_environment_with_false_default()
    {
        var compose = File.ReadAllText(LocateComposeFile());

        Assert.Contains("Hangfire__DashboardEnabled", compose);
        Assert.Contains("HANGFIRE_DASHBOARD_ENABLED:-false", compose);
    }
}
