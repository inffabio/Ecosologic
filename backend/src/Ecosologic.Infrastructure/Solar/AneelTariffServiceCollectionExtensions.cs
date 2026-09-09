using Ecosologic.Application.Solar;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Ecosologic.Infrastructure.Solar;

public static class AneelTariffServiceCollectionExtensions
{
    /// <summary>
    /// Registra a fonte ANEEL como um cliente HTTP tipado. O <see cref="HttpClient"/>
    /// subjacente é gerenciado pelo <c>IHttpClientFactory</c>; a URL de destino é
    /// definida por <see cref="AneelTariffSyncOptions"/> a cada sincronização.
    /// </summary>
    public static IServiceCollection AddAneelTariffSource(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient<IAneelTariffSource, AneelTariffClient>();

        return services;
    }

    /// <summary>
    /// Registra o normalizador e o serviço de importação ANEEL. O serviço é escopado
    /// (depende do <c>DbContext</c>) e montado explicitamente para não expor o
    /// <c>jobId</c> opcional ao contêiner de DI.
    /// </summary>
    public static IServiceCollection AddAneelTariffImport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<AneelTariffNormalizer>();
        services.AddScoped<IAneelTariffImportService>(sp => new AneelTariffImportService(
            sp.GetRequiredService<EcosologicDbContext>(),
            sp.GetRequiredService<IAneelTariffSource>(),
            sp.GetRequiredService<AneelTariffNormalizer>(),
            sp.GetRequiredService<TariffCatalog>()));

        return services;
    }
}
