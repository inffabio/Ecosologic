using Ecosologic.Api.Contracts;
using Ecosologic.Api.Suppliers;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Controllers;

[ApiController]
[Route("api/solar-suppliers")]
[Authorize(Roles = "Admin")]
public sealed class SolarSuppliersController(EcosologicDbContext db, SolarSupplierDiscovery discovery) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<SolarSupplierResponse>> List(CancellationToken cancellationToken) =>
        await db.SolarSuppliers.AsNoTracking().Where(item => item.Status == "Approved").OrderBy(item => item.Name).Select(item =>
            new SolarSupplierResponse(item.Id, item.Name, item.Website, item.Contact, item.ContactName, item.Phone, item.WhatsApp, item.Source, item.Status)).ToListAsync(cancellationToken);

    [HttpPost]
    public async Task<IActionResult> Create(SolarSupplierCreateRequest request, CancellationToken cancellationToken)
    {
        SolarSupplierRecord supplier;
        try
        {
            supplier = SolarSupplierRecord.Create(Guid.NewGuid(), request.Name, request.Website, request.Contact, request.Source);
            supplier.Approve(request.ContactName ?? "", request.Phone ?? "", request.WhatsApp ?? "");
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message, field = exception.ParamName });
        }
        db.SolarSuppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/solar-suppliers/{supplier.Id}", new SolarSupplierResponse(supplier.Id, supplier.Name, supplier.Website, supplier.Contact, supplier.ContactName, supplier.Phone, supplier.WhatsApp, supplier.Source, supplier.Status));
    }

    [HttpGet("review")]
    public async Task<IReadOnlyList<SolarSupplierResponse>> Review(CancellationToken cancellationToken) =>
        await db.SolarSuppliers.AsNoTracking().OrderBy(item => item.Name).Select(item =>
            new SolarSupplierResponse(item.Id, item.Name, item.Website, item.Contact, item.ContactName, item.Phone, item.WhatsApp, item.Source, item.Status)).ToListAsync(cancellationToken);

    [HttpPost("discover")]
    public async Task<IActionResult> Discover(SolarSupplierDiscoverRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "Informe o mercado ou região para pesquisar." });

        try
        {
            var candidates = await discovery.Search(request.Query, cancellationToken);
            var records = candidates.Where(item => !string.IsNullOrWhiteSpace(item.Name)).Select(item =>
                SolarSupplierRecord.Discover(Guid.NewGuid(), item.Name.Trim(), item.Website, item.Contact, item.Source)).ToList();
            db.SolarSuppliers.AddRange(records);
            await db.SaveChangesAsync(cancellationToken);
            return Ok(records.Select(item => new SolarSupplierResponse(item.Id, item.Name, item.Website, item.Contact, item.ContactName, item.Phone, item.WhatsApp, item.Source, item.Status)));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(SolarSupplierImportRequest request, CancellationToken cancellationToken)
    {
        var candidates = SolarSupplierImportDeduplicator.KeepFirstByName(request.Suppliers);
        var existingNames = await db.SolarSuppliers
            .Select(item => item.Name)
            .ToListAsync(cancellationToken);
        candidates = SolarSupplierImportDeduplicator.KeepFirstByName(candidates, existingNames);
        var records = candidates.Select(item =>
            SolarSupplierRecord.Discover(Guid.NewGuid(), item.Name.Trim(), item.Website, item.Contact, item.Source)).ToList();
        db.SolarSuppliers.AddRange(records);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(records.Select(item => new SolarSupplierResponse(item.Id, item.Name, item.Website, item.Contact, item.ContactName, item.Phone, item.WhatsApp, item.Source, item.Status)));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, SolarSupplierApproveRequest request, CancellationToken cancellationToken)
    {
        var supplier = await db.SolarSuppliers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (supplier is null) return NotFound();
        supplier.Approve(request.ContactName, request.Phone, request.WhatsApp);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new SolarSupplierResponse(supplier.Id, supplier.Name, supplier.Website, supplier.Contact, supplier.ContactName, supplier.Phone, supplier.WhatsApp, supplier.Source, supplier.Status));
    }
}
