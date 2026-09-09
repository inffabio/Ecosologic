namespace Ecosologic.Domain.Solar;

internal static class TariffRules
{
    public static bool IsSubgroupInGroup(TariffGroup group, TariffSubgroup subgroup) => subgroup switch
    {
        TariffSubgroup.A1 or TariffSubgroup.A2 or TariffSubgroup.A3 or TariffSubgroup.A3a
            or TariffSubgroup.A4 or TariffSubgroup.AS => group == TariffGroup.A,
        TariffSubgroup.B1 or TariffSubgroup.B2 or TariffSubgroup.B3 or TariffSubgroup.B4 => group == TariffGroup.B,
        _ => false
    };

    public static bool IsModalityValid(TariffGroup group, TariffSubgroup subgroup, TariffModality modality) => modality switch
    {
        TariffModality.Conventional => group == TariffGroup.B,
        TariffModality.Blue => group == TariffGroup.A,
        TariffModality.Green => group == TariffGroup.A && subgroup is TariffSubgroup.A3a or TariffSubgroup.A4 or TariffSubgroup.AS,
        TariffModality.White => group == TariffGroup.B && subgroup is TariffSubgroup.B1 or TariffSubgroup.B2 or TariffSubgroup.B3,
        _ => false
    };

    public static bool IsPostValidForModality(TariffModality modality, TariffPost post) => (modality, post) switch
    {
        (TariffModality.Conventional, TariffPost.Single) => true,
        (TariffModality.White, TariffPost.Peak) => true,
        (TariffModality.White, TariffPost.Intermediate) => true,
        (TariffModality.White, TariffPost.OffPeak) => true,
        (TariffModality.Blue, TariffPost.Peak) => true,
        (TariffModality.Blue, TariffPost.OffPeak) => true,
        (TariffModality.Green, TariffPost.Peak) => true,
        (TariffModality.Green, TariffPost.OffPeak) => true,
        _ => false
    };

    public static bool IsComponentKindAllowed(TariffGroup group, TariffComponentKind kind) => (group, kind) switch
    {
        (TariffGroup.A, TariffComponentKind.DEMAND or TariffComponentKind.OVERAGE) => true,
        (TariffGroup.B, TariffComponentKind.DEMAND or TariffComponentKind.OVERAGE) => false,
        _ => true
    };
}
