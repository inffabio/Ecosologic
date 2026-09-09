using Ecosologic.Application.Solar;
using Ecosologic.Domain.Solar;

namespace Ecosologic.Api.Tests;

public class TariffBillCalculatorTests
{
    private static readonly TariffBillCalculator Calculator = new();

    // --- Fixtures de tarifa (valores sintéticos, nunca Light/Enel reais) ---

    private static TariffComponent C(TariffComponentKind kind, TariffUnit unit, TariffPost post, decimal value) =>
        TariffComponent.Create(kind, unit, post, value, false);

    private static TariffProfile ProfileB(IReadOnlyList<TariffComponent> components, bool isComplete = true) =>
        TariffProfile.Create(
            Guid.NewGuid(), Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.Conventional,
            new DateOnly(2026, 1, 1), null, "RES X", "https://example", null, DateTimeOffset.UtcNow, isComplete, components);

    private static TariffProfile ProfileA(TariffModality modality, IReadOnlyList<TariffComponent> components, bool isComplete = true) =>
        TariffProfile.Create(
            Guid.NewGuid(), Distributor.Light, TariffGroup.A, TariffSubgroup.A4, modality,
            new DateOnly(2026, 1, 1), null, "RES X", "https://example", null, DateTimeOffset.UtcNow, isComplete, components);

    private static GridCompensationRule RuleB(
        TariffPost post = TariffPost.Single,
        decimal percent = 60m,
        TariffComponentKind baseComponent = TariffComponentKind.TUSD_DISTRIBUTION,
        bool isComplete = true,
        TariffModality modality = TariffModality.Conventional,
        Distributor distributor = Distributor.Light,
        TariffSubgroup subgroup = TariffSubgroup.B1,
        int referenceYear = 2026,
        DateOnly? validityEnd = null) =>
        GridCompensationRule.Create(
            Guid.NewGuid(), distributor, TariffGroup.B, subgroup, modality,
            post, referenceYear, new DateOnly(2026, 1, 1), validityEnd, percent, baseComponent,
            "RES 14.300/2022", "https://www.aneel.gov.br/example", null, DateTimeOffset.UtcNow, isComplete);

    private static GridCompensationRule RuleA(
        TariffPost post,
        TariffModality modality = TariffModality.Blue,
        decimal percent = 60m,
        TariffComponentKind baseComponent = TariffComponentKind.TUSD_DISTRIBUTION,
        bool isComplete = true,
        Distributor distributor = Distributor.Light,
        TariffSubgroup subgroup = TariffSubgroup.A4,
        int referenceYear = 2026,
        DateOnly? validityEnd = null) =>
        GridCompensationRule.Create(
            Guid.NewGuid(), distributor, TariffGroup.A, subgroup, modality,
            post, referenceYear, new DateOnly(2026, 1, 1), validityEnd, percent, baseComponent,
            "RES 14.300/2022", "https://www.aneel.gov.br/example", null, DateTimeOffset.UtcNow, isComplete);

    private static IReadOnlyList<decimal> Series(decimal value) => Enumerable.Repeat(value, 12).ToArray();

    private static IReadOnlyList<decimal> Monthly(params decimal[] values) => values;

    private static Dictionary<TariffPost, IReadOnlyList<decimal>> Cons(TariffPost post, IReadOnlyList<decimal> values) =>
        new() { [post] = values };

    // Grupo B convencional: TE 0,70; TUSD_DISTRIBUIÇÃO 0,30 + TUSD_TRANSMISSÃO 0,10; tributos 0,20 por kWh.
    private static IReadOnlyList<TariffComponent> GroupBComponents() =>
    [
        C(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Single, 0.70m),
        C(TariffComponentKind.TUSD_DISTRIBUTION, TariffUnit.KWh, TariffPost.Single, 0.30m),
        C(TariffComponentKind.TUSD_TRANSMISSION, TariffUnit.KWh, TariffPost.Single, 0.10m),
        C(TariffComponentKind.TAX, TariffUnit.KWh, TariffPost.Single, 0.20m)
    ];

