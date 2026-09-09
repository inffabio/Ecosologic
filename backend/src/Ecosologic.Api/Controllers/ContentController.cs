using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

namespace Ecosologic.Api.Controllers;

[ApiController]
[Route("api/content")]
public sealed class ContentController(EcosologicDbContext db) : ControllerBase
{
    private static readonly Guid HomeContentId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    [HttpGet("home")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicHome(CancellationToken cancellationToken)
    {
        var content = await GetOrCreate(cancellationToken);
        return Ok(ToResponse(content));
    }

    [HttpPut("home")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateHome(UpdateHomeContentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.HeroTitle) || request.HeroTitle.Length > 240
            || string.IsNullOrWhiteSpace(request.HeroText) || request.HeroText.Length > 1000
            || string.IsNullOrWhiteSpace(request.HeroImageUrl) || request.HeroImageUrl.Length > 500
            || string.IsNullOrWhiteSpace(request.ContactPhone) || request.ContactPhone.Length > 40
            || !IsValidEmail(request.ContactEmail))
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["content"] = ["Título e texto são obrigatórios."]
            }));

        var solutions = request.Solutions ?? [];
        var processSteps = request.ProcessSteps ?? [];
        var projects = request.Projects ?? [];

        if (solutions.Count > 4 || solutions.Any(item => string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 80
                || string.IsNullOrWhiteSpace(item.Text) || item.Text.Length > 240)
            || processSteps.Count > 4 || processSteps.Any(item => string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 80
                || string.IsNullOrWhiteSpace(item.Text) || item.Text.Length > 240)
            || projects.Count > 4 || projects.Any(IsInvalidProject))
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["content"] = ["Máximo de 4 soluções, 4 etapas e 4 projetos, todos com campos obrigatórios preenchidos."]
            }));

        var content = await GetOrCreate(cancellationToken);
        content.HeroTitle = request.HeroTitle.Trim();
        content.HeroText = request.HeroText.Trim();
        content.HeroImageUrl = request.HeroImageUrl.Trim();
        content.ContactEmail = request.ContactEmail.Trim();
        content.ContactPhone = request.ContactPhone.Trim();
        content.SolutionsJson = HomeContentDefaults.Serialize(solutions.Select(s => s with { Title = s.Title.Trim(), Text = s.Text.Trim() }).ToList());
        content.ProcessStepsJson = HomeContentDefaults.Serialize(processSteps.Select(s => s with { Title = s.Title.Trim(), Text = s.Text.Trim() }).ToList());
        content.ProjectsJson = HomeContentDefaults.Serialize(projects.Select(TrimProject).ToList());
        content.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(content));
    }

    private static bool IsInvalidProject(ProjectItem project) =>
        string.IsNullOrWhiteSpace(project.Title) || project.Title.Length > 160
        || string.IsNullOrWhiteSpace(project.Category) || project.Category.Length > 80
        || string.IsNullOrWhiteSpace(project.Power) || project.Power.Length > 40
        || string.IsNullOrWhiteSpace(project.ImageUrl) || project.ImageUrl.Length > 500
        || string.IsNullOrWhiteSpace(project.Alt) || project.Alt.Length > 240;

    private static ProjectItem TrimProject(ProjectItem project) =>
        project with { Title = project.Title.Trim(), Category = project.Category.Trim(), Power = project.Power.Trim(), ImageUrl = project.ImageUrl.Trim(), Alt = project.Alt.Trim() };

    private async Task<HomeContentRecord> GetOrCreate(CancellationToken cancellationToken)
    {
        var content = await db.HomeContent.FindAsync([HomeContentId], cancellationToken);
        if (content is not null)
        {
            if (string.IsNullOrWhiteSpace(content.SolutionsJson)) content.SolutionsJson = HomeContentDefaults.SolutionsJson;
            if (string.IsNullOrWhiteSpace(content.ProcessStepsJson)) content.ProcessStepsJson = HomeContentDefaults.ProcessStepsJson;
            if (string.IsNullOrWhiteSpace(content.ProjectsJson)) content.ProjectsJson = HomeContentDefaults.ProjectsJson;
            return content;
        }

        content = new HomeContentRecord { Id = HomeContentId, UpdatedAt = DateTimeOffset.UtcNow };
        db.HomeContent.Add(content);
        await db.SaveChangesAsync(cancellationToken);
        return content;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
            return false;
        try { return new MailAddress(email).Address == email.Trim(); }
        catch (FormatException) { return false; }
    }

    private static HomeContentResponse ToResponse(HomeContentRecord content) =>
        new(content.HeroTitle, content.HeroText, content.HeroImageUrl, content.ContactEmail, content.ContactPhone,
            content.UpdatedAt,
            HomeContentDefaults.Deserialize<List<SolutionItem>>(content.SolutionsJson) ?? [],
            HomeContentDefaults.Deserialize<List<ProcessStepItem>>(content.ProcessStepsJson) ?? [],
            HomeContentDefaults.Deserialize<List<ProjectItem>>(content.ProjectsJson) ?? []);
}

public sealed record UpdateHomeContentRequest(
    string HeroTitle,
    string HeroText,
    string HeroImageUrl,
    string ContactEmail,
    string ContactPhone,
    List<SolutionItem>? Solutions,
    List<ProcessStepItem>? ProcessSteps,
    List<ProjectItem>? Projects);

public sealed record HomeContentResponse(
    string HeroTitle,
    string HeroText,
    string HeroImageUrl,
    string ContactEmail,
    string ContactPhone,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SolutionItem> Solutions,
    IReadOnlyList<ProcessStepItem> ProcessSteps,
    IReadOnlyList<ProjectItem> Projects);
