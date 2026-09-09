using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ecosologic.Api.Security;
using Ecosologic.Domain.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace Ecosologic.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IConfiguration configuration) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimiter.PolicyName)]
    [AllowAnonymous]
    public IActionResult Login(LoginRequest request)
    {
        var adminEmail = configuration["Auth:AdminEmail"];
        var adminPassword = configuration["Auth:AdminPassword"];
        var jwtKey = configuration["Auth:JwtKey"];

        if (string.IsNullOrWhiteSpace(adminEmail)
            || string.IsNullOrWhiteSpace(adminPassword)
            || string.IsNullOrWhiteSpace(jwtKey))
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Autenticação não configurada.");
        }

        var validator = new AdminCredentialValidator(adminEmail, adminPassword);
        if (!validator.Validate(request.Email, request.Password))
            return Unauthorized(new { message = "Credenciais inválidas." });

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "ecosologic",
            audience: "ecosologic",
            claims: [new Claim(ClaimTypes.Name, adminEmail), new Claim(ClaimTypes.Role, "Admin")],
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials);

        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
    }
}

public sealed record LoginRequest(string? Email, string? Password);
