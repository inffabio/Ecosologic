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
        new ProjectItem("Instalação solar 1", "Instalação solar", "Projeto fotovoltaico", "assets/projects/1000093004.jpg", "Instalação de energia solar"),
        new ProjectItem("Instalação solar 2", "Instalação solar", "Projeto fotovoltaico", "assets/projects/1000093007.jpg", "Instalação de energia solar"),
        new ProjectItem("Instalação solar 3", "Instalação solar", "Projeto fotovoltaico", "assets/projects/1000141815.jpg", "Instalação de energia solar"),
        new ProjectItem("Instalação solar 4", "Instalação solar", "Projeto fotovoltaico", "assets/projects/dayse-8kw-1000kwh-mes.jpeg", "Instalação de energia solar"),
        new ProjectItem("Instalação solar 5", "Instalação solar", "Projeto fotovoltaico", "assets/projects/img-20190603-101900150.jpg", "Instalação de energia solar"),
        new ProjectItem("Instalação solar 6", "Instalação solar", "Projeto fotovoltaico", "assets/projects/instalacao-placas-joao-03.jpg", "Instalação de placas solares"),
        new ProjectItem("Instalação solar 7", "Instalação solar", "Projeto fotovoltaico", "assets/projects/inversor-instalado-joao-6-5kw.jpg", "Inversor solar instalado"),
        new ProjectItem("Instalação solar 8", "Instalação solar", "Projeto fotovoltaico", "assets/projects/jorge-01-7kw.jpg", "Instalação de energia solar"),
        new ProjectItem("Instalação solar 9", "Instalação solar", "Projeto fotovoltaico", "assets/projects/jorge-02.jpg", "Instalação de energia solar"),
        new ProjectItem("Instalação solar 10", "Instalação solar", "Projeto fotovoltaico", "assets/projects/lenilson-gd-02.jpg", "Instalação de energia solar"),
        new ProjectItem("Instalação solar 11", "Instalação solar", "Projeto fotovoltaico", "assets/projects/ricardo-01-5-5kw.jpg", "Instalação de energia solar"),
        new ProjectItem("Instalação solar 12", "Instalação solar", "Projeto fotovoltaico", "assets/projects/ricardo-02.jpg", "Instalação de energia solar"),
        new ProjectItem("Instalação solar 13", "Instalação solar", "Projeto fotovoltaico", "assets/projects/sinclar-01-8kw.jpg", "Instalação de energia solar"),
        new ProjectItem("Instalação solar 14", "Instalação solar", "Projeto fotovoltaico", "assets/projects/sinclar-03.jpg", "Instalação de energia solar"),
        new ProjectItem("Instalação solar 15", "Instalação solar", "Projeto fotovoltaico", "assets/projects/sinclar-06.jpg", "Instalação de energia solar"),
        new ProjectItem("Instalação solar 16", "Instalação solar", "Projeto fotovoltaico", "assets/projects/telhado-01.jpg", "Sistema solar instalado em telhado"),
        new ProjectItem("Instalação solar 17", "Instalação solar", "Projeto fotovoltaico", "assets/projects/telhado-02.jpg", "Sistema solar instalado em telhado"),
        new ProjectItem("Instalação solar 18", "Instalação solar", "Projeto fotovoltaico", "assets/projects/modulos-natalia-02.jpg", "Módulos de energia solar instalados"),
        new ProjectItem("Instalação solar 19", "Instalação solar", "Projeto fotovoltaico", "assets/projects/modulos-natalia-03.jpg", "Módulos de energia solar instalados"),
        new ProjectItem("Instalação solar 20", "Instalação solar", "Projeto fotovoltaico", "assets/projects/inversor-01.jpeg", "Inversor de energia solar instalado"),
        new ProjectItem("Gerador Carla", "Frame de vídeo", "Registro de instalação", "assets/projects/gerador-carla-frame.png", "Frame do vídeo do gerador Carla"),
        new ProjectItem("Sistema Natalia", "Frame de vídeo", "Registro de instalação", "assets/projects/filmagem-natalia-sistema-frame.png", "Frame do vídeo do sistema Natalia")
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
    public string ContactPhone { get; set; } = "+55 (21) 96584-7684";
    public string SolutionsJson { get; set; } = HomeContentDefaults.SolutionsJson;
    public string ProcessStepsJson { get; set; } = HomeContentDefaults.ProcessStepsJson;
    public string ProjectsJson { get; set; } = HomeContentDefaults.ProjectsJson;
    public DateTimeOffset UpdatedAt { get; set; }
}
