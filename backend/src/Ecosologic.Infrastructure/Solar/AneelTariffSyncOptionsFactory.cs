using Ecosologic.Application.Solar;
using Microsoft.Extensions.Configuration;

namespace Ecosologic.Infrastructure.Solar;

/// <summary>
/// Constrói a configuração de uma sincronização ANEEL a partir da configuração da
/// aplicação. A URL da fonte é obrigatória; o timeout é opcional com o padrão
/// definido em <see cref="AneelTariffSyncOptions"/>.
/// </summary>
public static class AneelTariffSyncOptionsFactory
{
    public const string SourceUrlConfigKey = "Tariffs:SourceUrl";
    public const string HttpTimeoutSecondsConfigKey = "Tariffs:HttpTimeoutSeconds";

    public static AneelTariffSyncOptions Build(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var sourceUrl = configuration[SourceUrlConfigKey] ?? "";

        var httpTimeout = AneelTariffSyncOptions.DefaultHttpTimeout;
        if (int.TryParse(configuration[HttpTimeoutSecondsConfigKey], out var seconds) && seconds > 0)
            httpTimeout = TimeSpan.FromSeconds(seconds);

        return new AneelTariffSyncOptions(sourceUrl, httpTimeout);
    }
}
