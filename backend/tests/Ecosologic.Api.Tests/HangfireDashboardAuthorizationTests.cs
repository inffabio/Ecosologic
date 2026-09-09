using System.Security.Claims;
using Ecosologic.Api.Security;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Storage;
using Microsoft.AspNetCore.Http;

namespace Ecosologic.Api.Tests;

public class HangfireDashboardAuthorizationTests
{
    private sealed class FakeJobStorage : JobStorage
    {
        public override IMonitoringApi GetMonitoringApi() => throw new NotSupportedException();
        public override IStorageConnection GetConnection() => throw new NotSupportedException();
    }

    private static readonly HangfireAuthorizationFilter Filter = new();

    private static DashboardContext CreateContext(ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext { User = principal };
        return new AspNetCoreDashboardContext(
            new FakeJobStorage(),
            new DashboardOptions { IgnoreAntiforgeryToken = true },
            httpContext);
    }

    [Fact]
    public void Rejects_unauthenticated_principal()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.False(Filter.Authorize(CreateContext(principal)));
    }

    [Fact]
    public void Rejects_authenticated_principal_without_admin_role()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "user@example.com")], "test");
        var principal = new ClaimsPrincipal(identity);

        Assert.False(Filter.Authorize(CreateContext(principal)));
    }

    [Fact]
    public void Rejects_authenticated_principal_with_non_admin_role()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "user@example.com"), new Claim(ClaimTypes.Role, "User")],
            "test");
        var principal = new ClaimsPrincipal(identity);

        Assert.False(Filter.Authorize(CreateContext(principal)));
    }

    [Fact]
    public void Accepts_authenticated_admin_principal()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "admin@ecosologic.com.br"), new Claim(ClaimTypes.Role, "Admin")],
            "test");
        var principal = new ClaimsPrincipal(identity);

        Assert.True(Filter.Authorize(CreateContext(principal)));
    }

    [Fact]
    public void Rejects_null_context()
    {
        Assert.Throws<ArgumentNullException>(() => Filter.Authorize(null!));
    }
}
