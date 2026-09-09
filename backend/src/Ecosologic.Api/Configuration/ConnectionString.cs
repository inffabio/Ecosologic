using Microsoft.Extensions.Configuration;

namespace Ecosologic.Api.Configuration;

public static class ConnectionString
{
    public static string Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection é obrigatório. Defina a variável de ambiente ConnectionStrings__DefaultConnection.");

        return connectionString;
    }
}
