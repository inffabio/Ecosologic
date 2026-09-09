using Ecosologic.Infrastructure.Jobs;
using Microsoft.Extensions.Configuration;

namespace Ecosologic.Api.Tests;

public class HangfireStorageConfigurationTests
{
    private static IConfiguration EmptyConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    private static IConfiguration ConfigurationWith(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void ResolveSchema_returns_hangfire_by_default()
    {
        Assert.Equal("hangfire", HangfireStorageConfiguration.ResolveSchema(EmptyConfiguration()));
    }

    [Fact]
    public void ResolveSchema_uses_configured_schema()
    {
        var configuration = ConfigurationWith(new Dictionary<string, string?>
        {
            ["Hangfire:StorageSchema"] = "jobs"
        });

        Assert.Equal("jobs", HangfireStorageConfiguration.ResolveSchema(configuration));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveSchema_treats_missing_or_blank_as_default(string? schema)
    {
        var configuration = ConfigurationWith(new Dictionary<string, string?>
        {
            ["Hangfire:StorageSchema"] = schema
        });

        Assert.Equal("hangfire", HangfireStorageConfiguration.ResolveSchema(configuration));
    }

    [Fact]
    public void BuildStorageOptions_sets_isolated_schema_and_enables_prepare()
    {
        var configuration = ConfigurationWith(new Dictionary<string, string?>
        {
            ["Hangfire:StorageSchema"] = "hangfire_jobs"
        });

        var options = HangfireStorageConfiguration.BuildStorageOptions(configuration);

        Assert.Equal("hangfire_jobs", options.SchemaName);
        Assert.True(options.PrepareSchemaIfNecessary);
    }
}
