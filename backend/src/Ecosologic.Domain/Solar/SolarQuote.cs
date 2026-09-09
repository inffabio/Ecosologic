namespace Ecosologic.Domain.Solar;

public enum SolarQuoteStatus
{
    Draft,
    Approved,
    Sent,
    Accepted,
    Rejected,
    Expired
}

public sealed class SolarQuote
{
    private SolarQuote(
        Guid sizingId,
        string itemsJson,
        decimal totalCost,
        decimal marginPercent,
        decimal taxPercent,
        decimal totalPrice,
        string conditionsJson,
        DateTimeOffset validUntil,
        string snapshotJson)
    {
        Id = Guid.NewGuid();
        SizingId = sizingId;
        ItemsJson = itemsJson;
        TotalCost = totalCost;
        MarginPercent = marginPercent;
        TaxPercent = taxPercent;
        TotalPrice = totalPrice;
        ConditionsJson = conditionsJson;
        ValidUntil = validUntil;
        SnapshotJson = snapshotJson;
        Status = SolarQuoteStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; }
    public Guid SizingId { get; }
    public string ItemsJson { get; }
    public decimal TotalCost { get; }
    public decimal MarginPercent { get; }
    public decimal TaxPercent { get; }
    public decimal TotalPrice { get; }
    public string ConditionsJson { get; }
    public DateTimeOffset ValidUntil { get; }
    public string SnapshotJson { get; }
    public SolarQuoteStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static SolarQuote CreateDraft(
        SolarSizing sizing,
        string itemsJson,
        decimal totalCost,
        decimal marginPercent,
        decimal taxPercent,
        decimal totalPrice,
        string conditionsJson,
        DateTimeOffset validUntil,
        string snapshotJson)
    {
        ArgumentNullException.ThrowIfNull(sizing);

        if (sizing.Status is not (SolarSizingStatus.Calculated or SolarSizingStatus.Approved))
            throw new InvalidOperationException(
                "Cotação só pode ser criada a partir de dimensionamento calculado ou aprovado.");

        itemsJson = SolarValidation.RequireJson(itemsJson, nameof(itemsJson), "Itens são obrigatórios.");
        conditionsJson = SolarValidation.RequireJson(conditionsJson, nameof(conditionsJson), "Condições são obrigatórias.");
        snapshotJson = SolarValidation.RequireJson(snapshotJson, nameof(snapshotJson), "Snapshot é obrigatório.");

        SolarValidation.RequireMoney(totalCost, nameof(totalCost));
        SolarValidation.RequireMoney(totalPrice, nameof(totalPrice));
        SolarValidation.RequirePercent(marginPercent, nameof(marginPercent));
        SolarValidation.RequirePercent(taxPercent, nameof(taxPercent));

        return new SolarQuote(
            sizing.Id,
            itemsJson,
            totalCost,
            marginPercent,
            taxPercent,
            totalPrice,
            conditionsJson,
            validUntil,
            snapshotJson);
    }

    public void Approve(SolarSizing sizing)
    {
        ArgumentNullException.ThrowIfNull(sizing);

        if (sizing.Id != SizingId)
            throw new InvalidOperationException(
                "Cotação só pode ser aprovada pelo dimensionamento vinculado.");

        if (sizing.Status != SolarSizingStatus.Approved)
            throw new InvalidOperationException(
                "Cotação só pode ser aprovada a partir de dimensionamento aprovado.");

        TransitionTo(SolarQuoteStatus.Approved);
        Touch();
    }

    public void Send()
    {
        TransitionTo(SolarQuoteStatus.Sent);
        Touch();
    }

    public void Accept()
    {
        TransitionTo(SolarQuoteStatus.Accepted);
        Touch();
    }

    public void Reject()
    {
        TransitionTo(SolarQuoteStatus.Rejected);
        Touch();
    }

    public void Expire()
    {
        TransitionTo(SolarQuoteStatus.Expired);
        Touch();
    }

    public bool CanTransitionTo(SolarQuoteStatus target) =>
        SolarTransitions.CanQuoteTransition(Status, target);

    private void TransitionTo(SolarQuoteStatus target)
    {
        if (!CanTransitionTo(target))
            throw new InvalidOperationException($"Transição inválida de SolarQuote {Status} para {target}.");
        Status = target;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
