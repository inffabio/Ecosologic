using Ecosologic.Application.Solar;
using Ecosologic.Domain.Solar;

namespace Ecosologic.Api.Tests;

public class FioBCalculatorTests
{
    private static readonly FioBCalculator Calculator = new();

    private static GridCompensationRule Rule(
        decimal percent = 60m,
        TariffComponentKind baseComponent = TariffComponentKind.TUSD_DISTRIBUTION,
        TariffPost post = TariffPost.Single,
        bool isComplete = true) =>
        GridCompensationRule.Create(
            Guid.NewGuid(),
            Distributor.Light,
            TariffGroup.B,
            TariffSubgroup.B1,
            TariffModality.Conventional,
            post,
            2026,
            new DateOnly(2026, 1, 1),
            null,
            percent,
            baseComponent,
            "RES 14.300/2022",
            "https://www.aneel.gov.br/example",
            null,
            DateTimeOffset.UtcNow,
            isComplete);

    private static TariffComponent Base(
        TariffComponentKind kind = TariffComponentKind.TUSD_DISTRIBUTION,
        TariffPost post = TariffPost.Single,
        decimal value = 0.30m) =>
        TariffComponent.Create(kind, TariffUnit.KWh, post, value, false);

    // --- Aplicação do percentual progressivo sobre a base configurada ---

    [Fact]
    public void FioB_applies_progressive_percent_to_configured_base()
    {
        var result = Calculator.Calculate(new FioBRequest(Rule(percent: 60m), Base(value: 0.30m), 300m));

        // 60% de R$ 0,30/kWh aplicado a 300 kWh compensados = R$ 54,00.
        Assert.Equal(54m, result.Cost);
    }

    [Fact]
    public void FioB_uses_configured_base_not_a_universal_rate()
    {
        // Mesma regra, bases diferentes: o Fio B acompanha a base configurada,
        // nunca uma tarifa universal fixa.
        var distribution = Calculator.Calculate(new FioBRequest(Rule(), Base(kind: TariffComponentKind.TUSD_DISTRIBUTION, value: 0.30m), 300m));
        var transmission = Calculator.Calculate(new FioBRequest(
            Rule(baseComponent: TariffComponentKind.TUSD_TRANSMISSION),
            Base(kind: TariffComponentKind.TUSD_TRANSMISSION, value: 0.10m),
            300m));

        Assert.Equal(54m, distribution.Cost);
        Assert.Equal(18m, transmission.Cost);
    }

    [Fact]
    public void FioB_progressive_percent_scales_linearly()
    {
        var at60 = Calculator.Calculate(new FioBRequest(Rule(percent: 60m), Base(), 100m));
        var at30 = Calculator.Calculate(new FioBRequest(Rule(percent: 30m), Base(), 100m));
        var at100 = Calculator.Calculate(new FioBRequest(Rule(percent: 100m), Base(), 100m));

        Assert.Equal(18m, at60.Cost);
        Assert.Equal(9m, at30.Cost);
        Assert.Equal(30m, at100.Cost);
    }

    [Fact]
    public void FioB_zero_compensated_energy_yields_zero_cost()
    {
        var result = Calculator.Calculate(new FioBRequest(Rule(), Base(), 0m));

        Assert.Equal(0m, result.Cost);
    }

    // --- Rejeições ---

    [Fact]
    public void FioB_rejects_incomplete_rule()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Calculator.Calculate(new FioBRequest(Rule(isComplete: false), Base(), 300m)));
    }

    [Fact]
    public void FioB_rejects_base_component_kind_mismatch()
    {
        // A regra exige TUSD_DISTRIBUTION, mas recebe TE como base.
        Assert.Throws<InvalidOperationException>(() =>
            Calculator.Calculate(new FioBRequest(Rule(), Base(kind: TariffComponentKind.TE), 300m)));
    }

    [Fact]
    public void FioB_rejects_base_component_post_mismatch()
    {
        // A regra é para o posto Single, mas a base é do posto Peak.
        Assert.Throws<InvalidOperationException>(() =>
            Calculator.Calculate(new FioBRequest(Rule(), Base(post: TariffPost.Peak), 300m)));
    }

    [Fact]
    public void FioB_rejects_negative_compensated_energy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Calculator.Calculate(new FioBRequest(Rule(), Base(), -1m)));
    }

    [Fact]
    public void FioB_rejects_null_base_component()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Calculator.Calculate(new FioBRequest(Rule(), null!, 300m)));
    }

    // --- Metadados expostos ---

    [Fact]
    public void FioB_result_exposes_metadata_for_traceability()
    {
        var rule = Rule();
        var baseComponent = Base();
        var result = Calculator.Calculate(new FioBRequest(rule, baseComponent, 300m));

        Assert.Equal(TariffComponentKind.TUSD_DISTRIBUTION, result.BaseComponentKind);
        Assert.Equal(TariffPost.Single, result.Post);
        Assert.Equal(60m, result.ProgressivePercent);
        Assert.Equal(0.30m, result.BaseRate);
        Assert.Equal(300m, result.CompensatedEnergyKWh);
    }
}
