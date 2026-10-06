namespace Ecosologic.Domain.Solar;

public enum SolarMaterialType
{
    Module,
    Inverter
}

public sealed class SolarMaterial
{
    private readonly List<SolarMaterialPrice> _prices = [];

    private SolarMaterial(
        SolarMaterialType type,
        string brand,
        string model,
        int powerW,
        string technicalDataJson,
        string sourceUrl)
    {
        Id = Guid.NewGuid();
        Type = type;
        Brand = brand;
        Model = model;
        PowerW = powerW;
        TechnicalDataJson = technicalDataJson;
        SourceUrl = sourceUrl;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public SolarMaterialType Type { get; }
    public string Brand { get; }
    public string Model { get; }
    public int PowerW { get; }
    public string TechnicalDataJson { get; }
    public string SourceUrl { get; }
    public DateTimeOffset CreatedAt { get; }
    public IReadOnlyCollection<SolarMaterialPrice> Prices => _prices.AsReadOnly();

    public static SolarMaterial Create(
        SolarMaterialType type,
        string brand,
        string model,
        int powerW,
        string technicalDataJson,
        string sourceUrl)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), "Tipo de material inválido.");
        if (powerW <= 0)
            throw new ArgumentOutOfRangeException(nameof(powerW), "Potência deve ser positiva.");

        brand = SolarValidation.RequireText(brand, nameof(brand), "Marca é obrigatória.");
        model = SolarValidation.RequireText(model, nameof(model), "Modelo é obrigatório.");
        technicalDataJson = SolarValidation.RequireJson(technicalDataJson, nameof(technicalDataJson), "Dados técnicos são obrigatórios.");
        sourceUrl = SolarValidation.RequireText(sourceUrl, nameof(sourceUrl), "Fonte é obrigatória.");

        return new SolarMaterial(type, brand, model, powerW, technicalDataJson, sourceUrl);
    }

    public SolarMaterialPrice AddPrice(decimal amount, DateTimeOffset validFrom)
    {
        SolarValidation.RequireMoney(amount, nameof(amount));

        if (_prices.Any(price => price.ValidFrom == validFrom))
            throw new InvalidOperationException("Já existe preço vigente a partir desta data.");

        var price = SolarMaterialPrice.Create(Id, amount, validFrom);
        _prices.Add(price);
        return price;
    }

    public SolarMaterialPrice? CurrentPrice(DateTimeOffset asOf) =>
        _prices
            .Where(price => price.ValidFrom <= asOf)
            .OrderByDescending(price => price.ValidFrom)
            .FirstOrDefault();
}
