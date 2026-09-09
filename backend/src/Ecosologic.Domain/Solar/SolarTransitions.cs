namespace Ecosologic.Domain.Solar;

internal static class SolarTransitions
{
    public static bool CanSizingTransition(SolarSizingStatus from, SolarSizingStatus to) => (from, to) switch
    {
        (SolarSizingStatus.Draft, SolarSizingStatus.Calculated) => true,
        (SolarSizingStatus.Draft, SolarSizingStatus.Cancelled) => true,
        (SolarSizingStatus.Calculated, SolarSizingStatus.Approved) => true,
        (SolarSizingStatus.Calculated, SolarSizingStatus.Cancelled) => true,
        (SolarSizingStatus.Approved, SolarSizingStatus.Cancelled) => true,
        _ => false
    };

    public static bool CanQuoteTransition(SolarQuoteStatus from, SolarQuoteStatus to) => (from, to) switch
    {
        (SolarQuoteStatus.Draft, SolarQuoteStatus.Approved) => true,
        (SolarQuoteStatus.Draft, SolarQuoteStatus.Expired) => true,
        (SolarQuoteStatus.Approved, SolarQuoteStatus.Sent) => true,
        (SolarQuoteStatus.Approved, SolarQuoteStatus.Expired) => true,
        (SolarQuoteStatus.Sent, SolarQuoteStatus.Accepted) => true,
        (SolarQuoteStatus.Sent, SolarQuoteStatus.Rejected) => true,
        (SolarQuoteStatus.Sent, SolarQuoteStatus.Expired) => true,
        _ => false
    };

    public static bool CanProposalTransition(ProposalStatus from, ProposalStatus to) => (from, to) switch
    {
        (ProposalStatus.Draft, ProposalStatus.Generated) => true,
        (ProposalStatus.Generated, ProposalStatus.Sent) => true,
        (ProposalStatus.Generated, ProposalStatus.Expired) => true,
        (ProposalStatus.Sent, ProposalStatus.Accepted) => true,
        (ProposalStatus.Sent, ProposalStatus.Rejected) => true,
        (ProposalStatus.Sent, ProposalStatus.Expired) => true,
        _ => false
    };
}
