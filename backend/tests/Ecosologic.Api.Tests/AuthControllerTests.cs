using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Ecosologic.Api.Controllers;
using Ecosologic.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;

namespace Ecosologic.Api.Tests;

public sealed class AuthControllerTests
{
    private const string Email = "admin@ecosologic.com.br";
    private const string Password = "s3nh4-f0rte";

    private static IConfiguration Config(
        string? email = null,
        string? password = null,
        string? jwtKey = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:AdminEmail"] = email ?? Email,
            ["Auth:AdminPassword"] = password ?? Password,
            ["Auth:JwtKey"] = jwtKey ?? "chave-secreta-com-tamanho-suficiente-para-hmac-sha256"
        }).Build();

    [Fact]
    public void Login_endpoint_is_explicitly_anonymous()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Login));
        Assert.NotEmpty(method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
    }

    [Fact]
    public void Login_endpoint_has_rate_limiting_policy_named_login()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Login));

        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true));
        Assert.Equal("login", ((EnableRateLimitingAttribute)attribute).PolicyName);
    }

    [Fact]
    public void Login_rate_limit_allows_five_requests_per_minute()
    {
        var options = AuthRateLimiter.LoginOptions();

        Assert.Equal(5, options.PermitLimit);
        Assert.Equal(TimeSpan.FromMinutes(1), options.Window);
        Assert.Equal(0, options.QueueLimit);
    }

    [Fact]
    public void Login_returns_token_with_valid_issuer_audience_role_and_lifetime()
    {
        var controller = new AuthController(Config());

        var result = controller.Login(new LoginRequest(Email, Password));

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        var token = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!["token"];

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("ecosologic", jwt.Issuer);
        Assert.Contains("ecosologic", jwt.Audiences);
        Assert.Contains(jwt.Claims, c => c.Value == "Admin");
        Assert.True(jwt.ValidTo > DateTime.UtcNow.AddHours(1));
        Assert.True(jwt.ValidTo <= DateTime.UtcNow.AddHours(3));
    }

    [Fact]
    public void Login_returns_unauthorized_for_wrong_credentials()
    {
        var controller = new AuthController(Config());

        var result = controller.Login(new LoginRequest(Email, "senha-errada"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void Login_returns_unauthorized_for_unknown_email()
    {
        var controller = new AuthController(Config());

        var result = controller.Login(new LoginRequest("outro@ecosologic.com.br", Password));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
