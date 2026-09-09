namespace Ecosologic.Domain.Solar;

public enum SolarSizingAlertSeverity
{
    Warning,
    Blocking
}

/// <summary>
/// Alerta técnico emitido pelo motor. Impeditivos bloqueiam a emissão; não impeditivos apenas informam.
/// </summary>
public sealed class SolarSizingAlert
{
    private SolarSizingAlert(string code, SolarSizingAlertSeverity severity, string message)
    {
        Code = code;
        Severity = severity;
        Message = message;
    }

    public string Code { get; }
    public SolarSizingAlertSeverity Severity { get; }
    public string Message { get; }

    internal static SolarSizingAlert Create(string code, SolarSizingAlertSeverity severity, string message) =>
        new(code, severity, message);
}

/// <summary>
/// Resultado determinístico do dimensionamento técnico (geração, kWp, módulos, strings, cobertura e alertas).
/// </summary>
public sealed class SolarSizingResult
{
    private SolarSizingResult(
        IReadOnlyList<double> monthlyGenerationKWh,
        double annualGenerationKWh,
        double averageGenerationKWh,
        double annualConsumptionKWh,
        double totalLosses,
        double targetKwp,
        double installedKwp,
        int moduleCount,
        int modulesPerString,
        int stringCount,
        IReadOnlyList<double> monthlyEnergyCompensableKWh,
        double annualEnergyCompensableKWh,
        double coverage,
        IReadOnlyList<double> monthlyBalanceKWh,
        double surplusKWh,
        double deficitKWh,
        int worstMonthIndex,
        double worstMonthGenerationKWh,
        double worstMonthConsumptionKWh,
        double worstMonthDeficitKWh,
        IReadOnlyList<SolarSizingAlert> alerts)
    {
        MonthlyGenerationKWh = monthlyGenerationKWh;
        AnnualGenerationKWh = annualGenerationKWh;
        AverageGenerationKWh = averageGenerationKWh;
        AnnualConsumptionKWh = annualConsumptionKWh;
        TotalLosses = totalLosses;
        TargetKwp = targetKwp;
        InstalledKwp = installedKwp;
        ModuleCount = moduleCount;
        ModulesPerString = modulesPerString;
        StringCount = stringCount;
        MonthlyEnergyCompensableKWh = monthlyEnergyCompensableKWh;
        AnnualEnergyCompensableKWh = annualEnergyCompensableKWh;
        Coverage = coverage;
        MonthlyBalanceKWh = monthlyBalanceKWh;
        SurplusKWh = surplusKWh;
        DeficitKWh = deficitKWh;
        WorstMonthIndex = worstMonthIndex;
        WorstMonthGenerationKWh = worstMonthGenerationKWh;
        WorstMonthConsumptionKWh = worstMonthConsumptionKWh;
        WorstMonthDeficitKWh = worstMonthDeficitKWh;
        Alerts = alerts;
    }

    public IReadOnlyList<double> MonthlyGenerationKWh { get; }
    public double AnnualGenerationKWh { get; }
    public double AverageGenerationKWh { get; }
    public double AnnualConsumptionKWh { get; }
    public double TotalLosses { get; }
    public double TargetKwp { get; }
    public double InstalledKwp { get; }
    public int ModuleCount { get; }
    public int ModulesPerString { get; }
    public int StringCount { get; }
    public IReadOnlyList<double> MonthlyEnergyCompensableKWh { get; }
    public double AnnualEnergyCompensableKWh { get; }
    public double Coverage { get; }
    public IReadOnlyList<double> MonthlyBalanceKWh { get; }
    public double SurplusKWh { get; }
    public double DeficitKWh { get; }
    public int WorstMonthIndex { get; }
    public double WorstMonthGenerationKWh { get; }
    public double WorstMonthConsumptionKWh { get; }
    public double WorstMonthDeficitKWh { get; }
    public IReadOnlyList<SolarSizingAlert> Alerts { get; }
    public bool HasBlockingAlert => Alerts.Any(a => a.Severity == SolarSizingAlertSeverity.Blocking);

    internal static SolarSizingResult Create(
        IReadOnlyList<double> monthlyGenerationKWh,
        double annualGenerationKWh,
        double averageGenerationKWh,
        double annualConsumptionKWh,
        double totalLosses,
        double targetKwp,
        double installedKwp,
        int moduleCount,
        int modulesPerString,
        int stringCount,
        IReadOnlyList<double> monthlyEnergyCompensableKWh,
        double annualEnergyCompensableKWh,
        double coverage,
        IReadOnlyList<double> monthlyBalanceKWh,
        double surplusKWh,
        double deficitKWh,
        int worstMonthIndex,
        double worstMonthGenerationKWh,
        double worstMonthConsumptionKWh,
        double worstMonthDeficitKWh,
        IReadOnlyList<SolarSizingAlert> alerts) =>
        new(
            RequireFiniteSeries(monthlyGenerationKWh, nameof(MonthlyGenerationKWh)),
            RequireFinite(annualGenerationKWh, nameof(AnnualGenerationKWh)),
            RequireFinite(averageGenerationKWh, nameof(AverageGenerationKWh)),
            RequireFinite(annualConsumptionKWh, nameof(AnnualConsumptionKWh)),
            RequireFinite(totalLosses, nameof(TotalLosses)),
            RequireFinite(targetKwp, nameof(TargetKwp)),
            RequireFinite(installedKwp, nameof(InstalledKwp)),
            moduleCount,
            modulesPerString,
            stringCount,
            RequireFiniteSeries(monthlyEnergyCompensableKWh, nameof(MonthlyEnergyCompensableKWh)),
            RequireFinite(annualEnergyCompensableKWh, nameof(AnnualEnergyCompensableKWh)),
            RequireFinite(coverage, nameof(Coverage)),
            RequireFiniteSeries(monthlyBalanceKWh, nameof(MonthlyBalanceKWh)),
            RequireFinite(surplusKWh, nameof(SurplusKWh)),
            RequireFinite(deficitKWh, nameof(DeficitKWh)),
            worstMonthIndex,
            RequireFinite(worstMonthGenerationKWh, nameof(WorstMonthGenerationKWh)),
            RequireFinite(worstMonthConsumptionKWh, nameof(WorstMonthConsumptionKWh)),
            RequireFinite(worstMonthDeficitKWh, nameof(WorstMonthDeficitKWh)),
            alerts);

    // Validação final: nenhum valor numérico do resultado pode ser NaN/Infinity.
    // Overflow silencioso nos intermediários é rejeitado antes de retornar.
    private static double RequireFinite(double value, string name)
    {
        if (!double.IsFinite(value))
            throw new InvalidOperationException(
                $"Resultado '{name}' é não finito (NaN/Infinity); verifique as entradas do dimensionamento.");
        return value;
    }

    private static IReadOnlyList<double> RequireFiniteSeries(IReadOnlyList<double> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values);
        for (var i = 0; i < values.Count; i++)
            RequireFinite(values[i], $"{name}[{i}]");
        return values;
    }
}
