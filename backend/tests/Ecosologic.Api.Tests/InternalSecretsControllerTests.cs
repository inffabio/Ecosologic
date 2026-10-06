using Ecosologic.Api.Controllers;
using Ecosologic.Infrastructure.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ecosologic.Api.Tests;

public sealed class InternalSecretsControllerTests
{
    [Fact]
    public async Task RejectsMissingInternalToken()
    {
        var controller = CreateController("expected-token");

        var result = await controller.Get("TAVILY_API_KEY", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task ReturnsSecretForValidInternalToken()
    {
        var controller = CreateController("expected-token");
        controller.ControllerContext.HttpContext.Request.Headers["X-Internal-Secrets-Token"] = "expected-token";

        var result = await controller.Get("TAVILY_API_KEY", CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("tavily-value", response.Value!.ToString());
    }

    private static InternalSecretsController CreateController(string token)
    {
        var broker = new InternalSecretBroker(
            new StubSecretReader(),
            new InternalSecretsOptions
            {
                Token = token,
                SecretNames = new Dictionary<string, string>
                {
                    ["TAVILY_API_KEY"] = "TAVILY_API_KEY"
                }
            });
        var controller = new InternalSecretsController(
            broker,
            Options.Create(new InternalSecretsOptions { Token = token }));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private sealed class StubSecretReader : IOciSecretReader
    {
        public Task<string> ReadAsync(string secretName, CancellationToken cancellationToken) =>
            Task.FromResult("tavily-value");
    }
}
