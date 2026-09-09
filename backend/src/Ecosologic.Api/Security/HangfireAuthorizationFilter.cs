using Hangfire.Dashboard;

namespace Ecosologic.Api.Security;

public sealed class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public const string AdminRole = "Admin";

    public bool Authorize(DashboardContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = context.GetHttpContext().User;

        return user.Identity?.IsAuthenticated == true && user.IsInRole(AdminRole);
    }
}
