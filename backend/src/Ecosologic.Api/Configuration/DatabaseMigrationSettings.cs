using System.Globalization;
using Ecosologic.Infrastructure.Persistence;

namespace Ecosologic.Api.Configuration;

public static class DatabaseMigrationSettings
{
    public static TimeSpan ParseBackoff(string? value)
    {
        if (double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var seconds) &&
            double.IsFinite(seconds) &&
            seconds > 0 &&
            seconds <= DatabaseMigrator.MaxBaseDelay.TotalSeconds)
        {
            var backoff = TimeSpan.FromSeconds(seconds);
            if (backoff > TimeSpan.Zero)
                return backoff;
        }

        return DatabaseMigrator.DefaultBaseDelay;
    }
}
