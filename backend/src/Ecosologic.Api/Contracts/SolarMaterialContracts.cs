namespace Ecosologic.Api.Contracts;

public sealed class SolarMaterialCreateRequest
{
    public string Type { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public int PowerW { get; set; }
    public string TechnicalDataJson { get; set; } = "{}";
    public string SourceUrl { get; set; } = "";
    public decimal? InitialPrice { get; set; }
    public DateTimeOffset? PriceValidFrom { get; set; }
}

public sealed class SolarMaterialPriceRequest
{
    public decimal Amount { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
}

public sealed record SolarMaterialResponse(
    Guid Id,
    string Type,
    string Brand,
    string Model,
    int PowerW,
    string TechnicalDataJson,
    string SourceUrl,
    decimal? CurrentPrice,
    DateTimeOffset CreatedAt);
