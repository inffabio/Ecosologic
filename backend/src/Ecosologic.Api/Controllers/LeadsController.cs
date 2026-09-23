using Ecosologic.Domain.Crm;
using Ecosologic.Infrastructure.Persistence;
using Ecosologic.Infrastructure.Email;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Controllers;

[ApiController]
[Route("api/leads")]
[Authorize(Roles = "Admin")]
public sealed class LeadsController(
    EcosologicDbContext db,
    ILeadEmailSender? emailSender = null,
    ILogger<LeadsController>? logger = null) : ControllerBase
{
    private const int MinDueYear = 1900;
    private const int MaxDueYear = 2200;

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(CreateLeadRequest request, CancellationToken cancellationToken)
    {
        var lead = Lead.Create(request.Name, request.Phone, request.Email ?? "", request.Message ?? "");
        if (!lead.IsValid)
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["lead"] = lead.Errors.ToArray() }));

        var record = new LeadRecord
        {
            Id = lead.Id,
            Name = lead.Name,
            Phone = lead.Phone,
            Email = lead.Email,
            Message = lead.Message,
            Stage = lead.Stage,
            CreatedAt = lead.CreatedAt
        };
        db.Leads.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        if (emailSender is not null)
        {
            try
            {
                await emailSender.SendAsync(record, cancellationToken);
            }
            catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
            {
                logger?.LogError(exception, "Lead {LeadId} was saved but email delivery failed.", record.Id);
            }
        }
        return Created($"api/leads/{record.Id}", new { record.Id, record.Stage, record.CreatedAt });
    }

    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;
    private const int MaxPage = 100000;

    [HttpGet]
    public async Task<IActionResult> List(
        string? q,
        string? stage,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            page = 1;
        if (page > MaxPage)
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["page"] = [$"Página deve ser no máximo {MaxPage}."] }));
        if (pageSize < 1)
            pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize)
            pageSize = MaxPageSize;

        IQueryable<LeadRecord> query = db.Leads.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(stage))
        {
            if (!Lead.TryParseStage(stage, out var parsedStage))
                return ValidationProblem(new ValidationProblemDetails(
                    new Dictionary<string, string[]> { ["stage"] = ["Etapa inválida."] }));

            query = query.Where(lead => lead.Stage == parsedStage);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim().ToLowerInvariant();
            // Busca normalizada sem distinção de cultura (traduzida para LOWER(...) pelo EF).
            // Para grandes volumes, considere índices funcionais sobre LOWER(name), LOWER(email), etc.
            query = query.Where(lead =>
                lead.Name.ToLowerInvariant().Contains(needle)
                || lead.Email.ToLowerInvariant().Contains(needle)
                || lead.Phone.ToLowerInvariant().Contains(needle)
                || lead.Message.ToLowerInvariant().Contains(needle));
        }

        var total = await query.CountAsync(cancellationToken);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

        if (totalPages == 0)
            page = 1;
        else if (page > totalPages)
            page = totalPages;

        var items = await query
            .OrderByDescending(lead => lead.CreatedAt)
            .ThenByDescending(lead => lead.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new LeadListResponse(items, total, page, pageSize, totalPages));
    }

    [HttpGet("stage-counts")]
    public async Task<IActionResult> StageCounts(CancellationToken cancellationToken)
    {
        var groups = await db.Leads.AsNoTracking()
            .GroupBy(lead => lead.Stage)
            .Select(group => new { Stage = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var counts = Enum.GetValues<LeadStage>()
            .ToDictionary(
                stage => stage.ToString(),
                stage => groups.Where(group => group.Stage == stage).Select(group => group.Count).FirstOrDefault());

        return Ok(counts);
    }

    [HttpGet("monthly-won-count")]
    public async Task<IActionResult> MonthlyWonCount(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var endOfMonth = startOfMonth.AddMonths(1);

        var count = await db.Leads.AsNoTracking()
            .CountAsync(lead =>
                lead.Stage == LeadStage.Won
                && lead.WonAt >= startOfMonth
                && lead.WonAt < endOfMonth,
                cancellationToken);

        return Ok(count);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var lead = await db.Leads.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return lead is null ? NotFound() : Ok(lead);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateLeadRequest request, CancellationToken cancellationToken)
    {
        var lead = await db.Leads.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lead is null)
            return NotFound();

        if (request.Stage is not null)
        {
            if (!Lead.TryParseStage(request.Stage, out var stage))
                return ValidationProblem(new ValidationProblemDetails(
                    new Dictionary<string, string[]> { ["stage"] = ["Etapa inválida."] }));

            if (stage != lead.Stage)
            {
                var previous = lead.Stage;
                lead.Stage = stage;
                if (stage == LeadStage.Won)
                    lead.WonAt = DateTimeOffset.UtcNow;
                else if (previous == LeadStage.Won)
                    lead.WonAt = null;
                db.LeadActivities.Add(new LeadActivityRecord
                {
                    Id = Guid.NewGuid(),
                    LeadId = lead.Id,
                    Type = "StageChanged",
                    Description = $"Etapa alterada de {previous} para {stage}",
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        if (request.Notes is not null)
        {
            if (request.Notes.Length > 4000)
                return ValidationProblem(new ValidationProblemDetails(
                    new Dictionary<string, string[]> { ["notes"] = ["Observação deve ter no máximo 4000 caracteres."] }));
            lead.Notes = request.Notes;
        }

        if (request.ReminderAt.HasValue)
        {
            if (request.ReminderAt.Value is not { } reminder)
            {
                lead.ReminderAt = null;
            }
            else if (reminder.Offset != TimeSpan.Zero)
            {
                return ValidationProblem(new ValidationProblemDetails(
                    new Dictionary<string, string[]> { ["reminderAt"] = ["Lembrete deve estar em UTC."] }));
            }
            else
            {
                lead.ReminderAt = reminder;
            }
        }

        lead.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(lead);
    }

    [HttpGet("{id:guid}/activities")]
    public async Task<IActionResult> ListActivities(Guid id, CancellationToken cancellationToken)
    {
        var exists = await db.Leads.AsNoTracking().AnyAsync(item => item.Id == id, cancellationToken);
        if (!exists)
            return NotFound();

        var activities = await db.LeadActivities.AsNoTracking()
            .Where(activity => activity.LeadId == id)
            .OrderByDescending(activity => activity.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(activities);
    }

    [HttpPost("{id:guid}/activities")]
    public async Task<IActionResult> AddActivity(Guid id, CreateActivityRequest request, CancellationToken cancellationToken)
    {
        var lead = await db.Leads.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lead is null)
            return NotFound();

        var description = request.Description?.Trim() ?? "";
        var type = string.IsNullOrWhiteSpace(request.Type) ? "Note" : request.Type.Trim();
        var errors = new Dictionary<string, string[]>();
        if (description.Length == 0)
            errors["description"] = ["Descrição é obrigatória."];
        else if (description.Length > 4000)
            errors["description"] = ["Descrição deve ter no máximo 4000 caracteres."];

        if (type.Length > 32)
            errors["type"] = ["Tipo deve ter no máximo 32 caracteres."];

        if (errors.Count > 0)
            return ValidationProblem(new ValidationProblemDetails(errors));

        var activity = new LeadActivityRecord
        {
            Id = Guid.NewGuid(),
            LeadId = id,
            Type = type,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.LeadActivities.Add(activity);
        lead.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/leads/{id}/activities/{activity.Id}", activity);
    }

    [HttpGet("{id:guid}/tasks")]
    public async Task<IActionResult> ListTasks(Guid id, CancellationToken cancellationToken, bool includeCompleted = false)
    {
        var exists = await db.Leads.AsNoTracking().AnyAsync(item => item.Id == id, cancellationToken);
        if (!exists)
            return NotFound();

        var query = db.LeadTasks.AsNoTracking().Where(task => task.LeadId == id);
        if (!includeCompleted)
            query = query.Where(task => task.CompletedAt == null);

        var pending = await query.Where(task => task.CompletedAt == null)
            .OrderBy(task => task.DueAt)
            .ThenBy(task => task.CreatedAt)
            .ThenBy(task => task.Id)
            .ToListAsync(cancellationToken);
        var completed = await query.Where(task => task.CompletedAt != null)
            .OrderByDescending(task => task.CompletedAt)
            .ThenByDescending(task => task.CreatedAt)
            .ThenByDescending(task => task.Id)
            .ToListAsync(cancellationToken);

        return Ok(pending.Concat(completed).ToList());
    }

    [HttpPost("{id:guid}/tasks")]
    public async Task<IActionResult> AddTask(Guid id, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var lead = await db.Leads.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lead is null)
            return NotFound();

        var title = request.Title?.Trim() ?? "";
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        var errors = new Dictionary<string, string[]>();
        if (title.Length == 0)
            errors["title"] = ["Título é obrigatório."];
        else if (title.Length > 160)
            errors["title"] = ["Título deve ter no máximo 160 caracteres."];

        if (description is not null && description.Length > 2000)
            errors["description"] = ["Descrição deve ter no máximo 2000 caracteres."];

        var dueAt = request.DueAt;
        if (dueAt is null || dueAt.Value == DateTimeOffset.MinValue)
        {
            errors["dueAt"] = ["Vencimento é obrigatório."];
        }
        else if (dueAt.Value.Year < MinDueYear || dueAt.Value.Year > MaxDueYear)
        {
            errors["dueAt"] = [$"Vencimento deve estar entre os anos {MinDueYear} e {MaxDueYear}."];
        }
        else if (dueAt.Value.Offset != TimeSpan.Zero)
        {
            errors["dueAt"] = ["Vencimento deve estar em UTC."];
        }

        if (errors.Count > 0)
            return ValidationProblem(new ValidationProblemDetails(errors));

        var now = DateTimeOffset.UtcNow;
        var task = new LeadTaskRecord
        {
            Id = Guid.NewGuid(),
            LeadId = id,
            Title = title,
            Description = description,
            DueAt = dueAt!.Value.ToUniversalTime(),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.LeadTasks.Add(task);
        lead.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/leads/{id}/tasks/{task.Id}", task);
    }

    [HttpPatch("{leadId:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> UpdateTask(Guid leadId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await db.LeadTasks.SingleOrDefaultAsync(item => item.Id == taskId && item.LeadId == leadId, cancellationToken);
        if (task is null)
            return NotFound();

        if (request.Completed.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            if (request.Completed.Value)
            {
                if (task.CompletedAt is null)
                {
                    task.CompletedAt = now;
                    var lead = await db.Leads.SingleAsync(item => item.Id == leadId, cancellationToken);
                    lead.UpdatedAt = now;
                }
            }
            else
            {
                task.CompletedAt = null;
                var lead = await db.Leads.SingleAsync(item => item.Id == leadId, cancellationToken);
                lead.UpdatedAt = now;
            }

            task.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(task);
    }

    [HttpDelete("{leadId:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(Guid leadId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await db.LeadTasks.SingleOrDefaultAsync(item => item.Id == taskId && item.LeadId == leadId, cancellationToken);
        if (task is null)
            return NotFound();

        db.LeadTasks.Remove(task);
        var lead = await db.Leads.SingleAsync(item => item.Id == leadId, cancellationToken);
        lead.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public sealed record LeadListResponse(IReadOnlyList<LeadRecord> Items, int Total, int Page, int PageSize, int TotalPages);
public sealed record CreateLeadRequest(string Name, string Phone, string? Email, string? Message);
public sealed record UpdateLeadRequest(
    string? Stage,
    string? Notes,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(OptionalDateTimeConverter))] Optional<DateTimeOffset> ReminderAt = default);
public sealed record CreateActivityRequest(string? Type, string? Description);
public sealed record CreateTaskRequest(string? Title, string? Description, DateTimeOffset? DueAt);
public sealed record UpdateTaskRequest(bool? Completed);
