using Microsoft.Extensions.Configuration;

namespace Ecosologic.Api.Configuration;

public static class DataProtectionKeys
{
    public const string DefaultPath = "/root/.aspnet/DataProtection-Keys";

    public static string Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var path = configuration["DataProtection:KeysPath"];
        return string.IsNullOrWhiteSpace(path) ? DefaultPath : path.Trim();
    }
}
