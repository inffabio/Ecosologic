using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;

namespace Ecosologic.Infrastructure.Jobs;

public static class HangfireStorageConfiguration
{
    public const string DefaultSchema = "hangfire";
    public const string SchemaConfigKey = "Hangfire:StorageSchema";

    public static string ResolveSchema(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var schema = configuration[SchemaConfigKey];
        return string.IsNullOrWhiteSpace(schema) ? DefaultSchema : schema;
    }

    public static PostgreSqlStorageOptions BuildStorageOptions(IConfiguration configuration)
        => new() { SchemaName = ResolveSchema(configuration) };
}
