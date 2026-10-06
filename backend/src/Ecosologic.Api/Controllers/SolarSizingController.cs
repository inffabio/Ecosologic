using System.Text.Json;
using System.Text.Json.Serialization;
using Ecosologic.Api.Contracts;
using Ecosologic.Application.Solar;
using Ecosologic.Domain.Solar;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Controllers;

[ApiController]
[Route("api/solar-sizing")]
[Authorize(Roles = "Admin")]
public sealed class SolarSizingController(EcosologicDbContext db) : ControllerBase
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [HttpPost("drafts")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] SolarSizingDraftRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var input = new SolarSizingRequest
        {
            LeadId = request.LeadId,
            Distributor = request.Distributor,
            Group = request.Group,
            Modality = request.Modality,
            Connection = request.Connection,
            MonthlyConsumptionKWh = request.MonthlyConsumptionKWh,
            MonthlyBillAmount = request.MonthlyBillAmount,
            ModuleMaterialId = request.ModuleMaterialId,
            InverterMaterialId = request.InverterMaterialId,
            ProtocolDate = request.ProtocolDate,
            Address = request.Address,
            AssumptionsJson = request.AssumptionsJson
        };

        try
        {
            input.Validate();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message, field = exception.ParamName });
        }

        if (!await db.Leads.AnyAsync(lead => lead.Id == input.LeadId, cancellationToken))
            return NotFound(new { error = "Lead não encontrado.", field = nameof(request.LeadId) });

        if (request.DistributorId is { } distributorId &&
            !await db.Distributors.AnyAsync(distributor => distributor.Id == distributorId && distributor.IsActive, cancellationToken))
            return BadRequest(new { error = "Concessionária inválida ou inativa.", field = nameof(request.DistributorId) });

        var inputsJson = JsonSerializer.Serialize(request);
        var materialSnapshotJson = JsonSerializer.Serialize(new
        {
            request.ModuleMaterialId,
            request.InverterMaterialId
        }, SnapshotJsonOptions);
        var tariffSnapshotJson = JsonSerializer.Serialize(new
        {
            request.Distributor,
            request.DistributorId,
            request.Group,
            request.Modality,
            request.Connection,
            request.ProtocolDate
        });
        var sizing = SolarSizingRecord.Create(
            Guid.NewGuid(),
            input.LeadId,
            input.Distributor,
            input.Group,
            input.Modality,
            "1.0.0",
            inputsJson,
            materialSnapshotJson,
            tariffSnapshotJson,
            request.DistributorId);

        db.SolarSizings.Add(sizing);
        await db.SaveChangesAsync(cancellationToken);

        var response = new SolarSizingDraftResponse(sizing.Id, sizing.Status.ToString());
        return CreatedAtAction(nameof(Get), new { id = sizing.Id }, response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var sizing = await db.SolarSizings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        return sizing is null
            ? NotFound()
            : Ok(new SolarSizingDraftResponse(sizing.Id, sizing.Status.ToString()));
    }

    [HttpPost("{id:guid}/calculate")]
    public async Task<IActionResult> Calculate(
        Guid id,
        [FromBody] SolarSizingCalculationRequest request,
        CancellationToken cancellationToken)
    {
        var sizing = await db.SolarSizings.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sizing is null)
            return NotFound();

        try
        {
            var draft = JsonSerializer.Deserialize<SolarSizingDraftRequest>(sizing.InputsJson)
                ?? throw new InvalidOperationException("Snapshot de entradas inválido.");
            var input = SolarSizingInput.Create(
                draft.MonthlyConsumptionKWh.Select(value => (double)value).ToArray(),
                request.MonthlyHsp,
                request.MonthlyKFactor,
                SolarModuleSpec.Create(
                    request.Module.PowerWp,
                    request.Module.Voc,
                    request.Module.Vmp,
                    request.Module.Isc,
                    request.Module.Imp,
                    request.Module.AreaM2),
                SolarLosses.Create(
                    request.Losses.Sombreamento,
                    request.Losses.Sujeira,
                    request.Losses.Tolerancia,
                    request.Losses.Mismatch,
                    request.Losses.Temperatura,
                    request.Losses.Cc,
                    request.Losses.Mppt,
                    request.Losses.Inversor,
                    request.Losses.Ca),
                request.Orientation,
                request.InclinationDegrees,
                request.AvailableAreaM2,
                request.OversizingFactor,
                request.DeratingFactor,
                request.Inverter is null
                    ? null
                    : SolarInverterSpec.Create(
                        request.Inverter.NominalPowerW,
                        request.Inverter.MpptVoltageMin,
                        request.Inverter.MpptVoltageMax,
                        request.Inverter.MaxInputVoltage,
                        request.Inverter.MaxInputCurrent,
                        request.Inverter.MpptCount,
                        request.Inverter.MaxStringsPerMppt),
                request.ModulesPerString,
                request.StringCount,
                 request.MonthlyBillWithSolarAmount is null ? null : draft.MonthlyBillAmount,
                 request.MonthlyBillWithSolarAmount,
                 request.InvestmentAmount);

            var result = new SolarSizingCalculator().Calculate(input);
            sizing.Calculate(JsonSerializer.Serialize(result));
            await db.SaveChangesAsync(cancellationToken);

            return Ok(new SolarSizingCalculationResponse(sizing.Id, sizing.Status.ToString(), result));
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
}
