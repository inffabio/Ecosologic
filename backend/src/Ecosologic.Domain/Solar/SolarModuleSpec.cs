namespace Ecosologic.Domain.Solar;

/// <summary>
/// Características elétricas e físicas do módulo fotovoltaico usadas no dimensionamento
/// e na verificação de compatibilidade com o inversor.
/// </summary>
public sealed class SolarModuleSpec
{
    private SolarModuleSpec(double powerWp, double voc, double vmp, double isc, double imp, double areaM2)
    {
        PowerWp = powerWp;
        Voc = voc;
        Vmp = vmp;
        Isc = isc;
        Imp = imp;
        AreaM2 = areaM2;
    }

    /// <summary>
    /// Tolerância relativa documentada entre a potência nominal (PowerWp) e o produto Vmp × Imp.
    /// No STC, Pmp = Vmp × Imp por definição; a tolerância cobre o arredondamento de Vmp/Imp
    /// publicado em datasheets (tipicamente ±3% de tolerância de potência).
    /// </summary>
    public const double PowerCoherenceTolerance = 0.05;

    public double PowerWp { get; }
    public double Voc { get; }
    public double Vmp { get; }
    public double Isc { get; }
    public double Imp { get; }
    public double AreaM2 { get; }

    public static SolarModuleSpec Create(double powerWp, double voc, double vmp, double isc, double imp, double areaM2)
    {
        powerWp = SolarSizingValidation.RequireFinitePositive(powerWp, nameof(powerWp));
        voc = SolarSizingValidation.RequireFinitePositive(voc, nameof(voc));
        vmp = SolarSizingValidation.RequireFinitePositive(vmp, nameof(vmp));
        isc = SolarSizingValidation.RequireFinitePositive(isc, nameof(isc));
        imp = SolarSizingValidation.RequireFinitePositive(imp, nameof(imp));
        areaM2 = SolarSizingValidation.RequireFinitePositive(areaM2, nameof(areaM2));

        if (voc < vmp)
            throw new ArgumentOutOfRangeException(nameof(voc), "Voc deve ser maior ou igual a Vmp.");

        if (isc < imp)
            throw new ArgumentOutOfRangeException(nameof(isc), "Isc deve ser maior ou igual a Imp.");

        var vmpImp = vmp * imp;
        if (Math.Abs(powerWp - vmpImp) > vmpImp * PowerCoherenceTolerance)
            throw new ArgumentOutOfRangeException(nameof(powerWp),
                $"PowerWp ({powerWp:F1} W) deve ser coerente com Vmp × Imp ({vmpImp:F1} W) dentro da tolerância de {PowerCoherenceTolerance:P0}.");

        return new SolarModuleSpec(powerWp, voc, vmp, isc, imp, areaM2);
    }
}

/// <summary>
/// Limites elétricos do inversor (potência, faixa MPPT, tensão e corrente máximas).
/// </summary>
public sealed class SolarInverterSpec
{
    private SolarInverterSpec(
        double nominalPowerW,
        double mpptVoltageMin,
        double mpptVoltageMax,
        double maxInputVoltage,
        double maxInputCurrent,
        int mpptCount,
        int maxStringsPerMppt)
    {
        NominalPowerW = nominalPowerW;
        MpptVoltageMin = mpptVoltageMin;
        MpptVoltageMax = mpptVoltageMax;
        MaxInputVoltage = maxInputVoltage;
        MaxInputCurrent = maxInputCurrent;
        MpptCount = mpptCount;
        MaxStringsPerMppt = maxStringsPerMppt;
    }

    public double NominalPowerW { get; }
    public double MpptVoltageMin { get; }
    public double MpptVoltageMax { get; }
    public double MaxInputVoltage { get; }

    /// <summary>Corrente máxima de entrada por MPPT (considerando as strings em paralelo nele).</summary>
    public double MaxInputCurrent { get; }

    public int MpptCount { get; }

    /// <summary>Número máximo de strings em paralelo por MPPT.</summary>
    public int MaxStringsPerMppt { get; }

    public static SolarInverterSpec Create(
        double nominalPowerW,
        double mpptVoltageMin,
        double mpptVoltageMax,
        double maxInputVoltage,
        double maxInputCurrent,
        int mpptCount,
        int maxStringsPerMppt = 1)
    {
        nominalPowerW = SolarSizingValidation.RequireFinitePositive(nominalPowerW, nameof(nominalPowerW));
        mpptVoltageMin = SolarSizingValidation.RequireFiniteNonNegative(mpptVoltageMin, nameof(mpptVoltageMin));
        mpptVoltageMax = SolarSizingValidation.RequireFinitePositive(mpptVoltageMax, nameof(mpptVoltageMax));
        maxInputVoltage = SolarSizingValidation.RequireFinitePositive(maxInputVoltage, nameof(maxInputVoltage));
        maxInputCurrent = SolarSizingValidation.RequireFinitePositive(maxInputCurrent, nameof(maxInputCurrent));

        if (mpptVoltageMax <= mpptVoltageMin)
            throw new ArgumentOutOfRangeException(nameof(mpptVoltageMax), "Tensão máxima do MPPT deve ser maior que a mínima.");

        if (mpptCount < 1)
            throw new ArgumentOutOfRangeException(nameof(mpptCount), "Número de MPPTs deve ser no mínimo 1.");

        if (maxStringsPerMppt < 1)
            throw new ArgumentOutOfRangeException(nameof(maxStringsPerMppt), "Número máximo de strings por MPPT deve ser no mínimo 1.");

        return new SolarInverterSpec(nominalPowerW, mpptVoltageMin, mpptVoltageMax, maxInputVoltage, maxInputCurrent, mpptCount, maxStringsPerMppt);
    }
}
