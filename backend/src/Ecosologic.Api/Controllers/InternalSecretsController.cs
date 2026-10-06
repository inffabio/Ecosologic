using System.Security.Cryptography;
using System.Text;
using Ecosologic.Infrastructure.Secrets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ecosologic.Api.Controllers;

[ApiController]
[Route("api/internal/secrets")]
[AllowAnonymous]
public sealed class InternalSecretsController(
    InternalSecretBroker broker,
    IOptions<InternalSecretsOptions> options) : ControllerBase
{
    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name, CancellationToken cancellationToken)
    {
        if (!HasValidToken(Request.Headers["X-Internal-Secrets-Token"]))
            return Unauthorized();

        try
        {
            var value = await broker.GetAsync(name, cancellationToken);
            Response.Headers.CacheControl = "no-store";
            return Ok(new { name, value });
        }
        catch (UnknownInternalSecretException)
        {
            return NotFound();
        }
    }

    private bool HasValidToken(string? suppliedToken)
    {
        var expected = options.Value.Token;
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(suppliedToken))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(suppliedToken));
    }
}
