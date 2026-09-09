using Ecosologic.Api.Security;
using Hangfire;
using Microsoft.Extensions.Configuration;

namespace Ecosologic.Api.Configuration;

public static class HangfireDashboardConfiguration
{
    public const string DashboardEnabledConfigKey = "Hangfire:DashboardEnabled";
    public const string DashboardPath = "/hangfire";

    public static bool IsDashboardEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return bool.TryParse(configuration[DashboardEnabledConfigKey], out var enabled) && enabled;
    }

    public static void MapDashboard(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!IsDashboardEnabled(app.Configuration))
            return;

        app.UseHangfireDashboard(DashboardPath, new DashboardOptions
        {
            Authorization = [new HangfireAuthorizationFilter()]
        });
    }
}
