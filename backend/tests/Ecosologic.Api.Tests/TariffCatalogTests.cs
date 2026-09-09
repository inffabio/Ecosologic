using System.Reflection;
using Ecosologic.Api.Controllers;
using Ecosologic.Application.Solar;
using Ecosologic.Domain.Solar;
using Ecosologic.Infrastructure.Persistence;
using Ecosologic.Infrastructure.Solar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Ecosologic.Api.Tests;

public class TariffCatalogTests
{
    private static readonly DateOnly DefaultStart = new(2024, 1, 1);

    private static EcosologicDbContext NewNpgsqlModelContext() =>
        new(new DbContextOptionsBuilder<EcosologicDbContext>()
            .UseNpgsql("Host=localhost;Database=ecosologic;Username=postgres;Password=postgres")
            .Options);

    private static EcosologicDbContext NewSqliteContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        var db = new EcosologicDbContext(
            new DbContextOptionsBuilder<EcosologicDbContext>()
                .UseSqlite(connection)
                .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static EcosologicDbContext NewSharedContext(string source, out SqliteConnection connection)
    {
        connection = new SqliteConnection($"Data Source={source};Mode=Memory;Cache=Shared");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        var db = new EcosologicDbContext(
            new DbContextOptionsBuilder<EcosologicDbContext>()
                .UseSqlite(connection)
                .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static TariffProfileRecord NewProfile(
        Guid? id = null,
        Distributor distributor = Distributor.Light,
        TariffGroup group = TariffGroup.B,
        TariffSubgroup subgroup = TariffSubgroup.B1,
        TariffModality modality = TariffModality.Conventional,
        DateOnly? validityStart = null,
        DateOnly? validityEnd = null,
        bool isComplete = false,
        IReadOnlyList<TariffComponent>? components = null)
    {
        var start = validityStart ?? DefaultStart;
        var comps = components ?? (isComplete
            ? [TariffComponent.Create(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Single, 0.5m, false)]
            : []);
        return TariffProfileRecord.Create(
            id ?? Guid.NewGuid(),
            distributor,
            group,
            subgroup,
            modality,
            start,
            validityEnd,
            "RES 3.242/2024",
            "https://www.aneel.gov.br/example",
            "abc123",
            DateTimeOffset.UtcNow,
            isComplete,
            comps);
    }

    private static TariffComponent Component(TariffComponentKind kind, TariffUnit unit, TariffPost post, decimal value = 1m) =>
        TariffComponent.Create(kind, unit, post, value, false);

    private static TariffProfile NewDomainProfile() =>
        TariffProfile.Create(
            Guid.NewGuid(),
            Distributor.Light,
            TariffGroup.B,
            TariffSubgroup.B1,
            TariffModality.Conventional,
            new DateOnly(2024, 1, 1),
            null,
            "RES 3.242/2024",
            "https://www.aneel.gov.br/example",
            "abc123",
            DateTimeOffset.UtcNow,
            true,
            [Component(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Single)]);

    private static GridCompensationRuleRecord NewGridRule(
        Guid? id = null,
        Distributor distributor = Distributor.Light,
        TariffGroup group = TariffGroup.B,
        TariffSubgroup subgroup = TariffSubgroup.B1,
        TariffModality modality = TariffModality.Conventional,
        TariffPost post = TariffPost.Single,
        int referenceYear = 2024,
        DateOnly? validityStart = null,
        DateOnly? validityEnd = null,
        decimal progressivePercent = 15m,
        TariffComponentKind baseComponent = TariffComponentKind.TUSD_DISTRIBUTION,
        bool isComplete = false) =>
        GridCompensationRuleRecord.Create(
            id ?? Guid.NewGuid(),
            distributor,
            group,
            subgroup,
            modality,
            post,
            referenceYear,
            validityStart ?? new DateOnly(2024, 1, 1),
            validityEnd,
            progressivePercent,
            baseComponent,
            "RES 3.242/2024",
            "https://www.aneel.gov.br/example",
            null,
            DateTimeOffset.UtcNow,
            isComplete);

    // --- Combinações válidas ---

    [Fact]
    public void TariffProfile_accepts_conventional_B1()
    {
        var profile = NewProfile();

        Assert.Equal(Distributor.Light, profile.Distributor);
        Assert.Equal(TariffGroup.B, profile.Group);
        Assert.Equal(TariffSubgroup.B1, profile.Subgroup);
        Assert.Equal(TariffModality.Conventional, profile.Modality);
    }

    [Fact]
    public void TariffProfile_accepts_blue_for_group_A()
    {
        var profile = NewProfile(group: TariffGroup.A, subgroup: TariffSubgroup.A4, modality: TariffModality.Blue);

        Assert.Equal(TariffModality.Blue, profile.Modality);
    }

    [Theory]
    [InlineData(TariffSubgroup.A3a)]
    [InlineData(TariffSubgroup.A4)]
    [InlineData(TariffSubgroup.AS)]
    public void TariffProfile_accepts_green_for_A3a_A4_AS(TariffSubgroup subgroup)
    {
        var profile = NewProfile(group: TariffGroup.A, subgroup: subgroup, modality: TariffModality.Green);

        Assert.Equal(subgroup, profile.Subgroup);
    }

    [Theory]
    [InlineData(TariffSubgroup.B1)]
    [InlineData(TariffSubgroup.B2)]
    [InlineData(TariffSubgroup.B3)]
    public void TariffProfile_accepts_white_for_B1_B2_B3(TariffSubgroup subgroup)
    {
        var profile = NewProfile(subgroup: subgroup, modality: TariffModality.White);

        Assert.Equal(TariffModality.White, profile.Modality);
    }

    // --- Combinações inválidas ---

    [Fact]
    public void TariffProfile_rejects_blue_for_group_B() =>
        Assert.Throws<ArgumentException>(() =>
            NewProfile(modality: TariffModality.Blue));

    [Fact]
    public void TariffProfile_rejects_green_for_group_B() =>
        Assert.Throws<ArgumentException>(() =>
            NewProfile(modality: TariffModality.Green));

    [Fact]
    public void TariffProfile_rejects_white_for_group_A() =>
        Assert.Throws<ArgumentException>(() =>
            NewProfile(group: TariffGroup.A, subgroup: TariffSubgroup.A4, modality: TariffModality.White));

    [Fact]
    public void TariffProfile_rejects_white_for_B4() =>
        Assert.Throws<ArgumentException>(() =>
            NewProfile(subgroup: TariffSubgroup.B4, modality: TariffModality.White));

    [Fact]
    public void TariffProfile_rejects_green_for_A1() =>
        Assert.Throws<ArgumentException>(() =>
            NewProfile(group: TariffGroup.A, subgroup: TariffSubgroup.A1, modality: TariffModality.Green));

    [Fact]
    public void TariffProfile_rejects_A_group_with_B_subgroup() =>
        Assert.Throws<ArgumentException>(() =>
            NewProfile(group: TariffGroup.A, subgroup: TariffSubgroup.B1));

    [Fact]
    public void TariffProfile_rejects_B_group_with_A_subgroup() =>
        Assert.Throws<ArgumentException>(() =>
            NewProfile(group: TariffGroup.B, subgroup: TariffSubgroup.A4));

    [Theory]
    [InlineData(TariffGroup.A, TariffSubgroup.A1, TariffModality.Blue, true)]
    [InlineData(TariffGroup.A, TariffSubgroup.A2, TariffModality.Blue, true)]
    [InlineData(TariffGroup.A, TariffSubgroup.A3, TariffModality.Blue, true)]
    [InlineData(TariffGroup.A, TariffSubgroup.A3a, TariffModality.Blue, true)]
    [InlineData(TariffGroup.A, TariffSubgroup.A4, TariffModality.Blue, true)]
    [InlineData(TariffGroup.A, TariffSubgroup.AS, TariffModality.Blue, true)]
    [InlineData(TariffGroup.A, TariffSubgroup.A3a, TariffModality.Green, true)]
    [InlineData(TariffGroup.A, TariffSubgroup.A4, TariffModality.Green, true)]
    [InlineData(TariffGroup.A, TariffSubgroup.AS, TariffModality.Green, true)]
    [InlineData(TariffGroup.A, TariffSubgroup.A1, TariffModality.Green, false)]
    [InlineData(TariffGroup.A, TariffSubgroup.A2, TariffModality.Green, false)]
    [InlineData(TariffGroup.A, TariffSubgroup.A3, TariffModality.Green, false)]
    [InlineData(TariffGroup.A, TariffSubgroup.A1, TariffModality.Conventional, false)]
    [InlineData(TariffGroup.A, TariffSubgroup.A3a, TariffModality.Conventional, false)]
    [InlineData(TariffGroup.A, TariffSubgroup.A4, TariffModality.Conventional, false)]
    [InlineData(TariffGroup.A, TariffSubgroup.AS, TariffModality.Conventional, false)]
    [InlineData(TariffGroup.A, TariffSubgroup.A4, TariffModality.White, false)]
    [InlineData(TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, true)]
    [InlineData(TariffGroup.B, TariffSubgroup.B2, TariffModality.Conventional, true)]
    [InlineData(TariffGroup.B, TariffSubgroup.B3, TariffModality.Conventional, true)]
    [InlineData(TariffGroup.B, TariffSubgroup.B4, TariffModality.Conventional, true)]
    [InlineData(TariffGroup.B, TariffSubgroup.B1, TariffModality.White, true)]
    [InlineData(TariffGroup.B, TariffSubgroup.B2, TariffModality.White, true)]
    [InlineData(TariffGroup.B, TariffSubgroup.B3, TariffModality.White, true)]
    [InlineData(TariffGroup.B, TariffSubgroup.B4, TariffModality.White, false)]
    [InlineData(TariffGroup.B, TariffSubgroup.B1, TariffModality.Blue, false)]
    [InlineData(TariffGroup.B, TariffSubgroup.B4, TariffModality.Blue, false)]
    [InlineData(TariffGroup.B, TariffSubgroup.B1, TariffModality.Green, false)]
    [InlineData(TariffGroup.B, TariffSubgroup.B4, TariffModality.Green, false)]
    public void TariffProfile_modality_matrix_accepts_only_valid_combinations(
        TariffGroup group, TariffSubgroup subgroup, TariffModality modality, bool valid)
    {
        var create = () => NewProfile(group: group, subgroup: subgroup, modality: modality);

        if (valid)
        {
            var profile = create();
            Assert.Equal(modality, profile.Modality);
        }
        else
        {
            Assert.Throws<ArgumentException>(create);
        }
    }

    // --- Disponibilidade (regra estrutural) ---

    [Theory]
    [InlineData(ConnectionPhase.Monophase, 30)]
    [InlineData(ConnectionPhase.Biphase, 50)]
    [InlineData(ConnectionPhase.Triphase, 100)]
    public void TariffAvailability_returns_minimum_monthly_kwh(ConnectionPhase phase, int expected) =>
        Assert.Equal(expected, TariffAvailability.MinimumMonthlyKWh(phase));

    [Theory]
    [InlineData(TariffGroup.B, true)]
    [InlineData(TariffGroup.A, false)]
    public void TariffAvailability_applies_only_to_group_B(TariffGroup group, bool expected) =>
        Assert.Equal(expected, TariffAvailability.AppliesTo(group));

    // --- Vigência ---

    [Fact]
    public void TariffProfile_rejects_validity_end_before_start() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NewProfile(validityStart: new DateOnly(2024, 2, 1), validityEnd: new DateOnly(2024, 1, 1)));

    [Fact]
    public void TariffProfile_rejects_validity_end_equal_start() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NewProfile(validityStart: new DateOnly(2024, 1, 1), validityEnd: new DateOnly(2024, 1, 1)));

    [Fact]
    public void TariffProfile_accepts_open_ended_validity()
    {
        var profile = NewProfile(validityEnd: null);

        Assert.Null(profile.ValidityEnd);
    }

    // --- Validação de postos e componentes ---

    [Fact]
    public void TariffProfile_rejects_peak_for_conventional() =>
        Assert.Throws<ArgumentException>(() =>
            NewProfile(components: [Component(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Peak)]));

    [Fact]
    public void TariffProfile_rejects_intermediate_for_blue() =>
        Assert.Throws<ArgumentException>(() =>
            NewProfile(group: TariffGroup.A, subgroup: TariffSubgroup.A4, modality: TariffModality.Blue,
                components: [Component(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Intermediate)]));

    [Fact]
    public void TariffProfile_accepts_peak_and_offpeak_for_blue()
    {
        var profile = NewProfile(group: TariffGroup.A, subgroup: TariffSubgroup.A4, modality: TariffModality.Blue, isComplete: true,
            components:
            [
                Component(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Peak, 0.6m),
                Component(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.OffPeak, 0.4m)
            ]);

        Assert.Equal(2, profile.Components.Count);
    }

    [Fact]
    public void TariffProfile_accepts_demand_and_overage_for_group_A()
    {
        var profile = NewProfile(group: TariffGroup.A, subgroup: TariffSubgroup.A4, modality: TariffModality.Blue, isComplete: true,
            components:
            [
                Component(TariffComponentKind.DEMAND, TariffUnit.KW, TariffPost.Peak, 30m),
                Component(TariffComponentKind.OVERAGE, TariffUnit.KW, TariffPost.Peak, 40m)
            ]);

        Assert.Contains(profile.Components, c => c.Kind == TariffComponentKind.DEMAND);
        Assert.Contains(profile.Components, c => c.Kind == TariffComponentKind.OVERAGE);
    }

    [Fact]
    public void TariffProfile_rejects_demand_for_group_B() =>
        Assert.Throws<ArgumentException>(() =>
            NewProfile(components: [Component(TariffComponentKind.DEMAND, TariffUnit.KW, TariffPost.Single)]));

    [Fact]
    public void TariffProfile_rejects_overage_for_group_B() =>
        Assert.Throws<ArgumentException>(() =>
            NewProfile(components: [Component(TariffComponentKind.OVERAGE, TariffUnit.KW, TariffPost.Single)]));

    [Fact]
    public void TariffProfile_rejects_duplicate_components_by_kind_unit_post() =>
        Assert.Throws<ArgumentException>(() =>
            NewProfile(components:
            [
                Component(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Single, 0.5m),
                Component(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Single, 0.6m)
            ]));

    [Fact]
    public void TariffProfile_accepts_same_kind_with_distinct_unit_or_post()
    {
        var profile = NewProfile(group: TariffGroup.A, subgroup: TariffSubgroup.A4, modality: TariffModality.Blue, isComplete: true,
            components:
            [
                Component(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Peak, 0.6m),
                Component(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.OffPeak, 0.4m),
                Component(TariffComponentKind.TE, TariffUnit.KW, TariffPost.Peak, 10m)
            ]);

        Assert.Equal(3, profile.Components.Count);
    }

    [Fact]
    public void TariffProfile_complete_requires_components() =>
        Assert.Throws<ArgumentException>(() => NewProfile(isComplete: true, components: []));

    [Fact]
    public void TariffProfile_incomplete_accepts_no_components()
    {
        var profile = NewProfile(isComplete: false);

        Assert.False(profile.IsComplete);
        Assert.Empty(profile.Components);
    }

    // --- GridCompensationRule ---

    [Fact]
    public void GridCompensationRule_accepts_valid_rule()
    {
        var rule = NewGridRule();

        Assert.Equal(15m, rule.ProgressivePercent);
        Assert.Equal(2024, rule.ReferenceYear);
        Assert.Equal(TariffGroup.B, rule.Group);
        Assert.Equal(TariffSubgroup.B1, rule.Subgroup);
        Assert.Equal(TariffModality.Conventional, rule.Modality);
    }

    [Fact]
    public void GridCompensationRule_rejects_percent_over_100() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => NewGridRule(progressivePercent: 101m));

