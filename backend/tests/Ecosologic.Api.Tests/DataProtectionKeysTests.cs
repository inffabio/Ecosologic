using Ecosologic.Api.Configuration;
using Microsoft.Extensions.Configuration;

namespace Ecosologic.Api.Tests;

public class DataProtectionKeysTests
{
    [Fact]
    public void Resolve_returns_the_container_default_path_when_configuration_is_missing()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Equal("/root/.aspnet/DataProtection-Keys", DataProtectionKeys.Resolve(configuration));
    }

    [Fact]
    public void Resolve_returns_the_container_default_path_when_configuration_is_blank()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = "   "
            })
            .Build();

        Assert.Equal("/root/.aspnet/DataProtection-Keys", DataProtectionKeys.Resolve(configuration));
    }

    [Fact]
    public void Resolve_returns_the_configured_path()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = "/custom/data-protection-keys"
            })
            .Build();

        Assert.Equal("/custom/data-protection-keys", DataProtectionKeys.Resolve(configuration));
    }
}
