using Ecosologic.Api.Controllers;
using Ecosologic.Api.Contracts;
using Ecosologic.Domain.Solar;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Tests;

public sealed class SolarMaterialControllerTests
{
    private static EcosologicDbContext NewDb(out SqliteConnection connection)
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var db = new EcosologicDbContext(new DbContextOptionsBuilder<EcosologicDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public void Controller_requires_admin_role()
    {
        var attribute = Assert.Single(typeof(SolarMaterialsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Equal("Admin", Assert.IsType<AuthorizeAttribute>(attribute).Roles);
    }

    [Fact]
    public async Task Create_persists_confirmed_material_and_price()
    {
        using var db = NewDb(out var connection);
        using var _ = connection;
        var controller = new SolarMaterialsController(db);

        var result = await controller.Create(new SolarMaterialCreateRequest
        {
            Type = "Module", Brand = " Trina ", Model = "Vertex S+", PowerW = 450,
            TechnicalDataJson = "{\"vmp\":41}", SourceUrl = "https://example.test/module",
            InitialPrice = 899.90m, PriceValidFrom = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero)
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<SolarMaterialResponse>(created.Value);
        var saved = await db.SolarMaterials.Include(material => material.Prices).SingleAsync();
        Assert.Equal("Trina", saved.Brand);
        Assert.Equal(899.90m, saved.Prices.Single().Amount);
        Assert.Equal(saved.Id, response.Id);
    }

    [Fact]
    public async Task AddPrice_keeps_previous_price_in_history()
    {
        using var db = NewDb(out var connection);
        using var _ = connection;
        var material = SolarMaterial.Create(SolarMaterialType.Inverter, "Solis", "S6", 5000, "{}", "https://example.test/inverter");
        var record = SolarMaterialRecord.Create(material);
        db.SolarMaterials.Add(record);
        await db.SaveChangesAsync();
        var controller = new SolarMaterialsController(db);

        var result = await controller.AddPrice(record.Id, new SolarMaterialPriceRequest
        {
            Amount = 3200m,
            ValidFrom = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero)
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(await db.SolarMaterialPrices.ToListAsync());
    }
}
