namespace Ecosologic.Domain.Solar;

public sealed record SolarChartPoint(string Label, double Value, string Unit);

public sealed record SolarChartSeries(
    string Key,
    string Unit,
    IReadOnlyList<SolarChartPoint> Points);

public sealed class SolarSizingCharts
{
    public SolarSizingCharts(
        IReadOnlyList<SolarChartSeries> generationVsConsumption,
        IReadOnlyList<SolarChartSeries> financialCashFlow,
        IReadOnlyList<SolarChartSeries> annualBillComparison)
    {
        GenerationVsConsumption = generationVsConsumption;
        FinancialCashFlow = financialCashFlow;
        AnnualBillComparison = annualBillComparison;
    }

    public IReadOnlyList<SolarChartSeries> GenerationVsConsumption { get; }
    public IReadOnlyList<SolarChartSeries> FinancialCashFlow { get; }
    public IReadOnlyList<SolarChartSeries> AnnualBillComparison { get; }
}
