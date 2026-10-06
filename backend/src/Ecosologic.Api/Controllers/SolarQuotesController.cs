using System.Text.Json;
using Ecosologic.Api.Contracts;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Controllers;

[ApiController]
[Route("api/solar-quotes")]
[Authorize(Roles = "Admin")]
public sealed class SolarQuotesController(EcosologicDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SolarQuoteCreateRequest request, CancellationToken cancellationToken)
    {
        var sizing = await db.SolarSizings.SingleOrDefaultAsync(item => item.Id == request.SizingId, cancellationToken);
        if (sizing is null)
            return NotFound(new { error = "Dimensionamento não encontrado." });

        try
        {
            var items = await PriceItems(request.Items, cancellationToken);
            var totalCost = items.Sum(item => item.Quantity * item.UnitPriceSnapshot);
            var totalPrice = CalculatePrice(totalCost, request.MarginPercent, request.TaxPercent);
            var quote = SolarQuoteRecord.Create(
                Guid.NewGuid(), sizing, request.SupplierName, JsonSerializer.Serialize(items), totalCost, request.MarginPercent,
                request.TaxPercent, totalPrice, request.ConditionsJson, request.ValidUntil,
                JsonSerializer.Serialize(new { pricedAt = DateTimeOffset.UtcNow }));
            db.SolarQuotes.Add(quote);
            await db.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = quote.Id }, ToResponse(quote));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message, field = exception.ParamName });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var quote = await db.SolarQuotes.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return quote is null ? NotFound() : Ok(ToResponse(quote));
    }

    [HttpPost("{id:guid}/refresh-prices")]
    public async Task<IActionResult> RefreshPrices(Guid id, CancellationToken cancellationToken)
    {
        var quote = await db.SolarQuotes.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (quote is null)
            return NotFound();

        try
        {
            var previousItems = JsonSerializer.Deserialize<List<QuoteItemSnapshot>>(quote.ItemsJson)
                ?? throw new InvalidOperationException("Itens da cotação são inválidos.");
            var refreshed = await PriceItems(previousItems.Select(item => new SolarQuoteItemRequest { MaterialId = item.MaterialId, Quantity = item.Quantity }).ToList(), cancellationToken);
            var totalCost = refreshed.Sum(item => item.Quantity * item.UnitPriceSnapshot);
            var totalPrice = CalculatePrice(totalCost, quote.MarginPercent, quote.TaxPercent);
            quote.RefreshPricing(JsonSerializer.Serialize(refreshed), totalCost, totalPrice, User.Identity?.Name ?? "system");
            await db.SaveChangesAsync(cancellationToken);
            return Ok(ToResponse(quote));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    private async Task<List<QuoteItemSnapshot>> PriceItems(IReadOnlyList<SolarQuoteItemRequest> requests, CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
            throw new ArgumentException("A cotação exige ao menos um item.", nameof(requests));
        if (requests.Any(item => item.Quantity <= 0))
            throw new ArgumentOutOfRangeException(nameof(requests), "A quantidade dos itens deve ser positiva.");

        var ids = requests.Select(item => item.MaterialId).Distinct().ToList();
        var materials = await db.SolarMaterials.Include(item => item.Prices).Where(item => ids.Contains(item.Id)).ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var items = new List<QuoteItemSnapshot>();
        foreach (var request in requests)
        {
            var material = materials.SingleOrDefault(item => item.Id == request.MaterialId)
                ?? throw new InvalidOperationException("Material da cotação não encontrado.");
            var price = material.Prices.Where(item => item.ValidFrom <= now).OrderByDescending(item => item.ValidFrom).FirstOrDefault()
                ?? throw new InvalidOperationException($"Material {material.Brand} {material.Model} não possui preço vigente.");
            items.Add(new QuoteItemSnapshot(request.MaterialId, material.Brand, material.Model, request.Quantity, price.Amount, price.Amount));
        }
        return items;
    }

    private static decimal CalculatePrice(decimal cost, decimal margin, decimal tax) =>
        decimal.Round(cost * (1 + margin / 100m) * (1 + tax / 100m), 2, MidpointRounding.AwayFromZero);

    private static SolarQuoteResponse ToResponse(SolarQuoteRecord quote) =>
        new(quote.Id, quote.Status.ToString(), quote.SupplierName, quote.TotalCost, quote.TotalPrice, quote.ItemsJson);

    private sealed record QuoteItemSnapshot(Guid MaterialId, string Brand, string Model, decimal Quantity, decimal UnitPriceSnapshot, decimal UnitPriceCurrent);
}
