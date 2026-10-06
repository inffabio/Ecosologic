using Ecosologic.Infrastructure.Secrets;

namespace Ecosologic.Api.Tests;

public sealed class InternalSecretBrokerTests
{
    [Fact]
    public async Task ReturnsAllowedSecretAndCachesIt()
    {
        var reader = new StubSecretReader("tavily-value");
        var broker = new InternalSecretBroker(
            reader,
            new InternalSecretsOptions
            {
                SecretNames = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["TAVILY_API_KEY"] = "TavilyApiKey"
                },
                CacheSeconds = 300
            });

        var first = await broker.GetAsync("TAVILY_API_KEY", CancellationToken.None);
        var second = await broker.GetAsync("TAVILY_API_KEY", CancellationToken.None);

        Assert.Equal("tavily-value", first);
        Assert.Equal(first, second);
        Assert.Equal(1, reader.CallCount);
        Assert.Equal("TavilyApiKey", reader.LastSecretName);
    }

    [Fact]
    public async Task RejectsSecretOutsideAllowlist()
    {
        var reader = new StubSecretReader("should-not-be-read");
        var broker = new InternalSecretBroker(
            reader,
            new InternalSecretsOptions
            {
                SecretNames = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["TAVILY_API_KEY"] = "TavilyApiKey"
                }
            });

        await Assert.ThrowsAsync<UnknownInternalSecretException>(() =>
            broker.GetAsync("DATABASE_PASSWORD", CancellationToken.None));

        Assert.Equal(0, reader.CallCount);
    }

    private sealed class StubSecretReader(string value) : IOciSecretReader
    {
        public int CallCount { get; private set; }

        public string? LastSecretName { get; private set; }

        public Task<string> ReadAsync(string secretName, CancellationToken cancellationToken)
        {
            CallCount++;
            LastSecretName = secretName;
            return Task.FromResult(value);
        }
    }
}
