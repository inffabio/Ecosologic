using Ecosologic.Api.Configuration;
using Microsoft.Extensions.Configuration;

namespace Ecosologic.Api.Tests;

public class ConnectionStringTests
{
    [Fact]
    public void Resolve_throws_clearly_when_default_connection_is_missing()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() => ConnectionString.Resolve(configuration));

        Assert.Contains("DefaultConnection", exception.Message);
    }

    [Fact]
    public void Resolve_throws_clearly_when_default_connection_is_blank()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "   "
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => ConnectionString.Resolve(configuration));
    }

    [Fact]
    public void Resolve_returns_configured_connection_string()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=test;Port=5432"
            })
            .Build();

        Assert.Equal("Host=test;Port=5432", ConnectionString.Resolve(configuration));
    }
}
