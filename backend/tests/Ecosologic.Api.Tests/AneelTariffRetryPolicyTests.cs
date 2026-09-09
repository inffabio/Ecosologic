using Ecosologic.Infrastructure.Jobs;
using Microsoft.Extensions.Configuration;

namespace Ecosologic.Api.Tests;

public class AneelTariffRetryPolicyTests
{
    private static IConfiguration Config(string? maxRetries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AneelTariffRetryPolicy.MaxRetriesConfigKey] = maxRetries
            })
            .Build();

    [Fact]
    public void Resolve_uses_safe_default_when_config_missing()
    {
        var policy = AneelTariffRetryPolicy.Resolve(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());

        Assert.Equal(AneelTariffRetryPolicy.DefaultMaxRetries, policy.MaxRetries);
        Assert.Equal(new[] { 60, 120, 240 }, policy.DelaysInSeconds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.5")]
    public void Resolve_falls_back_to_default_when_value_is_unparseable(string? value)
    {
        var policy = AneelTariffRetryPolicy.Resolve(Config(value));

        Assert.Equal(AneelTariffRetryPolicy.DefaultMaxRetries, policy.MaxRetries);
        Assert.Equal(new[] { 60, 120, 240 }, policy.DelaysInSeconds);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-3")]
    [InlineData("-100")]
    public void Resolve_falls_back_to_default_when_value_is_negative(string value)
    {
        var policy = AneelTariffRetryPolicy.Resolve(Config(value));

        Assert.Equal(AneelTariffRetryPolicy.DefaultMaxRetries, policy.MaxRetries);
        Assert.Equal(new[] { 60, 120, 240 }, policy.DelaysInSeconds);
    }

    [Fact]
    public void Resolve_reads_configured_max_retries()
    {
        var policy = AneelTariffRetryPolicy.Resolve(Config("5"));

        Assert.Equal(5, policy.MaxRetries);
        Assert.Equal(new[] { 60, 120, 240, 480, 960 }, policy.DelaysInSeconds);
    }

    [Fact]
    public void Resolve_clamps_attempts_to_upper_bound()
    {
        var policy = AneelTariffRetryPolicy.Resolve(Config("1000"));

        Assert.Equal(AneelTariffRetryPolicy.MaxRetriesLimit, policy.MaxRetries);
    }

    [Fact]
    public void Resolve_allows_zero_retries()
    {
        var policy = AneelTariffRetryPolicy.Resolve(Config("0"));

        Assert.Equal(0, policy.MaxRetries);
        Assert.Empty(policy.DelaysInSeconds);
    }

    [Fact]
    public void Resolve_keeps_delays_bounded()
    {
        var policy = AneelTariffRetryPolicy.Resolve(Config(AneelTariffRetryPolicy.MaxRetriesLimit.ToString()));

        Assert.Equal(AneelTariffRetryPolicy.MaxRetriesLimit, policy.DelaysInSeconds.Count);
        Assert.All(policy.DelaysInSeconds, delay => Assert.InRange(delay, 1, AneelTariffRetryPolicy.MaxDelaySeconds));
        Assert.Equal(AneelTariffRetryPolicy.MaxDelaySeconds, policy.DelaysInSeconds[^1]);
    }
}