    private static IReadOnlyList<TariffComponent> GroupABlueComponents() =>
    [
        C(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Peak, 0.90m),
        C(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.OffPeak, 0.45m),
        C(TariffComponentKind.TUSD_DISTRIBUTION, TariffUnit.KWh, TariffPost.Peak, 0.35m),
        C(TariffComponentKind.TUSD_DISTRIBUTION, TariffUnit.KWh, TariffPost.OffPeak, 0.15m),
        C(TariffComponentKind.TAX, TariffUnit.KWh, TariffPost.Peak, 0.10m),
        C(TariffComponentKind.TAX, TariffUnit.KWh, TariffPost.OffPeak, 0.05m),
        C(TariffComponentKind.DEMAND, TariffUnit.KW, TariffPost.Peak, 30m),
        C(TariffComponentKind.OVERAGE, TariffUnit.KW, TariffPost.Peak, 60m)
    ];

    private static TariffBillInput InputB(
        IReadOnlyList<TariffComponent> components,
        ConnectionPhase phase,
        IReadOnlyList<decimal> consumption,
        IReadOnlyList<decimal> injection,
        IReadOnlyDictionary<TariffPost, GridCompensationRule>? rules = null,
        int creditExpiryMonths = 60) =>
        TariffBillInput.Create(
            ProfileB(components),
            rules ?? new Dictionary<TariffPost, GridCompensationRule> { [TariffPost.Single] = RuleB() },
            Cons(TariffPost.Single, consumption),
            Cons(TariffPost.Single, injection),
            phase,
            null,
            null,
            creditExpiryMonths);

    // --- Grupo B: disponibilidade mono/bi/tri ---

    [Fact]
    public void GroupB_triphase_without_solar_bills_full_consumption()
    {
        var result = Calculator.Calculate(InputB(GroupBComponents(), ConnectionPhase.Triphase, Series(300m), Series(0m)));

        var month = result.Months[0];
        Assert.Equal(300m, month.EnergyFromGridKWh);
        Assert.Equal(210m, month.TeCost);
        Assert.Equal(120m, month.TusdCost);
        Assert.Equal(60m, month.TaxCost);
        Assert.Equal(0m, month.FioBCost);
        Assert.Equal(0m, month.AvailabilityCost);
        Assert.Equal(390m, month.TotalWithoutSolar);
        Assert.Equal(390m, month.TotalWithSolar);
        Assert.Equal(0m, month.Savings);
        Assert.Equal(4680m, result.Summary!.AnnualTotalWithoutSolar);
    }

    [Theory]
    [InlineData(ConnectionPhase.Monophase, 30, 21)]
    [InlineData(ConnectionPhase.Biphase, 50, 35)]
    [InlineData(ConnectionPhase.Triphase, 100, 70)]
    public void GroupB_full_solar_charges_availability_floor_only(
        ConnectionPhase phase, int floorKWh, decimal availabilityCost)
    {
        var result = Calculator.Calculate(InputB(GroupBComponents(), phase, Series(300m), Series(300m)));

        var month = result.Months[0];
        Assert.Equal(0m, month.EnergyFromGridKWh);
        Assert.Equal(300m, month.CompensatedEnergyKWh);
        Assert.Equal((decimal)floorKWh, month.AvailabilityKWh);
        Assert.Equal(availabilityCost, month.AvailabilityCost);
        Assert.Equal(0m, month.TeCost);
        Assert.Equal(0m, month.TusdCost);
        Assert.Equal(54m, month.FioBCost);
        Assert.Equal(390m, month.TotalWithoutSolar);
        Assert.Equal(availabilityCost + 54m, month.TotalWithSolar);
        Assert.Equal(390m - (availabilityCost + 54m), month.Savings);
    }

