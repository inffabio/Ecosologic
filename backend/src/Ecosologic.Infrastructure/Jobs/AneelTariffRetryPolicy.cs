using Microsoft.Extensions.Configuration;

namespace Ecosologic.Infrastructure.Jobs;

/// <summary>
/// Política de retry da sincronização ANEEL. O número de tentativas é configurável via
/// <c>Tariffs:MaxRetries</c> com um padrão seguro e validação (limites inferior e
/// superior), enquanto os atrasos de backoff exponencial permanecem limitados a um teto.
/// </summary>
public sealed record AneelTariffRetryPolicy(int MaxRetries, IReadOnlyList<int> DelaysInSeconds)
{
    public const string MaxRetriesConfigKey = "Tariffs:MaxRetries";
    public const int DefaultMaxRetries = 3;
    public const int MinRetries = 0;
    public const int MaxRetriesLimit = 10;
    public const int MaxDelaySeconds = 3600;
    public const int InitialDelaySeconds = 60;

    public static AneelTariffRetryPolicy Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var maxRetries = DefaultMaxRetries;
        var raw = configuration[MaxRetriesConfigKey];
        if (int.TryParse(raw, out var parsed))
        {
            if (parsed < MinRetries)
                maxRetries = DefaultMaxRetries;
            else
                maxRetries = Math.Min(parsed, MaxRetriesLimit);
        }

        return new AneelTariffRetryPolicy(maxRetries, BuildDelays(maxRetries));
    }

    private static int[] BuildDelays(int maxRetries)
    {
        if (maxRetries == 0)
            return [];

        var delays = new int[maxRetries];
        var seconds = InitialDelaySeconds;
        for (var i = 0; i < maxRetries; i++)
        {
            delays[i] = Math.Min(seconds, MaxDelaySeconds);
            seconds = Math.Min(seconds * 2, MaxDelaySeconds);
        }

        return delays;
    }
}
