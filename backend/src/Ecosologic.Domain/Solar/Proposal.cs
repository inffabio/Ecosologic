namespace Ecosologic.Domain.Solar;

public enum ProposalStatus
{
    Draft,
    Generated,
    Sent,
    Accepted,
    Rejected,
    Expired
}

public sealed class Proposal
{
    private Proposal(Guid quoteId, string templateVersion, string payloadJson)
    {
        Id = Guid.NewGuid();
        QuoteId = quoteId;
        TemplateVersion = templateVersion;
        PayloadJson = payloadJson;
        Status = ProposalStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; }
    public Guid QuoteId { get; }
    public string TemplateVersion { get; }
    public string PayloadJson { get; }
    public string? FileUrl { get; private set; }
    public string? FileHash { get; private set; }
    public ProposalStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Proposal CreateDraft(SolarQuote quote, string templateVersion, string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(quote);

        if (quote.Status != SolarQuoteStatus.Approved)
            throw new InvalidOperationException(
                "Proposta só pode ser criada a partir de cotação aprovada.");

        templateVersion = SolarValidation.RequireText(templateVersion, nameof(templateVersion), "Versão do template é obrigatória.");
        payloadJson = SolarValidation.RequireJson(payloadJson, nameof(payloadJson), "Snapshot do payload é obrigatório.");

        return new Proposal(quote.Id, templateVersion, payloadJson);
    }

    public void Generate(SolarQuote quote, string fileUrl, string? fileHash)
    {
        ArgumentNullException.ThrowIfNull(quote);

        if (quote.Id != QuoteId)
            throw new InvalidOperationException(
                "Proposta só pode ser gerada pela cotação vinculada.");

        if (quote.Status != SolarQuoteStatus.Approved)
            throw new InvalidOperationException(
                "Proposta só pode ser gerada a partir de cotação aprovada.");

        fileUrl = SolarValidation.RequireText(fileUrl, nameof(fileUrl), "URL do arquivo é obrigatória.");

        TransitionTo(ProposalStatus.Generated);
        FileUrl = fileUrl;
        FileHash = fileHash;
        Touch();
    }

    public void Send()
    {
        TransitionTo(ProposalStatus.Sent);
        Touch();
    }

    public void Accept()
    {
        TransitionTo(ProposalStatus.Accepted);
        Touch();
    }

    public void Reject()
    {
        TransitionTo(ProposalStatus.Rejected);
        Touch();
    }

    public void Expire()
    {
        TransitionTo(ProposalStatus.Expired);
        Touch();
    }

    public bool CanTransitionTo(ProposalStatus target) =>
        SolarTransitions.CanProposalTransition(Status, target);

    private void TransitionTo(ProposalStatus target)
    {
        if (!CanTransitionTo(target))
            throw new InvalidOperationException($"Transição inválida de Proposal {Status} para {target}.");
        Status = target;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
