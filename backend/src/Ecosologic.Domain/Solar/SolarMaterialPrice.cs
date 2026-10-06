namespace Ecosologic.Domain.Solar;

public sealed class SolarMaterialPrice
{
    private SolarMaterialPrice(Guid materialId, decimal amount, DateTimeOffset validFrom)
    {
        Id = Guid.NewGuid();
        MaterialId = materialId;
        Amount = amount;
        ValidFrom = validFrom;
    }

    public Guid Id { get; }
    public Guid MaterialId { get; }
    public decimal Amount { get; }
    public DateTimeOffset ValidFrom { get; }

    internal static SolarMaterialPrice Create(Guid materialId, decimal amount, DateTimeOffset validFrom)
    {
        SolarValidation.RequireGuid(materialId, nameof(materialId), "MaterialId é obrigatório.");
        SolarValidation.RequireMoney(amount, nameof(amount));
        return new SolarMaterialPrice(materialId, amount, validFrom);
    }
}
