namespace Ecosologic.Domain.Solar;

public static class TariffAvailability
{
    public const decimal MonophaseKWh = 30m;
    public const decimal BiphaseKWh = 50m;
    public const decimal TriphaseKWh = 100m;

    public static decimal MinimumMonthlyKWh(ConnectionPhase phase) => phase switch
    {
        ConnectionPhase.Monophase => MonophaseKWh,
        ConnectionPhase.Biphase => BiphaseKWh,
        ConnectionPhase.Triphase => TriphaseKWh,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Fase de ligação desconhecida.")
    };

    public static bool AppliesTo(TariffGroup group) => group == TariffGroup.B;
}
