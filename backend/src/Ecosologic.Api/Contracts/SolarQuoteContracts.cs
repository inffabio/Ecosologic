using Ecosologic.Domain.Solar;

namespace Ecosologic.Api.Contracts;

public sealed class SolarQuoteCreateRequest
{
    public Guid SizingId { get; set; }
    public string SupplierName { get; set; } = "";
    public IReadOnlyList<SolarQuoteItemRequest> Items { get; set; } = [];
    public decimal MarginPercent { get; set; }
    public decimal TaxPercent { get; set; }
    public string ConditionsJson { get; set; } = "{}";
    public DateTimeOffset ValidUntil { get; set; }
}

public sealed class SolarQuoteItemRequest
{
    public Guid MaterialId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed record SolarQuoteResponse(Guid Id, string Status, string SupplierName, decimal TotalCost, decimal TotalPrice, string ItemsJson);
