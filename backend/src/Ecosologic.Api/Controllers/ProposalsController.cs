using System.Text;
using Ecosologic.Api.Contracts;
using Ecosologic.Api.Media;
using Ecosologic.Application.Solar;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Controllers;

[ApiController]
[Route("api/proposals")]
[Authorize(Roles = "Admin")]
public sealed class ProposalsController(EcosologicDbContext db, IMediaStorage storage, ProposalRenderer renderer) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Generate([FromBody] ProposalGenerateRequest request, CancellationToken cancellationToken)
    {
        var quote = await db.SolarQuotes.SingleOrDefaultAsync(item => item.Id == request.QuoteId, cancellationToken);
        if (quote is null)
            return NotFound(new { error = "Cotação não encontrada." });

        try
        {
            var proposal = ProposalRecord.Create(Guid.NewGuid(), quote, request.TemplateVersion, request.PayloadJson);
            var rendered = renderer.Render(request.PayloadJson, request.TemplateVersion);
            await using var content = new MemoryStream(rendered.Content);
            var fileName = await storage.SaveAsync(content, ".pdf", cancellationToken);
            var fileUrl = $"/uploads/{fileName}";
            proposal.Generate(quote, fileUrl, rendered.Sha256);
            db.Proposals.Add(proposal);
            await db.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = proposal.Id }, new ProposalResponse(
                proposal.Id, proposal.QuoteId, proposal.Status.ToString(), proposal.TemplateVersion, fileUrl, rendered.Sha256));
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
        var proposal = await db.Proposals.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return proposal is null
            ? NotFound()
            : Ok(new ProposalResponse(proposal.Id, proposal.QuoteId, proposal.Status.ToString(), proposal.TemplateVersion, proposal.FileUrl ?? "", proposal.FileHash ?? ""));
    }
}