    [Fact]
    public void GridCompensationRule_rejects_negative_percent() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => NewGridRule(progressivePercent: -1m));

    [Fact]
    public void GridCompensationRule_rejects_invalid_subgroup_for_group() =>
        Assert.Throws<ArgumentException>(() => NewGridRule(group: TariffGroup.B, subgroup: TariffSubgroup.A4));

    [Fact]
    public void GridCompensationRule_rejects_invalid_modality_for_group() =>
        Assert.Throws<ArgumentException>(() => NewGridRule(group: TariffGroup.A, subgroup: TariffSubgroup.A4, modality: TariffModality.Conventional));

    [Fact]
    public void GridCompensationRule_rejects_post_invalid_for_modality() =>
        Assert.Throws<ArgumentException>(() => NewGridRule(modality: TariffModality.Conventional, post: TariffPost.Peak));

    [Fact]
    public void GridCompensationRule_accepts_blue_peak_for_group_A()
    {
        var rule = NewGridRule(group: TariffGroup.A, subgroup: TariffSubgroup.A4, modality: TariffModality.Blue, post: TariffPost.Peak);

        Assert.Equal(TariffPost.Peak, rule.Post);
    }

    // --- GridCompensationRule lookup ---

    [Fact]
    public async Task Catalog_find_grid_rule_returns_found_for_complete_rule()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.GridCompensationRules.Add(NewGridRule(isComplete: true));
        await db.SaveChangesAsync();

        var catalog = new TariffCatalog(db);
        var result = await catalog.FindGridCompensationRuleAsync(
            Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, TariffPost.Single, 2024, new DateOnly(2024, 6, 1));

        Assert.Equal(TariffLookupStatus.Found, result.Status);
        Assert.NotNull(result.Rule);
    }

    [Fact]
    public async Task Catalog_find_grid_rule_returns_incomplete_for_rule_not_complete()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.GridCompensationRules.Add(NewGridRule(isComplete: false));
        await db.SaveChangesAsync();

        var catalog = new TariffCatalog(db);
        var result = await catalog.FindGridCompensationRuleAsync(
            Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, TariffPost.Single, 2024, new DateOnly(2024, 6, 1));

        Assert.Equal(TariffLookupStatus.Incomplete, result.Status);
        Assert.NotNull(result.Rule);
    }

    [Fact]
    public async Task Catalog_find_grid_rule_returns_notfound_for_wrong_combination()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.GridCompensationRules.Add(NewGridRule(isComplete: true));
        await db.SaveChangesAsync();

        var catalog = new TariffCatalog(db);
        var result = await catalog.FindGridCompensationRuleAsync(
            Distributor.Light, TariffGroup.A, TariffSubgroup.A4, TariffModality.Blue, TariffPost.Peak, 2024, new DateOnly(2024, 6, 1));

        Assert.Equal(TariffLookupStatus.NotFound, result.Status);
        Assert.Null(result.Rule);
    }

    [Fact]
    public async Task Catalog_find_grid_rule_returns_notfound_for_date_outside_validity()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.GridCompensationRules.Add(NewGridRule(isComplete: true, validityStart: new DateOnly(2024, 1, 1), validityEnd: new DateOnly(2024, 12, 31)));
        await db.SaveChangesAsync();

        var catalog = new TariffCatalog(db);
        var result = await catalog.FindGridCompensationRuleAsync(
            Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, TariffPost.Single, 2024, new DateOnly(2025, 1, 1));

        Assert.Equal(TariffLookupStatus.NotFound, result.Status);
    }

    // --- GridCompensationRule: sobreposição e ambiguidade ---

    [Fact]
    public async Task Catalog_rejects_overlapping_grid_rule_validity()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        await catalog.AddGridCompensationRuleAsync(
            NewGridRule(validityStart: new DateOnly(2024, 1, 1), validityEnd: new DateOnly(2024, 6, 30)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.AddGridCompensationRuleAsync(
                NewGridRule(validityStart: new DateOnly(2024, 6, 1), validityEnd: new DateOnly(2024, 12, 31))));
    }

    [Fact]
    public async Task Catalog_allows_non_overlapping_grid_rule_validity()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        await catalog.AddGridCompensationRuleAsync(
            NewGridRule(validityStart: new DateOnly(2024, 1, 1), validityEnd: new DateOnly(2024, 6, 30)));
        await catalog.AddGridCompensationRuleAsync(
            NewGridRule(validityStart: new DateOnly(2024, 7, 1), validityEnd: new DateOnly(2024, 12, 31)));

        Assert.Equal(2, await db.GridCompensationRules.CountAsync());
    }

    [Fact]
    public async Task Catalog_concurrent_overlapping_grid_rule_adds_leave_single_rule()
    {
        var source = $"ecosologic_shared_{Guid.NewGuid():N}";
        using var db1 = NewSharedContext(source, out var connection1);
        using var db2 = NewSharedContext(source, out var connection2);
        using var _ = connection1;
        using var __ = connection2;

        var catalog1 = new TariffCatalog(db1);
        var catalog2 = new TariffCatalog(db2);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = Task.Run(async () =>
        {
            await gate.Task;
            try
            {
                await catalog1.AddGridCompensationRuleAsync(
                    NewGridRule(validityStart: new DateOnly(2024, 1, 1), validityEnd: new DateOnly(2024, 6, 30)));
                return true;
            }
            catch
            {
                return false;
            }
        });

        var second = Task.Run(async () =>
        {
            await gate.Task;
            try
            {
                await catalog2.AddGridCompensationRuleAsync(
                    NewGridRule(validityStart: new DateOnly(2024, 3, 1), validityEnd: new DateOnly(2024, 12, 31)));
                return true;
            }
            catch
            {
                return false;
            }
        });

        gate.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, results.Count(succeeded => succeeded));
        Assert.Equal(1, await db1.GridCompensationRules.CountAsync());
    }

    [Fact]
    public async Task Catalog_find_grid_rule_fails_on_ambiguity()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.GridCompensationRules.Add(NewGridRule(
            isComplete: true,
            referenceYear: 2024,
            validityStart: new DateOnly(2024, 1, 1),
            validityEnd: new DateOnly(2024, 12, 31)));
        db.GridCompensationRules.Add(NewGridRule(
            isComplete: true,
            referenceYear: 2024,
            validityStart: new DateOnly(2024, 6, 1),
            validityEnd: new DateOnly(2024, 12, 31)));
        await db.SaveChangesAsync();

        var catalog = new TariffCatalog(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.FindGridCompensationRuleAsync(
                Distributor.Light,
                TariffGroup.B,
                TariffSubgroup.B1,
                TariffModality.Conventional,
                TariffPost.Single,
                2024,
                new DateOnly(2024, 6, 1)));
    }

    [Fact]
    public async Task Catalog_find_grid_rule_distinguishes_by_reference_year()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.GridCompensationRules.Add(NewGridRule(
            isComplete: true,
            referenceYear: 2026,
            validityStart: new DateOnly(2026, 1, 1),
            validityEnd: new DateOnly(2026, 12, 31)));
        db.GridCompensationRules.Add(NewGridRule(
            isComplete: true,
            referenceYear: 2027,
            validityStart: new DateOnly(2026, 1, 1),
            validityEnd: new DateOnly(2026, 12, 31)));
        await db.SaveChangesAsync();

        var catalog = new TariffCatalog(db);
        var rule2026 = await catalog.FindGridCompensationRuleAsync(
            Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, TariffPost.Single, 2026, new DateOnly(2026, 6, 1));
        var rule2027 = await catalog.FindGridCompensationRuleAsync(
            Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, TariffPost.Single, 2027, new DateOnly(2026, 6, 1));

        Assert.Equal(TariffLookupStatus.Found, rule2026.Status);
        Assert.Equal(2026, rule2026.Rule!.ReferenceYear);
        Assert.Equal(TariffLookupStatus.Found, rule2027.Status);
        Assert.Equal(2027, rule2027.Rule!.ReferenceYear);
    }

    [Fact]
    public async Task Catalog_find_grid_rule_returns_notfound_for_wrong_reference_year()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.GridCompensationRules.Add(NewGridRule(isComplete: true, referenceYear: 2026));
        await db.SaveChangesAsync();

        var catalog = new TariffCatalog(db);
        var result = await catalog.FindGridCompensationRuleAsync(
            Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, TariffPost.Single, 2027, new DateOnly(2026, 6, 1));

        Assert.Equal(TariffLookupStatus.NotFound, result.Status);
        Assert.Null(result.Rule);
    }

    [Fact]
    public async Task Catalog_allows_overlapping_validity_for_different_reference_years()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        await catalog.AddGridCompensationRuleAsync(
            NewGridRule(referenceYear: 2026, validityStart: new DateOnly(2026, 1, 1), validityEnd: new DateOnly(2026, 12, 31)));
        await catalog.AddGridCompensationRuleAsync(
            NewGridRule(referenceYear: 2027, validityStart: new DateOnly(2026, 1, 1), validityEnd: new DateOnly(2026, 12, 31)));

        Assert.Equal(2, await db.GridCompensationRules.CountAsync());
    }

    [Fact]
    public async Task Catalog_rejects_overlapping_validity_for_same_reference_year()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        await catalog.AddGridCompensationRuleAsync(
            NewGridRule(referenceYear: 2026, validityStart: new DateOnly(2026, 1, 1), validityEnd: new DateOnly(2026, 6, 30)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.AddGridCompensationRuleAsync(
                NewGridRule(referenceYear: 2026, validityStart: new DateOnly(2026, 6, 1), validityEnd: new DateOnly(2026, 12, 31))));
    }

    [Fact]
    public async Task Catalog_find_profile_fails_on_ambiguity()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.TariffProfiles.Add(NewProfile(
            isComplete: true,
            validityStart: new DateOnly(2024, 1, 1),
            validityEnd: new DateOnly(2024, 12, 31)));
        db.TariffProfiles.Add(NewProfile(
            isComplete: true,
            validityStart: new DateOnly(2024, 3, 1),
            validityEnd: new DateOnly(2024, 12, 31)));
        await db.SaveChangesAsync();

        var catalog = new TariffCatalog(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.FindProfileAsync(
                Distributor.Light,
                TariffGroup.B,
                TariffSubgroup.B1,
                TariffModality.Conventional,
                new DateOnly(2024, 6, 1)));
    }

    // --- Imutabilidade ---

    [Fact]
    public void Tariff_domain_types_expose_no_public_setters()
    {
        AssertNoPublicSetters<TariffProfile>();
        AssertNoPublicSetters<TariffComponent>();
        AssertNoPublicSetters<GridCompensationRule>();
    }

    [Fact]
    public void TariffProfile_components_cannot_be_cast_to_mutable_list()
    {
        var profile = NewDomainProfile();

        Assert.Throws<InvalidCastException>(() => (List<TariffComponent>)profile.Components);
    }

    [Fact]
    public void TariffProfile_components_cannot_be_mutated_through_ilist()
    {
        var profile = NewDomainProfile();

        var list = (IList<TariffComponent>)profile.Components;
        Assert.Throws<NotSupportedException>(() =>
            list.Add(Component(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Single)));
    }

    [Fact]
    public void TariffProfileRecord_components_cannot_be_cast_to_mutable_list()
    {
        var record = NewProfile(isComplete: true);

        Assert.Throws<InvalidCastException>(() => (List<TariffComponentRecord>)record.Components);
    }

    [Fact]
    public void TariffProfileRecord_components_cannot_be_mutated_through_icollection()
    {
        var record = NewProfile(isComplete: true);

        var collection = (ICollection<TariffComponentRecord>)record.Components;
        Assert.Throws<NotSupportedException>(() => collection.Add(new TariffComponentRecord()));
    }

    [Fact]
    public void TariffProfileRecord_components_cannot_be_mutated_through_ilist()
    {
        var record = NewProfile(isComplete: true);

        var list = (IList<TariffComponentRecord>)record.Components;
        Assert.Throws<NotSupportedException>(() => list.Add(new TariffComponentRecord()));
    }

    [Fact]
    public void Tariff_records_protect_identity_and_snapshot_fields()
    {
        AssertNonPublicSetter<TariffProfileRecord>(
            nameof(TariffProfileRecord.Id),
            nameof(TariffProfileRecord.Distributor),
            nameof(TariffProfileRecord.Group),
            nameof(TariffProfileRecord.Subgroup),
            nameof(TariffProfileRecord.Modality),
            nameof(TariffProfileRecord.ValidityStart),
            nameof(TariffProfileRecord.ValidityEnd),
            nameof(TariffProfileRecord.ResolutionCode),
            nameof(TariffProfileRecord.SourceUrl),
            nameof(TariffProfileRecord.SourceDocumentHash),
            nameof(TariffProfileRecord.AccessedAt),
            nameof(TariffProfileRecord.IsComplete),
            nameof(TariffProfileRecord.Components));

        AssertNonPublicSetter<TariffComponentRecord>(
            nameof(TariffComponentRecord.Id),
            nameof(TariffComponentRecord.ProfileId),
            nameof(TariffComponentRecord.Kind),
            nameof(TariffComponentRecord.Unit),
            nameof(TariffComponentRecord.Post),
            nameof(TariffComponentRecord.Value),
            nameof(TariffComponentRecord.TaxIncluded),
            nameof(TariffComponentRecord.SourcePage));

        AssertNonPublicSetter<GridCompensationRuleRecord>(
            nameof(GridCompensationRuleRecord.Id),
            nameof(GridCompensationRuleRecord.Distributor),
            nameof(GridCompensationRuleRecord.Group),
            nameof(GridCompensationRuleRecord.Subgroup),
            nameof(GridCompensationRuleRecord.Modality),
            nameof(GridCompensationRuleRecord.Post),
            nameof(GridCompensationRuleRecord.ReferenceYear),
            nameof(GridCompensationRuleRecord.ValidityStart),
            nameof(GridCompensationRuleRecord.ValidityEnd),
            nameof(GridCompensationRuleRecord.ProgressivePercent),
            nameof(GridCompensationRuleRecord.BaseComponent));
    }

    // --- Catálogo: busca por combinação e data ---

    [Fact]
    public async Task Catalog_find_returns_found_for_complete_profile()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.TariffProfiles.Add(NewProfile(isComplete: true));
        await db.SaveChangesAsync();

        var catalog = new TariffCatalog(db);
        var result = await catalog.FindProfileAsync(Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, new DateOnly(2024, 6, 1));

        Assert.Equal(TariffLookupStatus.Found, result.Status);
        Assert.NotNull(result.Profile);
    }

    [Fact]
    public async Task Catalog_find_returns_incomplete_for_profile_without_components()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.TariffProfiles.Add(NewProfile(isComplete: false));
        await db.SaveChangesAsync();

        var catalog = new TariffCatalog(db);
        var result = await catalog.FindProfileAsync(Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, new DateOnly(2024, 6, 1));

        Assert.Equal(TariffLookupStatus.Incomplete, result.Status);
        Assert.NotNull(result.Profile);
    }

    [Fact]
    public async Task Catalog_find_returns_notfound_for_absent_combination()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.TariffProfiles.Add(NewProfile());
        await db.SaveChangesAsync();

        var catalog = new TariffCatalog(db);
        var result = await catalog.FindProfileAsync(Distributor.EnelRio, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, new DateOnly(2024, 6, 1));

        Assert.Equal(TariffLookupStatus.NotFound, result.Status);
        Assert.Null(result.Profile);
    }

    [Fact]
    public async Task Catalog_find_returns_notfound_for_date_outside_validity()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.TariffProfiles.Add(NewProfile(isComplete: true, validityStart: new DateOnly(2024, 1, 1), validityEnd: new DateOnly(2024, 12, 31)));
        await db.SaveChangesAsync();

        var catalog = new TariffCatalog(db);
        var result = await catalog.FindProfileAsync(Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, new DateOnly(2025, 1, 1));

        Assert.Equal(TariffLookupStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Catalog_find_respects_inclusive_validity_bounds()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.TariffProfiles.Add(NewProfile(isComplete: true, validityStart: new DateOnly(2024, 1, 1), validityEnd: new DateOnly(2024, 12, 31)));
        await db.SaveChangesAsync();

        var catalog = new TariffCatalog(db);
        var start = await catalog.FindProfileAsync(Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, new DateOnly(2024, 1, 1));
        var end = await catalog.FindProfileAsync(Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, new DateOnly(2024, 12, 31));

        Assert.Equal(TariffLookupStatus.Found, start.Status);
        Assert.Equal(TariffLookupStatus.Found, end.Status);
    }

    // --- Duplicidade ---

    [Fact]
    public async Task Catalog_rejects_overlapping_validity()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        await catalog.AddProfileAsync(NewProfile(validityStart: new DateOnly(2024, 1, 1), validityEnd: new DateOnly(2024, 6, 30)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.AddProfileAsync(NewProfile(validityStart: new DateOnly(2024, 6, 1), validityEnd: new DateOnly(2024, 12, 31))));
    }

    [Fact]
    public async Task Catalog_allows_non_overlapping_validity()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        await catalog.AddProfileAsync(NewProfile(validityStart: new DateOnly(2024, 1, 1), validityEnd: new DateOnly(2024, 6, 30)));
        await catalog.AddProfileAsync(NewProfile(validityStart: new DateOnly(2024, 7, 1), validityEnd: new DateOnly(2024, 12, 31)));

        Assert.Equal(2, await db.TariffProfiles.CountAsync());
    }

    [Fact]
    public async Task Catalog_unique_index_rejects_exact_duplicate()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.TariffProfiles.Add(NewProfile(id: Guid.NewGuid()));
        db.TariffProfiles.Add(NewProfile(id: Guid.NewGuid()));

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        Assert.Contains("UNIQUE", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Catalog_unique_index_rejects_duplicate_components()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var profile = NewProfile(isComplete: true);
        db.TariffProfiles.Add(profile);
        await db.SaveChangesAsync();

        var componentId = Guid.NewGuid();
        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"tariff_components\" (\"Id\", \"ProfileId\", \"Kind\", \"Unit\", \"Post\", \"Value\", \"TaxIncluded\", \"SourcePage\") VALUES ({componentId}, {profile.Id}, 'TE', 'KWh', 'Single', 0.5, 0, NULL)"));

        Assert.Contains("UNIQUE", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // --- Concorrência ---

    [Fact]
    public async Task Catalog_concurrent_overlapping_adds_leave_single_profile()
    {
        var source = $"ecosologic_shared_{Guid.NewGuid():N}";
        using var db1 = NewSharedContext(source, out var connection1);
        using var db2 = NewSharedContext(source, out var connection2);
        using var _ = connection1;
        using var __ = connection2;

        var catalog1 = new TariffCatalog(db1);
        var catalog2 = new TariffCatalog(db2);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = Task.Run(async () =>
        {
            await gate.Task;
            try
            {
                await catalog1.AddProfileAsync(NewProfile(validityStart: new DateOnly(2024, 1, 1), validityEnd: new DateOnly(2024, 6, 30)));
                return true;
            }
            catch
            {
                return false;
            }
        });

        var second = Task.Run(async () =>
        {
            await gate.Task;
            try
            {
                await catalog2.AddProfileAsync(NewProfile(validityStart: new DateOnly(2024, 3, 1), validityEnd: new DateOnly(2024, 12, 31)));
                return true;
            }
            catch
            {
                return false;
            }
        });

        gate.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, results.Count(succeeded => succeeded));
        Assert.Equal(1, await db1.TariffProfiles.CountAsync());
    }

    // --- Autorização ---

    [Fact]
    public void Tariffs_controller_requires_admin_role()
    {
        var attribute = Assert.Single(typeof(TariffsController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Equal("Admin", ((AuthorizeAttribute)attribute).Roles);
    }

    [Fact]
    public void Tariffs_controller_has_no_edit_endpoints()
    {
        var methods = typeof(TariffsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            Assert.Null(method.GetCustomAttribute<HttpPostAttribute>());
            Assert.Null(method.GetCustomAttribute<HttpPutAttribute>());
            Assert.Null(method.GetCustomAttribute<HttpPatchAttribute>());
            Assert.Null(method.GetCustomAttribute<HttpDeleteAttribute>());
        }
    }

    // --- API ---

    [Fact]
    public async Task Tariffs_list_returns_profiles_with_filters()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        await catalog.AddProfileAsync(NewProfile(distributor: Distributor.Light));
        await catalog.AddProfileAsync(NewProfile(distributor: Distributor.EnelRio));

        var controller = new TariffsController(catalog);
        var result = await controller.List("Light", null, null, null, null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var profiles = Assert.IsAssignableFrom<IReadOnlyList<TariffProfileResponse>>(ok.Value);
        Assert.Single(profiles);
        Assert.Equal("Light", profiles[0].Distributor);
    }

    [Fact]
    public async Task Tariffs_get_returns_profile_or_not_found()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        var profile = NewProfile(isComplete: true);
        await catalog.AddProfileAsync(profile);

        var controller = new TariffsController(catalog);
        var okResult = await controller.Get(profile.Id, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(okResult);
        var response = Assert.IsType<TariffProfileResponse>(ok.Value);
        Assert.Equal(profile.Id, response.Id);

        var notFound = await controller.Get(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundResult>(notFound);
    }

    [Fact]
    public async Task Tariffs_list_rejects_invalid_filter()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var controller = new TariffsController(new TariffCatalog(db));
        var result = await controller.List("Bogus", null, null, null, null, null, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Tariffs_list_grid_rules_returns_rules_with_filters()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        await catalog.AddGridCompensationRuleAsync(NewGridRule(distributor: Distributor.Light, isComplete: true));
        await catalog.AddGridCompensationRuleAsync(NewGridRule(distributor: Distributor.EnelRio, isComplete: true));

        var controller = new TariffsController(catalog);
        var result = await controller.ListGridRules("Light", null, null, null, null, null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var rules = Assert.IsAssignableFrom<IReadOnlyList<GridCompensationRuleResponse>>(ok.Value);
        Assert.Single(rules);
        Assert.Equal("Light", rules[0].Distributor);
    }

    [Fact]
    public async Task Tariffs_list_grid_rules_rejects_invalid_filter()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var controller = new TariffsController(new TariffCatalog(db));
        var result = await controller.ListGridRules("Bogus", null, null, null, null, null, null, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Tariffs_list_grid_rules_defaults_to_complete_rules_only()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.GridCompensationRules.Add(NewGridRule(isComplete: true, referenceYear: 2024));
        db.GridCompensationRules.Add(NewGridRule(isComplete: false, referenceYear: 2025));
        await db.SaveChangesAsync();

        var controller = new TariffsController(new TariffCatalog(db));
        var result = await controller.ListGridRules(null, null, null, null, null, null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var rules = Assert.IsAssignableFrom<IReadOnlyList<GridCompensationRuleResponse>>(ok.Value);
        Assert.Single(rules);
        Assert.True(rules[0].IsComplete);
    }

    [Fact]
    public async Task Tariffs_list_grid_rules_allows_incomplete_for_legacy_audit()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        db.GridCompensationRules.Add(NewGridRule(isComplete: true, referenceYear: 2024));
        db.GridCompensationRules.Add(NewGridRule(isComplete: false, referenceYear: 2025));
        await db.SaveChangesAsync();

        var controller = new TariffsController(new TariffCatalog(db));
        var result = await controller.ListGridRules(null, null, null, null, null, null, null, false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var rules = Assert.IsAssignableFrom<IReadOnlyList<GridCompensationRuleResponse>>(ok.Value);
        Assert.Single(rules);
        Assert.False(rules[0].IsComplete);
    }

    // --- EF model ---

    [Fact]
    public void Model_maps_tariff_entities_with_expected_tables_and_string_enums()
    {
        using var db = NewNpgsqlModelContext();

        var profile = db.Model.FindEntityType(typeof(TariffProfileRecord))!;
        Assert.Equal("tariff_profiles", profile.GetTableName());
        Assert.Equal("character varying(32)", profile.FindProperty(nameof(TariffProfileRecord.Distributor))!.GetColumnType());
        Assert.Equal("character varying(32)", profile.FindProperty(nameof(TariffProfileRecord.Modality))!.GetColumnType());

        var component = db.Model.FindEntityType(typeof(TariffComponentRecord))!;
        Assert.Equal("tariff_components", component.GetTableName());
        Assert.Equal("character varying(32)", component.FindProperty(nameof(TariffComponentRecord.Kind))!.GetColumnType());

        var rule = db.Model.FindEntityType(typeof(GridCompensationRuleRecord))!;
        Assert.Equal("grid_compensation_rules", rule.GetTableName());
        Assert.Equal("character varying(32)", rule.FindProperty(nameof(GridCompensationRuleRecord.Distributor))!.GetColumnType());
    }

    [Fact]
    public void Model_defines_tariff_check_constraints()
    {
        using var db = NewNpgsqlModelContext();
        var model = db.GetService<IDesignTimeModel>().Model;

        var profile = model.FindEntityType(typeof(TariffProfileRecord))!;
        Assert.Contains(profile.GetCheckConstraints(), c => c.Name == "CK_tariff_profiles_distributor");
        Assert.Contains(profile.GetCheckConstraints(), c => c.Name == "CK_tariff_profiles_group");
        Assert.Contains(profile.GetCheckConstraints(), c => c.Name == "CK_tariff_profiles_subgroup");
        Assert.Contains(profile.GetCheckConstraints(), c => c.Name == "CK_tariff_profiles_modality");
        Assert.Contains(profile.GetCheckConstraints(), c => c.Name == "CK_tariff_profiles_validity");

        var component = model.FindEntityType(typeof(TariffComponentRecord))!;
        Assert.Contains(component.GetCheckConstraints(), c => c.Name == "CK_tariff_components_kind");
        Assert.Contains(component.GetCheckConstraints(), c => c.Name == "CK_tariff_components_value_nonnegative");

        var rule = model.FindEntityType(typeof(GridCompensationRuleRecord))!;
        Assert.Contains(rule.GetCheckConstraints(), c => c.Name == "CK_grid_compensation_rules_progressive_percent");
    }

    [Fact]
    public void Model_has_unique_index_for_profile_combination()
    {
        using var db = NewNpgsqlModelContext();

        var profile = db.Model.FindEntityType(typeof(TariffProfileRecord))!;
        var unique = profile.GetIndexes().Single(index =>
            index.Properties.Any(p => p.Name == nameof(TariffProfileRecord.Distributor))
            && index.Properties.Any(p => p.Name == nameof(TariffProfileRecord.ValidityStart)));

        Assert.True(unique.IsUnique);
    }

    [Fact]
    public void Model_has_unique_index_for_component_combination()
    {
        using var db = NewNpgsqlModelContext();

        var component = db.Model.FindEntityType(typeof(TariffComponentRecord))!;
        var unique = component.GetIndexes().Single(index =>
            index.Properties.Any(p => p.Name == nameof(TariffComponentRecord.ProfileId))
            && index.Properties.Any(p => p.Name == nameof(TariffComponentRecord.Kind))
            && index.Properties.Any(p => p.Name == nameof(TariffComponentRecord.Unit))
            && index.Properties.Any(p => p.Name == nameof(TariffComponentRecord.Post)));

        Assert.True(unique.IsUnique);
    }

    [Fact]
    public void Model_has_unique_index_for_grid_rule_identity()
    {
        using var db = NewNpgsqlModelContext();

        var rule = db.Model.FindEntityType(typeof(GridCompensationRuleRecord))!;
        var unique = rule.GetIndexes().Single(index =>
            index.Properties.Any(p => p.Name == nameof(GridCompensationRuleRecord.Distributor))
            && index.Properties.Any(p => p.Name == nameof(GridCompensationRuleRecord.Group))
            && index.Properties.Any(p => p.Name == nameof(GridCompensationRuleRecord.Subgroup))
            && index.Properties.Any(p => p.Name == nameof(GridCompensationRuleRecord.Modality))
            && index.Properties.Any(p => p.Name == nameof(GridCompensationRuleRecord.Post))
            && index.Properties.Any(p => p.Name == nameof(GridCompensationRuleRecord.ReferenceYear))
            && index.Properties.Any(p => p.Name == nameof(GridCompensationRuleRecord.ValidityStart)));

        Assert.True(unique.IsUnique);
    }

    [Fact]
    public void Model_defines_grid_rule_check_constraints()
    {
        using var db = NewNpgsqlModelContext();
        var model = db.GetService<IDesignTimeModel>().Model;

        var rule = model.FindEntityType(typeof(GridCompensationRuleRecord))!;
        Assert.DoesNotContain(rule.GetCheckConstraints(), c => c.Name == "CK_grid_compensation_rules_group");
        Assert.DoesNotContain(rule.GetCheckConstraints(), c => c.Name == "CK_grid_compensation_rules_subgroup");
        Assert.DoesNotContain(rule.GetCheckConstraints(), c => c.Name == "CK_grid_compensation_rules_modality");
        Assert.Contains(rule.GetCheckConstraints(), c => c.Name == "CK_grid_compensation_rules_progressive_percent");
    }

    [Fact]
    public void Model_grid_rule_dimensions_are_nullable()
    {
        using var db = NewNpgsqlModelContext();

        var rule = db.Model.FindEntityType(typeof(GridCompensationRuleRecord))!;
        Assert.True(rule.FindProperty(nameof(GridCompensationRuleRecord.Group))!.IsNullable);
        Assert.True(rule.FindProperty(nameof(GridCompensationRuleRecord.Subgroup))!.IsNullable);
        Assert.True(rule.FindProperty(nameof(GridCompensationRuleRecord.Modality))!.IsNullable);
    }

    [Fact]
    public void Model_component_value_uses_precision_18_scale_8()
    {
        using var db = NewNpgsqlModelContext();

        var component = db.Model.FindEntityType(typeof(TariffComponentRecord))!;
        var value = component.FindProperty(nameof(TariffComponentRecord.Value))!;

        Assert.Equal(18, value.GetPrecision());
        Assert.Equal(8, value.GetScale());
    }

    [Fact]
    public void Model_maps_aneel_tariff_import_with_expected_table_and_string_status()
    {
        using var db = NewNpgsqlModelContext();

        var model = db.GetService<IDesignTimeModel>().Model;
        var import = model.FindEntityType(typeof(AneelTariffImportRecord))!;

        Assert.Equal("aneel_tariff_imports", import.GetTableName());
        Assert.Equal("character varying(32)", import.FindProperty(nameof(AneelTariffImportRecord.Status))!.GetColumnType());
        Assert.Contains(import.GetCheckConstraints(), c => c.Name == "CK_aneel_tariff_imports_status");
        Assert.Contains(import.GetCheckConstraints(), c => c.Name == "CK_aneel_tariff_imports_counts_nonnegative");
        Assert.Contains(import.GetIndexes(), i =>
            i.Properties.Any(p => p.Name == nameof(AneelTariffImportRecord.Status))
            && i.Properties.Any(p => p.Name == nameof(AneelTariffImportRecord.StartedAt)));
        Assert.Contains(import.GetIndexes(), i =>
            i.Properties.Any(p => p.Name == nameof(AneelTariffImportRecord.SourceHash)));
    }

    // --- Reconciliação de perfis importados (ANEEL) ---

    private static AneelNormalizedProfile NewNormalizedProfile(
        Distributor distributor,
        TariffSubgroup subgroup,
        DateOnly start,
        DateOnly? end,
        string hash,
        decimal sourceValue = 41.20m,
        string resolution = "RES 3.242/2024") =>
        new(distributor,
            distributor == Distributor.Light ? "Light" : "Enel RJ",
            TariffGroup.B,
            subgroup,
            TariffModality.Conventional,
            start,
            end,
            resolution,
            "https://www.aneel.gov.br/example",
            hash,
            DateTimeOffset.UtcNow,
            true,
            [new AneelNormalizedComponent(
                TariffComponentKind.FIO_B,
                TariffUnit.KWh,
                TariffPost.Single,
                sourceValue / 1000m,
                false,
                null,
                sourceValue,
                "R$/MWh",
                hash)]);

    private static IReadOnlyList<AneelNormalizedProfile> FullCoverageProfiles(
        DateOnly start,
        DateOnly? end,
        string hash,
        decimal sourceValue = 41.20m) =>
        [.. from distributor in new[] { Distributor.Light, Distributor.EnelRio }
           from subgroup in new[] { TariffSubgroup.B1, TariffSubgroup.B2, TariffSubgroup.B3 }
           select NewNormalizedProfile(distributor, subgroup, start, end, hash, sourceValue)];

    [Fact]
    public async Task Reconcile_first_import_inserts_all_profiles()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        var result = await catalog.ReconcileImportedProfilesAsync(
            FullCoverageProfiles(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), "h1"));

        Assert.Equal(6, result.InsertedProfiles);
        Assert.Equal(0, result.ClosedProfiles);
        Assert.Equal(0, result.UnchangedProfiles);
        Assert.Equal(6, await db.TariffProfiles.CountAsync());
    }

    [Fact]
    public async Task Reconcile_identical_rerun_is_noop()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var profiles = FullCoverageProfiles(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), "h1");
        var catalog = new TariffCatalog(db);

        await catalog.ReconcileImportedProfilesAsync(profiles);
        var result = await catalog.ReconcileImportedProfilesAsync(profiles);

        Assert.Equal(0, result.InsertedProfiles);
        Assert.Equal(0, result.ClosedProfiles);
        Assert.Equal(6, result.UnchangedProfiles);
        Assert.Equal(6, await db.TariffProfiles.CountAsync());
    }

    [Fact]
    public async Task Reconcile_changed_hash_rolls_validity_without_overlap()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        await catalog.ReconcileImportedProfilesAsync(
            FullCoverageProfiles(new DateOnly(2024, 1, 1), null, "h1"));

        var result = await catalog.ReconcileImportedProfilesAsync(
            FullCoverageProfiles(new DateOnly(2025, 1, 1), null, "h2"));

        Assert.Equal(6, result.InsertedProfiles);
        Assert.Equal(6, result.ClosedProfiles);
        Assert.Equal(0, result.UnchangedProfiles);
        Assert.Equal(12, await db.TariffProfiles.CountAsync());

        var previous = await db.TariffProfiles.Where(p => p.ValidityStart == new DateOnly(2024, 1, 1)).ToListAsync();
        var current = await db.TariffProfiles.Where(p => p.ValidityStart == new DateOnly(2025, 1, 1)).ToListAsync();

        Assert.All(previous, p => Assert.Equal(new DateOnly(2024, 12, 31), p.ValidityEnd));
        Assert.All(current, p => Assert.Null(p.ValidityEnd));
    }

    [Fact]
    public async Task Reconcile_multiple_periods_inserts_all_profiles()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var profiles = new List<AneelNormalizedProfile>();
        profiles.AddRange(FullCoverageProfiles(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), "h1"));
        profiles.AddRange(FullCoverageProfiles(new DateOnly(2025, 1, 1), null, "h1"));

        var catalog = new TariffCatalog(db);
        var result = await catalog.ReconcileImportedProfilesAsync(profiles);

        Assert.Equal(12, result.InsertedProfiles);
        Assert.Equal(0, result.ClosedProfiles);
        Assert.Equal(12, await db.TariffProfiles.CountAsync());
    }

    [Fact]
    public async Task Reconcile_unresolvable_overlap_rolls_back_entire_batch()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        await catalog.ReconcileImportedProfilesAsync(
            FullCoverageProfiles(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), "h1"));

        // Light: substituição adiante legítima (2025). Enel: vigência retroativa que
        // inicia antes do perfil existente e não pode ser fechada adiante.
        var batch = new List<AneelNormalizedProfile>();
        foreach (var subgroup in new[] { TariffSubgroup.B1, TariffSubgroup.B2, TariffSubgroup.B3 })
            batch.Add(NewNormalizedProfile(Distributor.Light, subgroup, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), "h2"));
        foreach (var subgroup in new[] { TariffSubgroup.B1, TariffSubgroup.B2, TariffSubgroup.B3 })
            batch.Add(NewNormalizedProfile(Distributor.EnelRio, subgroup, new DateOnly(2023, 6, 1), new DateOnly(2024, 6, 30), "h2"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.ReconcileImportedProfilesAsync(batch));

        Assert.Equal(6, await db.TariffProfiles.CountAsync());
        Assert.Empty(await db.TariffProfiles
            .Where(p => p.ValidityStart == new DateOnly(2025, 1, 1))
            .ToListAsync());
    }

    [Fact]
    public async Task Reconcile_preserves_previous_version_when_superseding()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        await catalog.ReconcileImportedProfilesAsync(
            FullCoverageProfiles(new DateOnly(2024, 1, 1), null, "h1"));
        await catalog.ReconcileImportedProfilesAsync(
            FullCoverageProfiles(new DateOnly(2025, 1, 1), null, "h2"));

        // A versão anterior permanece elegível na sua vigência encerrada.
        var previous = await catalog.FindProfileAsync(
            Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, new DateOnly(2024, 6, 1));

        Assert.Equal(TariffLookupStatus.Found, previous.Status);
        Assert.Equal(new DateOnly(2024, 12, 31), previous.Profile!.ValidityEnd);
    }

    // --- Correção de mesmo período (retificação) ---

    [Fact]
    public async Task Reconcile_same_period_correction_supersedes_without_overlap()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        await catalog.ReconcileImportedProfilesAsync(
            FullCoverageProfiles(new DateOnly(2024, 1, 1), null, "h1"));

        var result = await catalog.ReconcileImportedProfilesAsync(
            FullCoverageProfiles(new DateOnly(2024, 1, 1), null, "h2", sourceValue: 42.00m));

        Assert.Equal(6, result.InsertedProfiles);
        Assert.Equal(6, result.ClosedProfiles);
        Assert.Equal(0, result.UnchangedProfiles);

        // Ambas as versões preservadas; nenhuma sobreposição entre versões vigentes.
        Assert.Equal(12, await db.TariffProfiles.CountAsync());
        Assert.Equal(6, await db.TariffProfiles.CountAsync(p => p.IsCurrent));
        Assert.Equal(6, await db.TariffProfiles.CountAsync(p => !p.IsCurrent));

        var current = await db.TariffProfiles.Where(p => p.IsCurrent).ToListAsync();
        Assert.All(current, p => Assert.Equal("h2", p.SourceDocumentHash));

        var superseded = await db.TariffProfiles.Where(p => !p.IsCurrent).ToListAsync();
        Assert.All(superseded, p => Assert.Equal("h1", p.SourceDocumentHash));
    }

    [Fact]
    public async Task Reconcile_same_period_correction_lookup_selects_latest_version()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        await catalog.ReconcileImportedProfilesAsync(
            FullCoverageProfiles(new DateOnly(2024, 1, 1), null, "h1"));
        await catalog.ReconcileImportedProfilesAsync(
            FullCoverageProfiles(new DateOnly(2024, 1, 1), null, "h2", sourceValue: 42.00m));

        var lookup = await catalog.FindProfileAsync(
            Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional, new DateOnly(2024, 6, 1));

        Assert.Equal(TariffLookupStatus.Found, lookup.Status);
        Assert.Equal("h2", lookup.Profile!.SourceDocumentHash);
        Assert.Equal(42.00m / 1000m, lookup.Profile.Components.Single().Value);
    }

    [Fact]
    public async Task Reconcile_rejects_same_start_different_period_as_unresolvable()
    {
        using var db = NewSqliteContext(out var connection);
        using var _ = connection;

        var catalog = new TariffCatalog(db);
        await catalog.ReconcileImportedProfilesAsync(
            FullCoverageProfiles(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), "h1"));

        var corrected = FullCoverageProfiles(new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 30), "h2");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.ReconcileImportedProfilesAsync(corrected));

        Assert.Equal(6, await db.TariffProfiles.CountAsync());
    }

    // --- Helpers de reflexão ---

    private static void AssertNoPublicSetters<T>()
    {
        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.True(
                property.SetMethod is null or { IsPublic: false },
                $"{typeof(T).Name}.{property.Name} expõe um setter público.");
        }
    }

    private static void AssertNonPublicSetter<T>(params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            var property = typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);
            Assert.True(
                property!.SetMethod is null or { IsPublic: false },
                $"{typeof(T).Name}.{name} expõe um setter público.");
        }
    }
}
