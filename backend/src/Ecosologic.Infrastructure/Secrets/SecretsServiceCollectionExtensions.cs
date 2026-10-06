using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Ecosologic.Infrastructure.Secrets;

public static class SecretsServiceCollectionExtensions
{
    public static IServiceCollection AddInternalSecrets(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InternalSecretsOptions>(configuration.GetSection(InternalSecretsOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<InternalSecretsOptions>>().Value);
        services.AddSingleton<IOciSecretReader, OciSecretReader>();
        services.AddSingleton<InternalSecretBroker>();
        return services;
    }
}
