namespace Ecosologic.Domain.Solar;

public enum Distributor
{
    Light = 1,
    EnelRio = 2
}

public enum TariffGroup
{
    A = 1,
    B = 2
}

public enum TariffSubgroup
{
    A1,
    A2,
    A3,
    A3a,
    A4,
    AS,
    B1,
    B2,
    B3,
    B4
}

public enum TariffModality
{
    Conventional,
    White,
    Blue,
    Green
}

public enum TariffPost
{
    Single,
    Peak,
    Intermediate,
    OffPeak
}

public enum TariffUnit
{
    KWh,
    KW,
    Month
}

public enum TariffComponentKind
{
    TE,
    TUSD,
    TUSD_DISTRIBUTION,
    TUSD_TRANSMISSION,
    FIO_B,
    TAX,
    DEMAND,
    OVERAGE,
    OTHER
}

public enum ConnectionPhase
{
    Monophase,
    Biphase,
    Triphase
}
