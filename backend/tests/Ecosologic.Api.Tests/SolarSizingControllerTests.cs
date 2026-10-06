using Ecosologic.Api.Controllers;
using Ecosologic.Api.Contracts;
using Ecosologic.Domain.Crm;
using Ecosologic.Domain.Solar;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Api.Tests;

public sealed class SolarSizingControllerTests
{
    private static EcosologicDbContext NewDb(out SqliteConnection connection)
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var db = new EcosologicDbContext(new DbContextOptionsBuilder<EcosologicDbContext>()
            .UseSqlite(connection)
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static SolarSizingDraftRequest ValidRequest() => new()
    {
        LeadId = Guid.NewGuid(),
        Distributor = "Light RJ",
        Group = "B",
        Modality = "Convencional",
        Connection = "Monofasica",
        MonthlyConsumptionKWh = Enumerable.Repeat(600m, 12).ToArray(),
        MonthlyBillAmount = Enumerable.Repeat(580m, 12).ToArray(),
        ProtocolDate = new DateOnly(2026, 9, 25),
        Address = "Rua A, 100",
        AssumptionsJson = "{}"
    };

    private static SolarSizingCalculationRequest ValidCalculation() => new()
    {
        MonthlyHsp = Enumerable.Repeat(5.5, 12).ToArray(),
        MonthlyKFactor = Enumerable.Repeat(1.0, 12).ToArray(),
        Module = new SolarModuleRequest
        {
            PowerWp = 450,
            Voc = 49,
            Vmp = 41,
            Isc = 12,
            Imp = 11,
            AreaM2 = 2.1
        },
        Losses = new SolarLossesRequest(),
        Orientation = "Norte",
        InclinationDegrees = 10,
        AvailableAreaM2 = 50
    };

    [Fact]
    public void Controller_requires_admin_role()
    {
        var attribute = Assert.Single(typeof(SolarSizingController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true));

        Assert.Equal("Admin", Assert.IsType<AuthorizeAttribute>(attribute).Roles);
    }

    [Fact]
    public async Task CreateDraft_persists_monthly_input_and_snapshots()
    {
        using var db = NewDb(out var connection);
        using var _ = connection;
        var request = ValidRequest();
        db.Leads.Add(new LeadRecord
        {
            Id = request.LeadId,
            Name = "Lead",
            Phone = "123",
            Email = "",
            Message = "",
            Stage = LeadStage.New,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await new SolarSizingController(db).CreateDraft(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<SolarSizingDraftResponse>(created.Value);
        var saved = await db.SolarSizings.SingleAsync();

        Assert.Equal(saved.Id, response.Id);
        Assert.Equal("Draft", response.Status);
        Assert.Contains("MonthlyConsumptionKWh", saved.InputsJson);
        Assert.Equal("{}", saved.MaterialSnapshotJson);
        Assert.Contains("Light RJ", saved.TariffSnapshotJson);
    }

    [Fact]
    public async Task CreateDraft_rejects_monthly_series_without_twelve_values()
    {
        using var db = NewDb(out var connection);
        using var _ = connection;
        var request = ValidRequest();
        request.MonthlyBillAmount = Enumerable.Repeat(580m, 11).ToArray();

        var result = await new SolarSizingController(db).CreateDraft(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.SolarSizings);
    }

    [Fact]
    public async Task Calculate_persists_result_and_returns_backend_charts()
    {
        using var db = NewDb(out var connection);
        using var _ = connection;
        var request = ValidRequest();
        db.Leads.Add(new LeadRecord
        {
            Id = request.LeadId,
            Name = "Lead",
            Phone = "123",
            Email = "",
            Message = "",
            Stage = LeadStage.New,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var controller = new SolarSizingController(db);
        var draft = await controller.CreateDraft(request, CancellationToken.None);
        var id = Assert.IsType<SolarSizingDraftResponse>(Assert.IsType<CreatedAtActionResult>(draft).Value).Id;

        var result = await controller.Calculate(id, ValidCalculation(), CancellationToken.None);

        var response = Assert.IsType<SolarSizingCalculationResponse>(Assert.IsType<OkObjectResult>(result).Value);
        var saved = await db.SolarSizings.SingleAsync();
        Assert.Equal("Calculated", response.Status);
        Assert.Equal(12, response.Result.Charts.GenerationVsConsumption[0].Points.Count);
        Assert.NotNull(saved.ResultsJson);
    }
}
