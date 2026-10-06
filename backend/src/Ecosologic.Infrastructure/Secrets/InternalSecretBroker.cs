using System.Collections.Concurrent;

namespace Ecosologic.Infrastructure.Secrets;

public sealed class UnknownInternalSecretException(string name) : Exception($"Secret '{name}' is not allowed.");

public sealed class InternalSecretBroker(IOciSecretReader reader, InternalSecretsOptions options)
{
    private readonly ConcurrentDictionary<string, CacheEntry> cache = new(StringComparer.Ordinal);

    public async Task<string> GetAsync(string name, CancellationToken cancellationToken)
    {
        if (!options.SecretNames.TryGetValue(name, out var secretName) || string.IsNullOrWhiteSpace(secretName))
            throw new UnknownInternalSecretException(name);

        if (cache.TryGetValue(name, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return cached.Value;

        var value = await reader.ReadAsync(secretName, cancellationToken);
        cache[name] = new CacheEntry(value, DateTimeOffset.UtcNow.Add(options.CacheDuration));
        return value;
    }

    private sealed record CacheEntry(string Value, DateTimeOffset ExpiresAt);
}
