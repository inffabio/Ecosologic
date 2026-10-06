namespace Ecosologic.Infrastructure.Secrets;

public sealed class InternalSecretsOptions
{
    public const string SectionName = "InternalSecrets";

    public string Token { get; set; } = string.Empty;

    public int CacheSeconds { get; set; } = 300;

    public string VaultOcid { get; set; } = string.Empty;

    public Dictionary<string, string> SecretNames { get; set; } = new(StringComparer.Ordinal);

    public TimeSpan CacheDuration => TimeSpan.FromSeconds(Math.Max(1, CacheSeconds));
}
