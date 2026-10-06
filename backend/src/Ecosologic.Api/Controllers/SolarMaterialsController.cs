using Ecosologic.Api.Contracts;
using Ecosologic.Domain.Solar;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Controllers;

[ApiController]
[Route("api/solar-materials")]
[Authorize(Roles = "Admin")]
public sealed class SolarMaterialsController(EcosologicDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(string? type, string? query, CancellationToken cancellationToken)
    {
        SolarMaterialType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<SolarMaterialType>(type, true, out var value) || !Enum.IsDefined(value))
                return BadRequest(new { error = "Tipo de material inválido." });
            parsedType = value;
        }

        var materials = await db.SolarMaterials.AsNoTracking()
            .Include(material => material.Prices)
            .Where(material => parsedType == null || material.Type == parsedType)
            .Where(material => string.IsNullOrWhiteSpace(query) || material.Brand.Contains(query!) || material.Model.Contains(query!))
            .OrderBy(material => material.Type)
            .ThenBy(material => material.Brand)
            .ThenBy(material => material.Model)
            .ToListAsync(cancellationToken);

        return Ok(materials.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SolarMaterialCreateRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SolarMaterialType>(request.Type, true, out var type) || !Enum.IsDefined(type))
            return BadRequest(new { error = "Tipo de material inválido.", field = nameof(request.Type) });
        if (request.InitialPrice.HasValue != request.PriceValidFrom.HasValue)
            return BadRequest(new { error = "Preço inicial e vigência devem ser informados juntos." });

        try
        {
            var material = SolarMaterial.Create(type, request.Brand, request.Model, request.PowerW, request.TechnicalDataJson, request.SourceUrl);
            if (request.InitialPrice is { } amount)
                material.AddPrice(amount, request.PriceValidFrom!.Value);
            var record = SolarMaterialRecord.Create(material);
            db.SolarMaterials.Add(record);
            await db.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = record.Id }, ToResponse(record));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message, field = exception.ParamName });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var material = await db.SolarMaterials.AsNoTracking().Include(item => item.Prices).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return material is null ? NotFound() : Ok(ToResponse(material));
    }

    [HttpPost("{id:guid}/prices")]
    public async Task<IActionResult> AddPrice(Guid id, [FromBody] SolarMaterialPriceRequest request, CancellationToken cancellationToken)
    {
        var material = await db.SolarMaterials.Include(item => item.Prices).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (material is null)
            return NotFound();

        try
        {
            var price = material.AddPrice(request.Amount, request.ValidFrom);
            db.SolarMaterialPrices.Add(price);
            await db.SaveChangesAsync(cancellationToken);
            return Ok(ToResponse(material));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new { error = exception.Message, field = exception.ParamName });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    private static SolarMaterialResponse ToResponse(SolarMaterialRecord material) =>
        new(material.Id, material.Type.ToString(), material.Brand, material.Model, material.PowerW,
            material.TechnicalDataJson, material.SourceUrl,
            material.Prices.Where(price => price.ValidFrom <= DateTimeOffset.UtcNow).OrderByDescending(price => price.ValidFrom).Select(price => (decimal?)price.Amount).FirstOrDefault(),
            material.CreatedAt);
}
