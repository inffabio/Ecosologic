using Ecosologic.Application.Solar;
using Ecosologic.Domain.Solar;

namespace Ecosologic.Api.Tests;

public sealed class FioBRuleSelectorTests
{
    private static GridCompensationRule Rule(
        int year,
        decimal percent,
        DateOnly start,
        DateOnly? end = null,
        Distributor distributor = Distributor.Light) =>
        GridCompensationRule.Create(
            Guid.NewGuid(),
            distributor,
            TariffGroup.B,
            TariffSubgroup.B1,
            TariffModality.Conventional,
            TariffPost.Single,
            year,
            start,
            end,
            percent,
            TariffComponentKind.TUSD_DISTRIBUTION,
            "RES 14.300/2022",
            "https://example.test/aneel",
            null,
            DateTimeOffset.UtcNow,
            true);

    private static FioBRuleSelectionRequest Request(DateOnly date, int year = 2026) =>
        new(
            Distributor.Light,
            TariffGroup.B,
            TariffSubgroup.B1,
            TariffModality.Conventional,
            TariffPost.Single,
            year,
            date);

    [Fact]
    public void Selects_rule_by_full_tariff_identity_and_reference_date()
    {
        var rules = new[]
        {
            Rule(2026, 60m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
            Rule(2027, 75m, new DateOnly(2027, 1, 1), null),
            Rule(2026, 60m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Distributor.EnelRio)
        };

        var selected = FioBCalculator.SelectRule(Request(new DateOnly(2026, 9, 25)), rules);

        Assert.Equal(60m, selected.ProgressivePercent);
    }

    [Fact]
    public void SelectRule_rejects_missing_or_ambiguous_current_rule()
    {
        var missing = Assert.Throws<InvalidOperationException>(() =>
            FioBCalculator.SelectRule(Request(new DateOnly(2025, 12, 31), 2025),
                [Rule(2026, 60m, new DateOnly(2026, 1, 1))]));
        Assert.Contains("nenhuma", missing.Message, StringComparison.OrdinalIgnoreCase);

        var ambiguous = Assert.Throws<InvalidOperationException>(() =>
            FioBCalculator.SelectRule(Request(new DateOnly(2026, 9, 25)),
                [
                    Rule(2026, 60m, new DateOnly(2026, 1, 1)),
                    Rule(2026, 60m, new DateOnly(2026, 1, 1))
                ]));
        Assert.Contains("múltiplas", ambiguous.Message, StringComparison.OrdinalIgnoreCase);
    }
}
