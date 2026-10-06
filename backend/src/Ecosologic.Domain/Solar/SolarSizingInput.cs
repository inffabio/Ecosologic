namespace Ecosologic.Domain.Solar;

/// <summary>
/// Entradas normalizadas do dimensionamento técnico. Sem dependência de banco ou HTTP.
/// </summary>
public sealed class SolarSizingInput
{
    private SolarSizingInput(
        IReadOnlyList<double> monthlyConsumptionKWh,
        IReadOnlyList<double> monthlyHsp,
        IReadOnlyList<double> monthlyKFactor,
        SolarModuleSpec module,
        SolarLosses losses,
        string orientation,
        double inclinationDegrees,
        double availableAreaM2,
        double oversizingFactor,
        double deratingFactor,
        SolarInverterSpec? inverter,
        int? modulesPerString,
        int? stringCount,
        IReadOnlyList<decimal>? monthlyBillAmount,
        IReadOnlyList<decimal>? monthlyBillWithSolarAmount,
        decimal? investmentAmount)
    {
        MonthlyConsumptionKWh = monthlyConsumptionKWh;
        MonthlyHsp = monthlyHsp;
        MonthlyKFactor = monthlyKFactor;
        Module = module;
        Losses = losses;
        Orientation = orientation;
        InclinationDegrees = inclinationDegrees;
        AvailableAreaM2 = availableAreaM2;
        OversizingFactor = oversizingFactor;
        DeratingFactor = deratingFactor;
        Inverter = inverter;
        ModulesPerString = modulesPerString;
        StringCount = stringCount;
        MonthlyBillAmount = monthlyBillAmount;
        MonthlyBillWithSolarAmount = monthlyBillWithSolarAmount;
        InvestmentAmount = investmentAmount;
    }

    public IReadOnlyList<double> MonthlyConsumptionKWh { get; }
    public IReadOnlyList<double> MonthlyHsp { get; }
    public IReadOnlyList<double> MonthlyKFactor { get; }
    public SolarModuleSpec Module { get; }
    public SolarLosses Losses { get; }
    public string Orientation { get; }
    public double InclinationDegrees { get; }
    public double AvailableAreaM2 { get; }
    public double OversizingFactor { get; }
    public double DeratingFactor { get; }
    public SolarInverterSpec? Inverter { get; }
    public int? ModulesPerString { get; }
    public int? StringCount { get; }
    public IReadOnlyList<decimal>? MonthlyBillAmount { get; }
    public IReadOnlyList<decimal>? MonthlyBillWithSolarAmount { get; }
    public decimal? InvestmentAmount { get; }

    public static SolarSizingInput Create(
        IReadOnlyList<double> monthlyConsumptionKWh,
        IReadOnlyList<double> monthlyHsp,
        IReadOnlyList<double> monthlyKFactor,
        SolarModuleSpec module,
        SolarLosses losses,
        string orientation,
        double inclinationDegrees,
        double availableAreaM2,
        double oversizingFactor = 1.0,
        double? deratingFactor = null,
        SolarInverterSpec? inverter = null,
        int? modulesPerString = null,
        int? stringCount = null,
        IReadOnlyList<decimal>? monthlyBillAmount = null,
        IReadOnlyList<decimal>? monthlyBillWithSolarAmount = null,
        decimal? investmentAmount = null)
    {
        var consumption = SolarSizingValidation.RequireMonthlySeries(monthlyConsumptionKWh, nameof(monthlyConsumptionKWh));
        var hsp = SolarSizingValidation.RequireMonthlySeries(monthlyHsp, nameof(monthlyHsp));
        var kFactor = SolarSizingValidation.RequireMonthlySeries(monthlyKFactor, nameof(monthlyKFactor));

        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(losses);

        orientation = SolarSizingValidation.RequireText(orientation, nameof(orientation), "Orientação é obrigatória.");

        inclinationDegrees = SolarSizingValidation.RequireFinite(inclinationDegrees, nameof(inclinationDegrees));
        if (inclinationDegrees is < 0 or > 90)
            throw new ArgumentOutOfRangeException(nameof(inclinationDegrees), "Inclinação deve estar entre 0 e 90 graus.");

        availableAreaM2 = SolarSizingValidation.RequireFinitePositive(availableAreaM2, nameof(availableAreaM2));

        oversizingFactor = SolarSizingValidation.RequireFinite(oversizingFactor, nameof(oversizingFactor));
        if (oversizingFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(oversizingFactor), "Fator de sobredimensionamento deve ser maior que zero.");

        var derating = deratingFactor ?? 1.0;
        derating = SolarSizingValidation.RequireDerating(derating, nameof(deratingFactor));

        if (modulesPerString is <= 0)
            throw new ArgumentOutOfRangeException(nameof(modulesPerString), "Módulos por string deve ser no mínimo 1.");

        if (stringCount is <= 0)
            throw new ArgumentOutOfRangeException(nameof(stringCount), "Número de strings deve ser no mínimo 1.");

        if ((monthlyBillAmount is null) != (monthlyBillWithSolarAmount is null))
            throw new ArgumentException("As séries de fatura sem e com solar devem ser informadas juntas.");

        if (monthlyBillAmount is not null)
        {
            RequireMonthlyMoney(monthlyBillAmount, nameof(monthlyBillAmount));
            RequireMonthlyMoney(monthlyBillWithSolarAmount!, nameof(monthlyBillWithSolarAmount));
        }

        if (investmentAmount is < 0)
            throw new ArgumentOutOfRangeException(nameof(investmentAmount), "Investimento não pode ser negativo.");

        return new SolarSizingInput(
            consumption,
            hsp,
            kFactor,
            module,
            losses,
            orientation,
            inclinationDegrees,
            availableAreaM2,
            oversizingFactor,
            derating,
            inverter,
            modulesPerString,
            stringCount,
            monthlyBillAmount,
            monthlyBillWithSolarAmount,
            investmentAmount);
    }

    private static void RequireMonthlyMoney(IReadOnlyList<decimal> values, string name)
    {
        if (values.Count != 12)
            throw new ArgumentException($"{name} deve conter exatamente 12 valores.", name);
        if (values.Any(value => value < 0 || decimal.Round(value, 2) != value))
            throw new ArgumentOutOfRangeException(name, "Valores de fatura devem ser não negativos e ter no máximo 2 casas decimais.");
    }
}
