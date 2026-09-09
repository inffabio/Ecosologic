using Ecosologic.Domain.Solar;

namespace Ecosologic.Application.Solar;

/// <summary>
/// Entrada do cálculo do Fio B: regra de compensação já resolvida, base tarifária
/// correspondente e energia compensada (kWh) no posto.
/// </summary>
public sealed record FioBRequest(
    GridCompensationRule Rule,
    TariffComponent BaseComponent,
    decimal CompensatedEnergyKWh);

/// <summary>
/// Resultado do Fio B: custo aplicado e metadados para rastreabilidade.
/// O Fio B nunca é uma tarifa universal — o percentual incide apenas sobre a
/// base configurada na regra e sobre a energia compensada.
/// </summary>
public sealed class FioBResult
{
    private FioBResult(
        decimal cost,
        TariffComponentKind baseComponentKind,
        TariffPost post,
        decimal progressivePercent,
        decimal baseRate,
        decimal compensatedEnergyKWh)
    {
        Cost = cost;
        BaseComponentKind = baseComponentKind;
        Post = post;
        ProgressivePercent = progressivePercent;
        BaseRate = baseRate;
        CompensatedEnergyKWh = compensatedEnergyKWh;
    }

    public decimal Cost { get; }
    public TariffComponentKind BaseComponentKind { get; }
    public TariffPost Post { get; }
    public decimal ProgressivePercent { get; }
    public decimal BaseRate { get; }
    public decimal CompensatedEnergyKWh { get; }

    internal static FioBResult Create(
        decimal cost,
        TariffComponentKind baseComponentKind,
        TariffPost post,
        decimal progressivePercent,
        decimal baseRate,
        decimal compensatedEnergyKWh) =>
        new(cost, baseComponentKind, post, progressivePercent, baseRate, compensatedEnergyKWh);
}
