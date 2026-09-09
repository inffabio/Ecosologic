using Ecosologic.Api.Controllers;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Tests;

public sealed class ContentControllerTests
{
    [Fact]
    public void Admin_update_requires_admin_role()
    {
        var method = typeof(ContentController).GetMethod(nameof(ContentController.UpdateHome));
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Equal("Admin", ((AuthorizeAttribute)attribute).Roles);
    }

    private static EcosologicDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EcosologicDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EcosologicDbContext(options);
    }

    private static UpdateHomeContentRequest ValidRequest(
        List<SolutionItem>? solutions = null,
        List<ProcessStepItem>? steps = null,
        List<ProjectItem>? projects = null) =>
        new("Energia feita para o seu futuro.", "Texto atualizado", "assets/projects/02-solar.jpg",
            "novo@ecosologic.com.br", "+5521995424027", solutions, steps, projects);

    [Fact]
    public async Task Public_home_returns_seed_content_when_store_is_empty()
    {
        await using var db = NewContext();

        var result = await new ContentController(db).GetPublicHome(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var content = Assert.IsType<HomeContentResponse>(ok.Value);
        Assert.Contains("energia", content.HeroTitle, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("assets/projects/01-solar.jpg", content.HeroImageUrl);
    }

    [Fact]
    public async Task Public_home_returns_default_solutions_process_and_projects_when_empty()
    {
        await using var db = NewContext();

        var result = await new ContentController(db).GetPublicHome(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var content = Assert.IsType<HomeContentResponse>(ok.Value);
        Assert.Equal(4, content.Solutions.Count);
        Assert.Equal("Residencial", content.Solutions[0].Title);
        Assert.Equal(4, content.ProcessSteps.Count);
        Assert.Equal("Diagnóstico", content.ProcessSteps[0].Title);
        Assert.Equal(3, content.Projects.Count);
        Assert.Equal("assets/projects/02-solar.jpg", content.Projects[0].ImageUrl);
    }

    [Fact]
    public async Task Admin_update_persists_home_content()
    {
        await using var db = NewContext();
        var controller = new ContentController(db);
        var request = ValidRequest();

        var result = await controller.UpdateHome(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var content = Assert.IsType<HomeContentResponse>(ok.Value);
        Assert.Equal(request.HeroTitle, content.HeroTitle);
        Assert.Equal(request.HeroImageUrl, content.HeroImageUrl);
        Assert.NotEqual(default, content.UpdatedAt);
    }

    [Fact]
    public async Task Admin_update_persists_solutions_process_steps_and_projects()
    {
        await using var db = NewContext();
        var controller = new ContentController(db);
        var solutions = new List<SolutionItem> { new("Residencial", "Texto residencial.") };
        var steps = new List<ProcessStepItem> { new("Diagnóstico", "Texto diagnóstico.") };
        var projects = new List<ProjectItem> { new("Usina residencial", "Residencial · RJ", "5,5 kWp", "assets/projects/02-solar.jpg", "Alt acessível") };

        var result = await controller.UpdateHome(ValidRequest(solutions, steps, projects), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var content = Assert.IsType<HomeContentResponse>(ok.Value);
        Assert.Single(content.Solutions);
        Assert.Equal("Residencial", content.Solutions[0].Title);
        Assert.Single(content.ProcessSteps);
        Assert.Equal("Diagnóstico", content.ProcessSteps[0].Title);
        Assert.Single(content.Projects);
        Assert.Equal("Alt acessível", content.Projects[0].Alt);
    }

    [Fact]
    public async Task Admin_update_persists_lists_as_json_columns()
    {
        await using var db = NewContext();
        var controller = new ContentController(db);
        var solutions = new List<SolutionItem> { new("Residencial", "Texto residencial.") };
        var steps = new List<ProcessStepItem> { new("Diagnóstico", "Texto diagnóstico.") };
        var projects = new List<ProjectItem> { new("Usina", "Residencial · RJ", "5,5 kWp", "assets/projects/02-solar.jpg", "Alt") };

        await controller.UpdateHome(ValidRequest(solutions, steps, projects), CancellationToken.None);

        var record = await db.HomeContent.SingleAsync();
        Assert.Contains("Residencial", record.SolutionsJson, StringComparison.Ordinal);
        Assert.Contains("Diagn", record.ProcessStepsJson, StringComparison.Ordinal);
        Assert.Contains("02-solar.jpg", record.ProjectsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_update_rejects_more_than_four_solutions()
    {
        await using var db = NewContext();
        var solutions = Enumerable.Range(1, 5)
            .Select(i => new SolutionItem($"Solução {i}", "Texto")).ToList();

        var result = await new ContentController(db).UpdateHome(ValidRequest(solutions: solutions), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Admin_update_rejects_more_than_four_process_steps()
    {
        await using var db = NewContext();
        var steps = Enumerable.Range(1, 5)
            .Select(i => new ProcessStepItem($"Etapa {i}", "Texto")).ToList();

        var result = await new ContentController(db).UpdateHome(ValidRequest(steps: steps), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Admin_update_rejects_more_than_four_projects()
    {
        await using var db = NewContext();
        var projects = Enumerable.Range(1, 5)
            .Select(i => new ProjectItem($"Projeto {i}", "Categoria", "1 kWp", "img.jpg", "alt")).ToList();

        var result = await new ContentController(db).UpdateHome(ValidRequest(projects: projects), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Admin_update_rejects_project_without_image_or_alt()
    {
        await using var db = NewContext();
        var projects = new List<ProjectItem> { new("Usina", "Residencial · RJ", "5,5 kWp", "", "") };

        var result = await new ContentController(db).UpdateHome(ValidRequest(projects: projects), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Public_home_reuses_the_same_seed_record()
    {
        await using var db = NewContext();
        var controller = new ContentController(db);

        await controller.GetPublicHome(CancellationToken.None);
        await controller.GetPublicHome(CancellationToken.None);

        Assert.Equal(1, await db.HomeContent.CountAsync());
    }

    [Fact]
    public async Task Admin_update_rejects_invalid_contact_fields()
    {
        await using var db = NewContext();

        var result = await new ContentController(db).UpdateHome(
            new UpdateHomeContentRequest("Título", "Texto", "", "email inválido", "", null, null, null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
