using Ecosologic.Api.Contracts;
using Ecosologic.Api.Suppliers;

namespace Ecosologic.Api.Tests;

public sealed class SolarSupplierImportDeduplicatorTests
{
    [Fact]
    public void Keeps_first_item_for_duplicate_names_ignoring_case_and_whitespace()
    {
        var items = new[]
        {
            new SolarSupplierImportItem { Name = "  Aldo Solar ", Website = "https://first.example" },
            new SolarSupplierImportItem { Name = "aldo solar", Website = "https://second.example" },
            new SolarSupplierImportItem { Name = "Solar Brasil", Website = "https://solar.example" }
        };

        var result = SolarSupplierImportDeduplicator.KeepFirstByName(items);

        Assert.Equal(2, result.Count);
        Assert.Equal("  Aldo Solar ", result[0].Name);
        Assert.Equal("Solar Brasil", result[1].Name);
    }

    [Fact]
    public void Drops_items_without_names()
    {
        var items = new[]
        {
            new SolarSupplierImportItem { Name = " " },
            new SolarSupplierImportItem { Name = "Solar Brasil" }
        };

        var result = SolarSupplierImportDeduplicator.KeepFirstByName(items);

        var item = Assert.Single(result);
        Assert.Equal("Solar Brasil", item.Name);
    }

    [Fact]
    public void Drops_names_already_present_in_the_database_ignoring_case()
    {
        var items = new[]
        {
            new SolarSupplierImportItem { Name = "  aldo solar " },
            new SolarSupplierImportItem { Name = "Solar Brasil" }
        };

        var result = SolarSupplierImportDeduplicator.KeepFirstByName(items, ["Aldo Solar"]);

        var item = Assert.Single(result);
        Assert.Equal("Solar Brasil", item.Name);
    }
}
