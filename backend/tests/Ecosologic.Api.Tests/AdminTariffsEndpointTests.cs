using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Ecosologic.Api.Controllers;
using Ecosologic.Application.Solar;
using Ecosologic.Infrastructure.Persistence;
using Ecosologic.Infrastructure.Solar;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Ecosologic.Api.Tests;

public sealed class AdminTariffsEndpointTests
{
    private const string JwtKey = "chave-secreta-com-tamanho-suficiente-para-hmac-sha256";
    private const string SourceUrl = "https://www.aneel.gov.br/relatorio-tarifas";

    private sealed class FakeImportService : IAneelTariffImportService
    {
        public Guid ReservedRunId { get; } = Guid.NewGuid();

        public Task<Guid> ImportAsync(AneelTariffSyncOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());

        public Task<Guid> ImportAsync(AneelTariffSyncOptions options, Guid runId, string? jobId, CancellationToken cancellationToken = default)
            => Task.FromResult(runId);

        public Task<Guid> ReserveRunAsync(AneelTariffSyncOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(ReservedRunId);

        public Task AttachJobIdAsync(Guid runId, string jobId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task FailRunAsync(Guid runId, string errorMessage, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) => "job-id";

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }

    private static async Task RunAsync(Func<HttpClient, Task> assertion, Action<EcosologicDbContext>? seed = null)
    {
        var (app, client) = await StartAppAsync();
        try
        {
            if (seed is not null)
            {
                using var scope = app.Services.CreateScope();
                seed(scope.ServiceProvider.GetRequiredService<EcosologicDbContext>());
            }

            await assertion(client);
        }
        finally
        {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:JwtKey"] = JwtKey,
            ["Tariffs:SourceUrl"] = SourceUrl
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
        builder.Services.AddAuthorization();
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(AdminTariffsController).Assembly);

        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<EcosologicDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        builder.Services.AddSingleton<IAneelTariffImportService>(new FakeImportService());
        builder.Services.AddSingleton<IBackgroundJobClient>(new FakeBackgroundJobClient());

        builder.WebHost.UseTestServer();

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

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
    public Task Post_sync_returns_unauthorized_when_anonymous() =>
        RunAsync(async client =>
        {
            var response = await client.PostAsync("/api/admin/tariffs/sync", content: null);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        });

    [Fact]
    public Task Post_sync_returns_forbidden_for_non_admin() =>
        RunAsync(async client =>
        {
            SetBearer(client, "User");
            var response = await client.PostAsync("/api/admin/tariffs/sync", content: null);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        });

    [Fact]
    public Task Post_sync_returns_accepted_for_admin() =>
        RunAsync(async client =>
        {
            SetBearer(client, "Admin");
            var response = await client.PostAsync("/api/admin/tariffs/sync", content: null);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        });

    [Fact]
    public Task Get_status_returns_unauthorized_when_anonymous() =>
        RunAsync(async client =>
        {
            var response = await client.GetAsync("/api/admin/tariffs/sync/status");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        });

    [Fact]
    public Task Get_status_returns_forbidden_for_non_admin() =>
        RunAsync(async client =>
        {
            SetBearer(client, "User");
            var response = await client.GetAsync("/api/admin/tariffs/sync/status");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        });

    [Fact]
    public Task Get_status_returns_ok_for_admin_when_run_exists() =>
        RunAsync(async client =>
        {
            SetBearer(client, "Admin");
            var response = await client.GetAsync("/api/admin/tariffs/sync/status");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }, seed: db =>
        {
            db.AneelTariffImports.Add(AneelTariffImportRecord.Create(Guid.NewGuid(), null, "{}", SourceUrl));
            db.SaveChanges();
        });
}
