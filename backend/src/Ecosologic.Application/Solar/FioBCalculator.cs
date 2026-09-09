using Ecosologic.Domain.Solar;

namespace Ecosologic.Application.Solar;

/// <summary>
/// Motor determinístico do Fio B (Lei 14.300/2022 e atos regulatórios).
///
/// Fórmula exata:
///   FioB = (ProgressivePercent / 100) × BaseRate × EnergiaCompensadaKWh
///
/// A base tarifária (BaseRate) é o valor do componente indicado pela regra
/// (BaseComponent) para o posto da regra; o percentual progressivo varia por ano
/// de referência. O Fio B NÃO é uma tarifa universal: só incide sobre a base
/// configurada e sobre a energia efetivamente compensada.
/// </summary>
public sealed class FioBCalculator
{
    public const string EngineVersion = "1.0.0";

    public FioBResult Calculate(FioBRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Rule);
        ArgumentNullException.ThrowIfNull(request.BaseComponent);

        return Calculate(request.Rule, request.BaseComponent, request.CompensatedEnergyKWh);
    }

    public FioBResult Calculate(GridCompensationRule rule, TariffComponent baseComponent, decimal compensatedEnergyKWh)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(baseComponent);

        if (!rule.IsComplete)
            throw new InvalidOperationException(
                "Regra de compensação do Fio B está incompleta; não é possível aplicá-la.");

        if (baseComponent.Kind != rule.BaseComponent)
            throw new InvalidOperationException(
                $"A base configurada ({baseComponent.Kind}) não corresponde à base da regra ({rule.BaseComponent}).");

        if (baseComponent.Post != rule.Post)
            throw new InvalidOperationException(
                $"O posto da base configurada ({baseComponent.Post}) não corresponde ao posto da regra ({rule.Post}).");

        if (compensatedEnergyKWh < 0)
            throw new ArgumentOutOfRangeException(nameof(compensatedEnergyKWh), "Energia compensada não pode ser negativa.");

        var cost = rule.ProgressivePercent / 100m * baseComponent.Value * compensatedEnergyKWh;

        return FioBResult.Create(
            cost,
            rule.BaseComponent,
            rule.Post,
            rule.ProgressivePercent,
            baseComponent.Value,
            compensatedEnergyKWh);
    }
}
