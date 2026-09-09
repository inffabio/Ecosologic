using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Ecosologic.Api.Configuration;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace Ecosologic.Api.Tests;

public sealed class HangfireDashboardRouteTests
{
    private const string JwtKey = "chave-secreta-com-tamanho-suficiente-para-hmac-sha256";

    private static async Task RunAsync(bool dashboardEnabled, Func<HttpClient, Task> assertion)
    {
        var (app, client) = await StartAppAsync(dashboardEnabled);
        try
        {
            await assertion(client);
        }
        finally
        {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartAppAsync(bool dashboardEnabled)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [HangfireDashboardConfiguration.DashboardEnabledConfigKey] = dashboardEnabled ? "true" : "false",
            ["Auth:JwtKey"] = JwtKey
        });

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)),
                ValidateIssuer = true,
                ValidIssuer = "ecosologic",
                ValidateAudience = true,
                ValidAudience = "ecosologic",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            });

        builder.Services.AddHangfire(_ => { });
        builder.Services.AddSingleton<JobStorage>(new MemoryStorage());
        builder.Services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        builder.WebHost.UseTestServer();

        var app = builder.Build();

        app.UseAuthentication();
        HangfireDashboardConfiguration.MapDashboard(app);
        app.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });

        await app.StartAsync();

        return (app, app.GetTestClient());
    }

    private static string CreateToken(string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "user@ecosologic.com.br"),
            new(ClaimTypes.Role, role)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "ecosologic",
            audience: "ecosologic",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void SetBearer(HttpClient client, string role) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(role));

    [Fact]
    public Task Dashboard_is_not_mapped_when_disabled() =>
        RunAsync(dashboardEnabled: false, async client =>
        {
            var response = await client.GetAsync("/hangfire");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });

    [Fact]
    public Task Dashboard_returns_unauthorized_when_unauthenticated() =>
        RunAsync(dashboardEnabled: true, async client =>
        {
            var response = await client.GetAsync("/hangfire");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        });

    [Fact]
    public Task Dashboard_returns_forbidden_for_non_admin() =>
        RunAsync(dashboardEnabled: true, async client =>
        {
            SetBearer(client, "User");

            var response = await client.GetAsync("/hangfire");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        });

    [Fact]
    public Task Dashboard_is_accessible_for_admin() =>
        RunAsync(dashboardEnabled: true, async client =>
        {
            SetBearer(client, "Admin");

            var response = await client.GetAsync("/hangfire");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

    [Fact]
    public Task Dashboard_subroute_is_accessible_for_admin() =>
        RunAsync(dashboardEnabled: true, async client =>
        {
            SetBearer(client, "Admin");

            var response = await client.GetAsync("/hangfire/jobs/enqueued");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

    [Fact]
    public Task Dashboard_subroute_is_forbidden_for_non_admin() =>
        RunAsync(dashboardEnabled: true, async client =>
        {
            SetBearer(client, "User");

            var response = await client.GetAsync("/hangfire/jobs/enqueued");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        });
}
