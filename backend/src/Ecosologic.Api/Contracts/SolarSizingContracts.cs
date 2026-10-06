using Ecosologic.Domain.Solar;

namespace Ecosologic.Api.Contracts;

public sealed class SolarSizingDraftRequest
{
    public Guid LeadId { get; set; }
    public string Distributor { get; set; } = "";
    public Guid? DistributorId { get; set; }
    public string Group { get; set; } = "";
    public string Modality { get; set; } = "";
    public string Connection { get; set; } = "";
    public IReadOnlyList<decimal> MonthlyConsumptionKWh { get; set; } = [];
    public IReadOnlyList<decimal> MonthlyBillAmount { get; set; } = [];
    public Guid? ModuleMaterialId { get; set; }
    public Guid? InverterMaterialId { get; set; }
    public DateOnly? ProtocolDate { get; set; }
    public string Address { get; set; } = "";
    public string AssumptionsJson { get; set; } = "{}";
}

public sealed record SolarSizingDraftResponse(Guid Id, string Status);

public sealed class SolarSizingCalculationRequest
{
    public IReadOnlyList<double> MonthlyHsp { get; set; } = [];
    public IReadOnlyList<double> MonthlyKFactor { get; set; } = [];
    public SolarModuleRequest Module { get; set; } = new();
    public SolarLossesRequest Losses { get; set; } = new();
    public string Orientation { get; set; } = "";
    public double InclinationDegrees { get; set; }
    public double AvailableAreaM2 { get; set; }
    public double OversizingFactor { get; set; } = 1;
    public double? DeratingFactor { get; set; }
    public SolarInverterRequest? Inverter { get; set; }
    public int? ModulesPerString { get; set; }
    public int? StringCount { get; set; }
    public IReadOnlyList<decimal>? MonthlyBillWithSolarAmount { get; set; }
    public decimal? InvestmentAmount { get; set; }
}

public sealed class SolarModuleRequest
{
    public double PowerWp { get; set; }
    public double Voc { get; set; }
    public double Vmp { get; set; }
    public double Isc { get; set; }
    public double Imp { get; set; }
    public double AreaM2 { get; set; }
}

public sealed class SolarLossesRequest
{
    public double Sombreamento { get; set; }
    public double Sujeira { get; set; }
    public double Tolerancia { get; set; }
    public double Mismatch { get; set; }
    public double Temperatura { get; set; }
    public double Cc { get; set; }
    public double Mppt { get; set; }
    public double Inversor { get; set; }
    public double Ca { get; set; }
}

public sealed class SolarInverterRequest
{
    public double NominalPowerW { get; set; }
    public double MpptVoltageMin { get; set; }
    public double MpptVoltageMax { get; set; }
    public double MaxInputVoltage { get; set; }
    public double MaxInputCurrent { get; set; }
    public int MpptCount { get; set; }
    public int MaxStringsPerMppt { get; set; } = 1;
}

public sealed record SolarSizingCalculationResponse(Guid Id, string Status, SolarSizingResult Result);