    [Fact]
    public void GroupB_partial_solar_bills_only_remaining_energy()
    {
        var result = Calculator.Calculate(InputB(GroupBComponents(), ConnectionPhase.Triphase, Series(300m), Series(200m)));

        var month = result.Months[0];
        Assert.Equal(100m, month.EnergyFromGridKWh);
        Assert.Equal(200m, month.CompensatedEnergyKWh);
        Assert.Equal(70m, month.TeCost);
        Assert.Equal(40m, month.TusdCost);
        Assert.Equal(20m, month.TaxCost);
        Assert.Equal(36m, month.FioBCost);
        Assert.Equal(0m, month.AvailabilityCost);
        Assert.Equal(166m, month.TotalWithSolar);
    }

    // --- Créditos e expiração ---

    [Fact]
    public void GroupB_surplus_banks_credit_used_in_next_month()
    {
        var consumption = Monthly(100, 300, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100);
        var injection = Monthly(300, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var result = Calculator.Calculate(InputB(GroupBComponents(), ConnectionPhase.Triphase, consumption, injection));

        Assert.Equal(0m, result.Months[0].EnergyFromGridKWh);
        Assert.Equal(200m, result.Months[1].Posts.Single(p => p.Post == TariffPost.Single).CompensatedViaCreditKWh);
        Assert.Equal(100m, result.Months[1].EnergyFromGridKWh);
    }

    [Fact]
    public void GroupB_credit_expires_after_configured_months()
    {
        var consumption = Monthly(0, 40, 60, 50, 0, 0, 0, 0, 0, 0, 0, 0);
        var injection = Monthly(100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        // Crédito de 100 kWh gerado no mês 1 expira ao fim do mês 3 (60 -> 2 meses de expiração).
        var result = Calculator.Calculate(InputB(GroupBComponents(), ConnectionPhase.Triphase, consumption, injection, creditExpiryMonths: 2));

        Assert.Equal(40m, result.Months[1].Posts.Single(p => p.Post == TariffPost.Single).CompensatedViaCreditKWh);
        Assert.Equal(60m, result.Months[2].Posts.Single(p => p.Post == TariffPost.Single).CompensatedViaCreditKWh);
        // Mês 4: crédito expirado, energia da rede não compensada.
        Assert.Equal(0m, result.Months[3].Posts.Single(p => p.Post == TariffPost.Single).CompensatedViaCreditKWh);
        Assert.Equal(50m, result.Months[3].EnergyFromGridKWh);
        Assert.Empty(result.RemainingCredits);
    }

    [Fact]
    public void GroupB_zero_consumption_and_injection_charges_availability()
    {
        var result = Calculator.Calculate(InputB(GroupBComponents(), ConnectionPhase.Monophase, Series(0m), Series(0m)));

        var month = result.Months[0];
        Assert.Equal(0m, month.EnergyFromGridKWh);
        Assert.Equal(30m, month.AvailabilityKWh);
        Assert.Equal(21m, month.AvailabilityCost);
        Assert.Equal(21m, month.TotalWithSolar);
    }

    // --- Grupo A: azul ---

    [Fact]
    public void GroupA_blue_separates_demand_overage_and_cross_post_compensation()
    {
        var input = TariffBillInput.Create(
            ProfileA(TariffModality.Blue, GroupABlueComponents()),
            new Dictionary<TariffPost, GridCompensationRule>
            {
                [TariffPost.Peak] = RuleA(TariffPost.Peak),
                [TariffPost.OffPeak] = RuleA(TariffPost.OffPeak)
            },
            new Dictionary<TariffPost, IReadOnlyList<decimal>>
            {
                [TariffPost.Peak] = Series(500m),
                [TariffPost.OffPeak] = Series(1000m)
            },
            new Dictionary<TariffPost, IReadOnlyList<decimal>>
            {
                [TariffPost.Peak] = Series(0m),
                [TariffPost.OffPeak] = Series(1500m)
            },
            null,
            Series(100m),
            Series(120m));

        var result = Calculator.Calculate(input);

        var month = result.Months[0];
        Assert.Equal(3600m, month.DemandCost);
        Assert.Equal(1200m, month.OverageCost);
        Assert.Equal(0m, month.EnergyFromGridKWh);
        Assert.Equal(1500m, month.CompensatedEnergyKWh);
        Assert.Equal(195m, month.FioBCost);
        Assert.Equal(4995m, month.TotalWithSolar);
        Assert.Equal(6125m, month.TotalWithoutSolar);
        Assert.Equal(1130m, month.Savings);

        // Posto cruzado: excedente fora ponta compensa déficit na ponta, com rastreabilidade.
        var peak = month.Posts.Single(p => p.Post == TariffPost.Peak);
        var offPeak = month.Posts.Single(p => p.Post == TariffPost.OffPeak);
        Assert.Equal(500m, peak.CompensatedViaCreditKWh);
        Assert.Equal(1000m, offPeak.CompensatedWithinMonthKWh);
        Assert.Empty(result.RemainingCredits);
    }

    [Fact]
    public void GroupA_blue_no_overage_when_measured_below_contracted()
    {
        var input = TariffBillInput.Create(
            ProfileA(TariffModality.Blue, GroupABlueComponents()),
            new Dictionary<TariffPost, GridCompensationRule>
            {
                [TariffPost.Peak] = RuleA(TariffPost.Peak),
                [TariffPost.OffPeak] = RuleA(TariffPost.OffPeak)
            },
            new Dictionary<TariffPost, IReadOnlyList<decimal>>
            {
                [TariffPost.Peak] = Series(500m),
                [TariffPost.OffPeak] = Series(1000m)
            },
            new Dictionary<TariffPost, IReadOnlyList<decimal>>
            {
                [TariffPost.Peak] = Series(0m),
                [TariffPost.OffPeak] = Series(0m)
            },
            null,
            Series(100m),
            Series(90m));

        var month = Calculator.Calculate(input).Months[0];
        Assert.Equal(3000m, month.DemandCost);
        Assert.Equal(0m, month.OverageCost);
    }

    // --- Grupo A: verde ---

    [Fact]
    public void GroupA_green_uses_offpeak_demand_and_no_overage()
    {
        var components = new[]
        {
            C(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Peak, 0.80m),
            C(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.OffPeak, 0.40m),
            C(TariffComponentKind.TUSD_DISTRIBUTION, TariffUnit.KWh, TariffPost.Peak, 0.30m),
            C(TariffComponentKind.TUSD_DISTRIBUTION, TariffUnit.KWh, TariffPost.OffPeak, 0.12m),
            C(TariffComponentKind.DEMAND, TariffUnit.KW, TariffPost.OffPeak, 25m),
            C(TariffComponentKind.OVERAGE, TariffUnit.KW, TariffPost.OffPeak, 50m)
        };

        var input = TariffBillInput.Create(
            ProfileA(TariffModality.Green, components),
            new Dictionary<TariffPost, GridCompensationRule>
            {
                [TariffPost.Peak] = RuleA(TariffPost.Peak, TariffModality.Green),
                [TariffPost.OffPeak] = RuleA(TariffPost.OffPeak, TariffModality.Green)
            },
            new Dictionary<TariffPost, IReadOnlyList<decimal>>
            {
                [TariffPost.Peak] = Series(400m),
                [TariffPost.OffPeak] = Series(800m)
            },
            new Dictionary<TariffPost, IReadOnlyList<decimal>>
            {
                [TariffPost.Peak] = Series(0m),
                [TariffPost.OffPeak] = Series(400m)
            },
            null,
            Series(100m),
            Series(100m));

        var month = Calculator.Calculate(input).Months[0];
        Assert.Equal(2500m, month.DemandCost);
        Assert.Equal(0m, month.OverageCost);
        Assert.Equal(3176.80m, month.TotalWithSolar);
        Assert.Equal(3356m, month.TotalWithoutSolar);
        Assert.Equal(179.20m, month.Savings);
    }

    // --- Tarifa branca: posto intermediário ---

    [Fact]
    public void GroupB_white_supports_peak_intermediate_offpeak()
    {
        var components = new[]
        {
            C(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Peak, 1.00m),
            C(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Intermediate, 0.80m),
            C(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.OffPeak, 0.50m),
            C(TariffComponentKind.TUSD_DISTRIBUTION, TariffUnit.KWh, TariffPost.Peak, 0.40m),
            C(TariffComponentKind.TUSD_DISTRIBUTION, TariffUnit.KWh, TariffPost.Intermediate, 0.30m),
            C(TariffComponentKind.TUSD_DISTRIBUTION, TariffUnit.KWh, TariffPost.OffPeak, 0.20m)
        };

        var profile = TariffProfile.Create(
            Guid.NewGuid(), Distributor.Light, TariffGroup.B, TariffSubgroup.B1, TariffModality.White,
            new DateOnly(2026, 1, 1), null, "RES X", "https://example", null, DateTimeOffset.UtcNow, true, components);

        var input = TariffBillInput.Create(
            profile,
            new Dictionary<TariffPost, GridCompensationRule>
            {
                [TariffPost.Peak] = RuleB(TariffPost.Peak, modality: TariffModality.White),
                [TariffPost.Intermediate] = RuleB(TariffPost.Intermediate, modality: TariffModality.White),
                [TariffPost.OffPeak] = RuleB(TariffPost.OffPeak, modality: TariffModality.White)
            },
            new Dictionary<TariffPost, IReadOnlyList<decimal>>
            {
                [TariffPost.Peak] = Series(100m),
                [TariffPost.Intermediate] = Series(50m),
                [TariffPost.OffPeak] = Series(200m)
            },
            new Dictionary<TariffPost, IReadOnlyList<decimal>>
            {
                [TariffPost.Peak] = Series(0m),
                [TariffPost.Intermediate] = Series(0m),
                [TariffPost.OffPeak] = Series(0m)
            },
            ConnectionPhase.Monophase);

        var month = Calculator.Calculate(input).Months[0];
        Assert.Equal(3, month.Posts.Count);
        Assert.Contains(month.Posts, p => p.Post == TariffPost.Intermediate);
        // TE = 100×1,00 + 50×0,80 + 200×0,50 = 240.
        Assert.Equal(240m, month.TeCost);
        Assert.Equal(335m, month.TotalWithSolar);
    }

    // --- Bloqueios: nunca preencher zero silenciosamente ---

    [Fact]
    public void Incomplete_profile_blocks()
    {
        var input = TariffBillInput.Create(
            ProfileB([], isComplete: false),
            new Dictionary<TariffPost, GridCompensationRule> { [TariffPost.Single] = RuleB() },
            Cons(TariffPost.Single, Series(300m)),
            Cons(TariffPost.Single, Series(0m)),
            ConnectionPhase.Triphase);

        var result = Calculator.Calculate(input);

        Assert.True(result.IsBlocked);
        Assert.Contains(result.Errors, e => e.Code == "ProfileIncomplete");
        Assert.Empty(result.Months);
        Assert.Null(result.Summary);
    }

    [Fact]
    public void Missing_te_component_blocks()
    {
        var input = TariffBillInput.Create(
            ProfileB([C(TariffComponentKind.TUSD_DISTRIBUTION, TariffUnit.KWh, TariffPost.Single, 0.30m)]),
            new Dictionary<TariffPost, GridCompensationRule> { [TariffPost.Single] = RuleB() },
            Cons(TariffPost.Single, Series(300m)),
            Cons(TariffPost.Single, Series(0m)),
            ConnectionPhase.Triphase);

        var result = Calculator.Calculate(input);

        Assert.True(result.IsBlocked);
        Assert.Contains(result.Errors, e => e.Code == "ComponentMissing");
    }

    [Fact]
    public void Missing_fiob_rule_blocks()
    {
        var input = TariffBillInput.Create(
            ProfileB(GroupBComponents()),
            new Dictionary<TariffPost, GridCompensationRule>(),
            Cons(TariffPost.Single, Series(300m)),
            Cons(TariffPost.Single, Series(0m)),
            ConnectionPhase.Triphase);

        var result = Calculator.Calculate(input);

        Assert.True(result.IsBlocked);
        Assert.Contains(result.Errors, e => e.Code == "RuleMissing");
    }

    [Fact]
    public void Incomplete_fiob_rule_blocks()
    {
        var input = TariffBillInput.Create(
            ProfileB(GroupBComponents()),
            new Dictionary<TariffPost, GridCompensationRule> { [TariffPost.Single] = RuleB(isComplete: false) },
            Cons(TariffPost.Single, Series(300m)),
            Cons(TariffPost.Single, Series(0m)),
            ConnectionPhase.Triphase);

        var result = Calculator.Calculate(input);

        Assert.True(result.IsBlocked);
        Assert.Contains(result.Errors, e => e.Code == "RuleIncomplete");
    }

    [Fact]
    public void Missing_fiob_base_component_blocks()
    {
        // Perfil sem TUSD_DISTRIBUTION (apenas TE e TUSD combinado): a base configurada da regra não existe.
        var components = new[]
        {
            C(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Single, 0.70m),
            C(TariffComponentKind.TUSD, TariffUnit.KWh, TariffPost.Single, 0.40m)
        };

        var input = TariffBillInput.Create(
            ProfileB(components),
            new Dictionary<TariffPost, GridCompensationRule> { [TariffPost.Single] = RuleB() },
            Cons(TariffPost.Single, Series(300m)),
            Cons(TariffPost.Single, Series(0m)),
            ConnectionPhase.Triphase);

        var result = Calculator.Calculate(input);

        Assert.True(result.IsBlocked);
        Assert.Contains(result.Errors, e => e.Code == "ComponentMissing");
    }

    [Fact]
    public void GroupA_missing_demand_component_blocks()
    {
        var components = new[]
        {
            C(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Peak, 0.90m),
            C(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.OffPeak, 0.45m),
            C(TariffComponentKind.TUSD_DISTRIBUTION, TariffUnit.KWh, TariffPost.Peak, 0.35m),
            C(TariffComponentKind.TUSD_DISTRIBUTION, TariffUnit.KWh, TariffPost.OffPeak, 0.15m)
        };

        var input = TariffBillInput.Create(
            ProfileA(TariffModality.Blue, components),
            new Dictionary<TariffPost, GridCompensationRule>
            {
                [TariffPost.Peak] = RuleA(TariffPost.Peak),
                [TariffPost.OffPeak] = RuleA(TariffPost.OffPeak)
            },
            new Dictionary<TariffPost, IReadOnlyList<decimal>> { [TariffPost.Peak] = Series(500m), [TariffPost.OffPeak] = Series(1000m) },
            new Dictionary<TariffPost, IReadOnlyList<decimal>> { [TariffPost.Peak] = Series(0m), [TariffPost.OffPeak] = Series(0m) },
            null,
            Series(100m),
            Series(100m));

        var result = Calculator.Calculate(input);

        Assert.True(result.IsBlocked);
        Assert.Contains(result.Errors, e => e.Code == "ComponentMissing");
    }

    // --- Bloqueios: regra de Fio B deve corresponder exatamente ao perfil ---

    [Fact]
    public void Rule_distributor_mismatch_blocks()
    {
        var input = TariffBillInput.Create(
            ProfileB(GroupBComponents()),
            new Dictionary<TariffPost, GridCompensationRule> { [TariffPost.Single] = RuleB(distributor: Distributor.EnelRio) },
            Cons(TariffPost.Single, Series(300m)),
            Cons(TariffPost.Single, Series(0m)),
            ConnectionPhase.Triphase);

        var result = Calculator.Calculate(input);

        Assert.True(result.IsBlocked);
        Assert.Contains(result.Errors, e => e.Code == "RuleDistributorMismatch");
    }

    [Fact]
    public void Rule_subgroup_mismatch_blocks()
    {
        var input = TariffBillInput.Create(
            ProfileB(GroupBComponents()),
            new Dictionary<TariffPost, GridCompensationRule> { [TariffPost.Single] = RuleB(subgroup: TariffSubgroup.B2) },
            Cons(TariffPost.Single, Series(300m)),
            Cons(TariffPost.Single, Series(0m)),
            ConnectionPhase.Triphase);

        var result = Calculator.Calculate(input);

        Assert.True(result.IsBlocked);
        Assert.Contains(result.Errors, e => e.Code == "RuleSubgroupMismatch");
    }

    [Fact]
    public void Rule_modality_mismatch_blocks()
    {
        var input = TariffBillInput.Create(
            ProfileA(TariffModality.Blue, GroupABlueComponents()),
            new Dictionary<TariffPost, GridCompensationRule>
            {
                [TariffPost.Peak] = RuleA(TariffPost.Peak, TariffModality.Green),
                [TariffPost.OffPeak] = RuleA(TariffPost.OffPeak, TariffModality.Green)
            },
            new Dictionary<TariffPost, IReadOnlyList<decimal>> { [TariffPost.Peak] = Series(500m), [TariffPost.OffPeak] = Series(1000m) },
            new Dictionary<TariffPost, IReadOnlyList<decimal>> { [TariffPost.Peak] = Series(0m), [TariffPost.OffPeak] = Series(0m) },
            null,
            Series(100m),
            Series(100m));

        var result = Calculator.Calculate(input);

        Assert.True(result.IsBlocked);
        Assert.Contains(result.Errors, e => e.Code == "RuleModalityMismatch");
    }

    [Fact]
    public void Rule_reference_year_mismatch_blocks()
    {
        var input = TariffBillInput.Create(
            ProfileB(GroupBComponents()),
            new Dictionary<TariffPost, GridCompensationRule> { [TariffPost.Single] = RuleB(referenceYear: 2027) },
            Cons(TariffPost.Single, Series(300m)),
            Cons(TariffPost.Single, Series(0m)),
            ConnectionPhase.Triphase,
            null,
            null,
            60,
            referenceYear: 2026);

        var result = Calculator.Calculate(input);

        Assert.True(result.IsBlocked);
        Assert.Contains(result.Errors, e => e.Code == "RuleReferenceYearMismatch");
    }

    [Fact]
    public void Rule_not_in_force_blocks()
    {
        var input = TariffBillInput.Create(
            ProfileB(GroupBComponents()),
            new Dictionary<TariffPost, GridCompensationRule> { [TariffPost.Single] = RuleB(validityEnd: new DateOnly(2026, 6, 30)) },
            Cons(TariffPost.Single, Series(300m)),
            Cons(TariffPost.Single, Series(0m)),
            ConnectionPhase.Triphase,
            null,
            null,
            60,
            referenceDate: new DateOnly(2026, 12, 31));

        var result = Calculator.Calculate(input);

        Assert.True(result.IsBlocked);
        Assert.Contains(result.Errors, e => e.Code == "RuleNotInForce");
    }

    [Fact]
    public void Rules_dictionary_key_must_match_rule_post()
    {
        Assert.Throws<ArgumentException>(() =>
            TariffBillInput.Create(
                ProfileA(TariffModality.Blue, GroupABlueComponents()),
                new Dictionary<TariffPost, GridCompensationRule>
                {
                    [TariffPost.Peak] = RuleA(TariffPost.OffPeak),
                    [TariffPost.OffPeak] = RuleA(TariffPost.OffPeak)
                },
                new Dictionary<TariffPost, IReadOnlyList<decimal>> { [TariffPost.Peak] = Series(500m), [TariffPost.OffPeak] = Series(1000m) },
                new Dictionary<TariffPost, IReadOnlyList<decimal>> { [TariffPost.Peak] = Series(0m), [TariffPost.OffPeak] = Series(0m) },
                null,
                Series(100m),
                Series(100m)));
    }

    // --- Arredondamento: centavos apenas no total mensal/fatura ---

    [Fact]
    public void Totals_round_to_cents_but_line_items_keep_full_precision()
    {
        var components = new[]
        {
            C(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Single, 0.1111m),
            C(TariffComponentKind.TUSD_DISTRIBUTION, TariffUnit.KWh, TariffPost.Single, 0.2222m),
            C(TariffComponentKind.TAX, TariffUnit.KWh, TariffPost.Single, 0.3333m)
        };

        // Consumo 1 kWh, mono (piso 30): TE 0,1111; TUSD 0,2222; tributos 0,3333;
        // disponibilidade 29 × 0,1111 = 3,2219. Total bruto = 3,8885 -> 3,89.
        var result = Calculator.Calculate(InputB(components, ConnectionPhase.Monophase, Series(1m), Series(0m)));

        var month = result.Months[0];
        Assert.Equal(0.1111m, month.TeCost);
        Assert.Equal(0.2222m, month.TusdCost);
        Assert.Equal(3.2219m, month.AvailabilityCost);
        Assert.Equal(3.89m, month.TotalWithSolar);
        Assert.Equal(46.68m, result.Summary!.AnnualTotalWithSolar);
    }

    // --- Validações de entrada ---

    [Theory]
    [InlineData(-1)]
    public void Rejects_negative_consumption(decimal value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InputB(GroupBComponents(), ConnectionPhase.Triphase, Monthly(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), Series(0m)));
    }

    [Fact]
    public void Rejects_negative_injection()
    {
        var injection = Monthly(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InputB(GroupBComponents(), ConnectionPhase.Triphase, Series(300m), injection));
    }

    [Fact]
    public void Rejects_wrong_series_length()
    {
        Assert.Throws<ArgumentException>(() =>
            InputB(GroupBComponents(), ConnectionPhase.Triphase, new decimal[11], Series(0m)));
    }

    [Fact]
    public void GroupA_rejects_negative_demand()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TariffBillInput.Create(
                ProfileA(TariffModality.Blue, GroupABlueComponents()),
                new Dictionary<TariffPost, GridCompensationRule>
                {
                    [TariffPost.Peak] = RuleA(TariffPost.Peak),
                    [TariffPost.OffPeak] = RuleA(TariffPost.OffPeak)
                },
                new Dictionary<TariffPost, IReadOnlyList<decimal>> { [TariffPost.Peak] = Series(500m), [TariffPost.OffPeak] = Series(1000m) },
                new Dictionary<TariffPost, IReadOnlyList<decimal>> { [TariffPost.Peak] = Series(0m), [TariffPost.OffPeak] = Series(0m) },
                null,
                Series(-1m),
                Series(100m)));
    }

    [Fact]
    public void Extreme_tariff_overflow_throws_instead_of_silent_zero()
    {
        // Decimal não tem NaN/Infinity; overflow de multiplicação deve propagar, nunca virar zero.
        var components = new[]
        {
            C(TariffComponentKind.TE, TariffUnit.KWh, TariffPost.Single, decimal.MaxValue),
            C(TariffComponentKind.TUSD_DISTRIBUTION, TariffUnit.KWh, TariffPost.Single, 0.30m)
        };

        var input = InputB(components, ConnectionPhase.Monophase, Series(2m), Series(0m));

        Assert.Throws<OverflowException>(() => Calculator.Calculate(input));
    }

    // --- Determinismo e identidades ---

    [Fact]
    public void Calculator_is_deterministic()
    {
        var first = Calculator.Calculate(InputB(GroupBComponents(), ConnectionPhase.Triphase, Series(300m), Series(200m)));
        var second = Calculator.Calculate(InputB(GroupBComponents(), ConnectionPhase.Triphase, Series(300m), Series(200m)));

        Assert.Equal(first.Summary, second.Summary);
        Assert.Equal(first.Months.Select(m => m.TotalWithSolar).ToArray(), second.Months.Select(m => m.TotalWithSolar).ToArray());
        Assert.Equal(first.Months.Select(m => m.TotalWithoutSolar).ToArray(), second.Months.Select(m => m.TotalWithoutSolar).ToArray());
        Assert.Equal(first.RemainingCredits, second.RemainingCredits);
    }

    [Fact]
    public void Savings_equals_total_without_solar_minus_total_with_solar()
    {
        var result = Calculator.Calculate(InputB(GroupBComponents(), ConnectionPhase.Triphase, Series(300m), Series(200m)));

        foreach (var month in result.Months)
            Assert.Equal(month.TotalWithoutSolar - month.TotalWithSolar, month.Savings);
    }
}
