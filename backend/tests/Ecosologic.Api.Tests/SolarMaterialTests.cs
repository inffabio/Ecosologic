using Ecosologic.Domain.Solar;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Ecosologic.Api.Tests;

public sealed class SolarMaterialTests
{
    [Fact]
    public void Create_normalizes_brand_and_model_and_keeps_module_type()
    {
        var material = SolarMaterial.Create(
            SolarMaterialType.Module,
            "  Trina  ",
            "  Vertex S+  ",
            450,
            "{\"technology\":\"TOPCon\"}",
            "https://example.test/module");

        Assert.Equal(SolarMaterialType.Module, material.Type);
        Assert.Equal("Trina", material.Brand);
        Assert.Equal("Vertex S+", material.Model);
        Assert.Equal(450, material.PowerW);
    }

    [Fact]
    public void Create_supports_inverter_type()
    {
        var material = SolarMaterial.Create(
            SolarMaterialType.Inverter,
            "Solis",
            "S6-GR1P",
            5000,
            "{}",
            "https://example.test/inverter");

        Assert.Equal(SolarMaterialType.Inverter, material.Type);
    }

    [Fact]
    public void Create_rejects_non_positive_power()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SolarMaterial.Create(
            SolarMaterialType.Module,
            "Trina",
            "Vertex S+",
            0,
            "{}",
            "https://example.test/module"));
    }

    [Fact]
    public void AddPrice_keeps_history_and_returns_price_for_requested_date()
    {
        var material = SolarMaterial.Create(
            SolarMaterialType.Module,
            "Trina",
            "Vertex S+",
            450,
            "{}",
            "https://example.test/module");
        var firstDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var secondDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        material.AddPrice(899.90m, firstDate);
        material.AddPrice(949.90m, secondDate);

        Assert.Equal(2, material.Prices.Count);
        var firstPrice = material.CurrentPrice(firstDate.AddMonths(2));
        var secondPrice = material.CurrentPrice(secondDate);
        Assert.NotNull(firstPrice);
        Assert.NotNull(secondPrice);
        Assert.Equal(899.90m, firstPrice!.Amount);
        Assert.Equal(949.90m, secondPrice!.Amount);
        Assert.Null(material.CurrentPrice(firstDate.AddDays(-1)));
    }

    [Fact]
    public void Material_can_be_reused_without_losing_previous_prices()
    {
        var material = SolarMaterial.Create(
            SolarMaterialType.Inverter,
            "Solis",
            "S6-GR1P",
            5000,
            "{}",
            "https://example.test/inverter");

        material.AddPrice(3200m, DateTimeOffset.UtcNow.AddMonths(-2));
        material.AddPrice(3350m, DateTimeOffset.UtcNow.AddMonths(-1));

        Assert.Equal("Solis", material.Brand);
        var currentPrice = material.CurrentPrice(DateTimeOffset.UtcNow);
        Assert.NotNull(currentPrice);
        Assert.Equal(3350m, currentPrice!.Amount);
        Assert.Equal(2, material.Prices.Count);
    }

    [Fact]
    public void Model_maps_materials_with_lookup_indexes_and_price_invariants()
    {
        using var db = new EcosologicDbContext(new DbContextOptionsBuilder<EcosologicDbContext>()
            .UseNpgsql("Host=localhost;Database=ecosologic;Username=postgres;Password=postgres")
            .Options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var material = model.FindEntityType(typeof(SolarMaterialRecord))!;
        var price = model.FindEntityType(typeof(SolarMaterialPriceRecord))!;

        Assert.Equal("solar_materials", material.GetTableName());
        Assert.Equal("solar_material_prices", price.GetTableName());
        Assert.Contains(material.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(SolarMaterialRecord.Type), nameof(SolarMaterialRecord.Brand), nameof(SolarMaterialRecord.Model)]));
        Assert.Contains(price.GetCheckConstraints(), constraint => constraint.Name == "CK_solar_material_prices_amount_nonnegative");
        Assert.Contains(material.GetCheckConstraints(), constraint => constraint.Name == "CK_solar_materials_power_positive");
    }
}
