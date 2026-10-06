using Ecosologic.Api.LeadEnrichment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosologic.Api.Controllers;

[ApiController]
[Route("api/lead-enrichment")]
[Authorize(Roles = "Admin")]
public sealed class RegionalLeadEnrichmentController(IRegionalLeadEnrichmentClient client) : ControllerBase
{
    [HttpPost("regional")]
    public async Task<IActionResult> Run(RegionalLeadEnrichmentRequest request, CancellationToken cancellationToken)
    {
        var cities = request.Cities
            .Select(city => city?.Trim() ?? "")
            .Where(city => city.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (cities.Length == 0)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["cities"] = ["Informe ao menos uma cidade."] }));
        if (cities.Length > 20)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["cities"] = ["Informe no máximo 20 cidades por execução."] }));
        if (request.MaxLeadsPerCity is < 1 or > 10)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["maxLeadsPerCity"] = ["O limite deve estar entre 1 e 10."] }));

        try
        {
            return Ok(await client.RunAsync(new RegionalLeadEnrichmentRequest(cities, request.MaxLeadsPerCity), cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (HttpRequestException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
