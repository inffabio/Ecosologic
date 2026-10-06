namespace Ecosologic.Api.Contracts;

public sealed class ProposalGenerateRequest
{
    public Guid QuoteId { get; set; }
    public string TemplateVersion { get; set; } = "proposal-1.0";
    public string PayloadJson { get; set; } = "{}";
}

public sealed record ProposalResponse(Guid Id, Guid QuoteId, string Status, string TemplateVersion, string FileUrl, string FileHash);
