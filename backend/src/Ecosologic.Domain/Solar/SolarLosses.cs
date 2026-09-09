namespace Ecosologic.Domain.Solar;

/// <summary>
/// Perdas individuais do sistema fotovoltaico, expressas como fração em [0, 1].
/// As perdas são totalizadas de forma aditiva (soma), reproduzindo o fator de perdas da planilha.
/// </summary>
public sealed class SolarLosses
{
    private SolarLosses(
        double sombreamento,
        double sujeira,
        double tolerancia,
        double mismatch,
        double temperatura,
        double cc,
        double mppt,
        double inversor,
        double ca)
    {
        Sombreamento = sombreamento;
        Sujeira = sujeira;
        Tolerancia = tolerancia;
        Mismatch = mismatch;
        Temperatura = temperatura;
        Cc = cc;
        Mppt = mppt;
        Inversor = inversor;
        Ca = ca;
    }

    public double Sombreamento { get; }
    public double Sujeira { get; }
    public double Tolerancia { get; }
    public double Mismatch { get; }
    public double Temperatura { get; }
    public double Cc { get; }
    public double Mppt { get; }
    public double Inversor { get; }
    public double Ca { get; }

    public double TotalLosses =>
        Sombreamento + Sujeira + Tolerancia + Mismatch + Temperatura + Cc + Mppt + Inversor + Ca;

    public static SolarLosses Create(
        double sombreamento,
        double sujeira,
        double tolerancia,
        double mismatch,
        double temperatura,
        double cc,
        double mppt,
        double inversor,
        double ca)
    {
        sombreamento = SolarSizingValidation.RequireLoss(sombreamento, nameof(sombreamento));
        sujeira = SolarSizingValidation.RequireLoss(sujeira, nameof(sujeira));
        tolerancia = SolarSizingValidation.RequireLoss(tolerancia, nameof(tolerancia));
        mismatch = SolarSizingValidation.RequireLoss(mismatch, nameof(mismatch));
        temperatura = SolarSizingValidation.RequireLoss(temperatura, nameof(temperatura));
        cc = SolarSizingValidation.RequireLoss(cc, nameof(cc));
        mppt = SolarSizingValidation.RequireLoss(mppt, nameof(mppt));
        inversor = SolarSizingValidation.RequireLoss(inversor, nameof(inversor));
        ca = SolarSizingValidation.RequireLoss(ca, nameof(ca));

        return new SolarLosses(sombreamento, sujeira, tolerancia, mismatch, temperatura, cc, mppt, inversor, ca);
    }
}
