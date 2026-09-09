using System.Text.Json;

namespace Ecosologic.Infrastructure.Persistence;

public sealed record SolutionItem(string Title, string Text);
public sealed record ProcessStepItem(string Title, string Text);
public sealed record ProjectItem(string Title, string Category, string Power, string ImageUrl, string Alt);

public static class HomeContentDefaults
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static readonly IReadOnlyList<SolutionItem> Solutions = new[]
    {
        new SolutionItem("Residencial", "Mais controle sobre a conta e mais liberdade para sua casa."),
        new SolutionItem("Comercial", "Eficiência que protege a margem e valoriza seu negócio."),
        new SolutionItem("Industrial", "Performance energética para operações que não podem parar."),
        new SolutionItem("Agronegócio", "Energia confiável para produzir com visão de longo prazo.")
    };

    public static readonly IReadOnlyList<ProcessStepItem> ProcessSteps = new[]
    {
        new ProcessStepItem("Diagnóstico", "Entendemos seu consumo, imóvel e objetivo."),
        new ProcessStepItem("Dimensionamento", "Calculamos a solução adequada ao seu perfil."),
        new ProcessStepItem("Proposta clara", "Você recebe números, prazos e condições sem letras miúdas."),
        new ProcessStepItem("Instalação", "Equipe especializada acompanha tudo até a entrega.")
    };

    public static readonly IReadOnlyList<ProjectItem> Projects = new[]
    {
        new ProjectItem("Instalação residencial completa", "Residencial · RJ", "5,5 kWp", "assets/projects/02-solar.jpg", "Instalação solar residencial Ecosologic"),
        new ProjectItem("Módulos solares instalados", "Residencial · RJ", "4,0 kWp", "assets/projects/03-solar.jpg", "Detalhe de módulos solares instalados"),
        new ProjectItem("Usina em telhado residencial", "Residencial · RJ", "7,0 kWp", "assets/projects/04-solar.jpg", "Sistema fotovoltaico em telhado")
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    public static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch (JsonException) { return default; }
    }

    public static readonly string SolutionsJson = Serialize(Solutions);
    public static readonly string ProcessStepsJson = Serialize(ProcessSteps);
    public static readonly string ProjectsJson = Serialize(Projects);
}

public sealed class HomeContentRecord
{
    public Guid Id { get; set; }
    public string HeroTitle { get; set; } = "Seu próximo passo para uma energia mais inteligente.";
    public string HeroText { get; set; } = "Projetamos sistemas solares com clareza, precisão e acompanhamento próximo, do primeiro cálculo à instalação.";
    public string HeroImageUrl { get; set; } = "assets/projects/01-solar.jpg";
    public string ContactEmail { get; set; } = "fabio@ecosologic.com.br";
    public string ContactPhone { get; set; } = "+55 (21) 99542-4027";
    public string SolutionsJson { get; set; } = HomeContentDefaults.SolutionsJson;
    public string ProcessStepsJson { get; set; } = HomeContentDefaults.ProcessStepsJson;
    public string ProjectsJson { get; set; } = HomeContentDefaults.ProjectsJson;
    public DateTimeOffset UpdatedAt { get; set; }
}
