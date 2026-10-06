namespace Ecosologic.Api.Contracts;

public sealed class SolarSupplierCreateRequest
{
    public string Name { get; set; } = "";
    public string? Website { get; set; }
    public string? Contact { get; set; }
    public string Source { get; set; } = "Manual";
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
}

public sealed class SolarSupplierDiscoverRequest
{
    public string Query { get; set; } = "";
}

public sealed class SolarSupplierApproveRequest
{
    public string ContactName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string WhatsApp { get; set; } = "";
}

public sealed class SolarSupplierImportRequest
{
    public IReadOnlyList<SolarSupplierImportItem> Suppliers { get; set; } = [];
}

public sealed class SolarSupplierImportItem
{
    public string Name { get; set; } = "";
    public string? Website { get; set; }
    public string? Contact { get; set; }
    public string Source { get; set; } = "n8n";
}

public sealed record SolarSupplierResponse(Guid Id, string Name, string? Website, string? Contact, string? ContactName, string? Phone, string? WhatsApp, string Source, string Status);
