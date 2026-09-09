using Ecosologic.Domain.Solar;

namespace Ecosologic.Application.Solar;

/// <summary>Erro de bloqueio com código estável e mensagem explícita.</summary>
public sealed record BillError(string Code, string Message);

/// <summary>
/// Crédito de energia excedente ainda não compensado, com mês de geração, posto de
/// origem e mês após o qual expira. Preserva a rastreabilidade do posto cruzado.
/// </summary>
public sealed record CreditEntry(int GeneratedMonth, TariffPost Post, decimal RemainingKWh, int ExpiresAfterMonth);

/// <summary>
/// Detalhe mensal por posto: energia consumida/injetada/compensada e custos.
/// Os custos por linha NÃO são arredondados (mantêm precisão decimal); apenas o
/// total mensal e a fatura são arredondados para centavos.
/// </summary>
public sealed record PostBillLine(
    TariffPost Post,
    decimal ConsumptionKWh,
    decimal InjectedKWh,
    decimal CompensatedWithinMonthKWh,
    decimal CompensatedViaCreditKWh,
    decimal EnergyFromGridKWh,
    decimal TeCost,
    decimal TusdCost,
    decimal TaxCost,
    decimal FioBCost);

/// <summary>
/// Consolidação mensal: energia da rede, compensada, disponibilidade, Fio B, TE,
/// TUSD, tributos, demanda, ultrapassagem e totais (sem/com solar) e economia.
/// TotalWithoutSolar/TotalWithSolar/Savings são arredondados para centavos.
/// </summary>
public sealed record TariffBillMonth(
    int Month,
    IReadOnlyList<PostBillLine> Posts,
    decimal EnergyFromGridKWh,
    decimal CompensatedEnergyKWh,
    decimal AvailabilityKWh,
    decimal AvailabilityCost,
    decimal TeCost,
    decimal TusdCost,
    decimal TaxCost,
    decimal FioBCost,
    decimal DemandCost,
    decimal OverageCost,
    decimal TotalWithoutSolar,
    decimal TotalWithSolar,
    decimal Savings);

public sealed record TariffBillSummary(
    decimal AnnualTotalWithoutSolar,
    decimal AnnualTotalWithSolar,
    decimal AnnualSavings,
    decimal TotalCreditsKWh);

/// <summary>
/// Resultado do cálculo de fatura. Quando bloqueado (perfil/regra/componente
/// ausente ou incompleto), não há meses nem resumo — nunca zero silencioso.
/// </summary>
public sealed class TariffBillResult
{
    private TariffBillResult(
        bool isBlocked,
        IReadOnlyList<BillError> errors,
        IReadOnlyList<TariffBillMonth> months,
        TariffBillSummary? summary,
        IReadOnlyList<CreditEntry> remainingCredits)
    {
        IsBlocked = isBlocked;
        Errors = errors;
        Months = months;
        Summary = summary;
        RemainingCredits = remainingCredits;
    }

    public bool IsBlocked { get; }
    public IReadOnlyList<BillError> Errors { get; }
    public IReadOnlyList<TariffBillMonth> Months { get; }
    public TariffBillSummary? Summary { get; }
    public IReadOnlyList<CreditEntry> RemainingCredits { get; }

    internal static TariffBillResult Blocked(IReadOnlyList<BillError> errors) =>
        new(true, errors, Array.Empty<TariffBillMonth>(), null, Array.Empty<CreditEntry>());

    internal static TariffBillResult Calculated(
        IReadOnlyList<TariffBillMonth> months,
        TariffBillSummary summary,
        IReadOnlyList<CreditEntry> remainingCredits) =>
        new(false, Array.Empty<BillError>(), months, summary, remainingCredits);
}
