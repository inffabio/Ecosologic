namespace Ecosologic.Domain.Solar;

public sealed class GridCompensationRule
{
    private GridCompensationRule(
        Guid id,
        Distributor distributor,
        TariffGroup group,
        TariffSubgroup subgroup,
        TariffModality modality,
        TariffPost post,
        int referenceYear,
        DateOnly validityStart,
        DateOnly? validityEnd,
        decimal progressivePercent,
        TariffComponentKind baseComponent,
        string resolutionCode,
        string sourceUrl,
        string? sourceDocumentHash,
        DateTimeOffset accessedAt,
        bool isComplete)
    {
        Id = id;
        Distributor = distributor;
        Group = group;
        Subgroup = subgroup;
        Modality = modality;
        Post = post;
        ReferenceYear = referenceYear;
        ValidityStart = validityStart;
        ValidityEnd = validityEnd;
        ProgressivePercent = progressivePercent;
        BaseComponent = baseComponent;
        ResolutionCode = resolutionCode;
        SourceUrl = sourceUrl;
        SourceDocumentHash = sourceDocumentHash;
        AccessedAt = accessedAt;
        IsComplete = isComplete;
    }

    public Guid Id { get; }
    public Distributor Distributor { get; }
    public TariffGroup Group { get; }
    public TariffSubgroup Subgroup { get; }
    public TariffModality Modality { get; }
    public TariffPost Post { get; }
    public int ReferenceYear { get; }
    public DateOnly ValidityStart { get; }
    public DateOnly? ValidityEnd { get; }
    public decimal ProgressivePercent { get; }
    public TariffComponentKind BaseComponent { get; }
    public string ResolutionCode { get; }
    public string SourceUrl { get; }
    public string? SourceDocumentHash { get; }
    public DateTimeOffset AccessedAt { get; }
    public bool IsComplete { get; }

    public static GridCompensationRule Create(
        Guid id,
        Distributor distributor,
        TariffGroup group,
        TariffSubgroup subgroup,
        TariffModality modality,
        TariffPost post,
        int referenceYear,
        DateOnly validityStart,
        DateOnly? validityEnd,
        decimal progressivePercent,
        TariffComponentKind baseComponent,
        string resolutionCode,
        string sourceUrl,
        string? sourceDocumentHash,
        DateTimeOffset accessedAt,
        bool isComplete)
    {
        TariffValidation.RequireGuid(id, nameof(id), "Id da regra é obrigatório.");

        if (!TariffRules.IsSubgroupInGroup(group, subgroup))
            throw new ArgumentException("Subgrupo incompatível com o grupo tarifário.", nameof(subgroup));

        if (!TariffRules.IsModalityValid(group, subgroup, modality))
            throw new ArgumentException("Modalidade tarifária incompatível com grupo/subgrupo.", nameof(modality));

        if (!TariffRules.IsPostValidForModality(modality, post))
            throw new ArgumentException($"Posto {post} é inválido para a modalidade {modality}.", nameof(post));

        referenceYear = TariffValidation.RequireReferenceYear(referenceYear);
        TariffValidation.RequireValidity(validityStart, validityEnd);
        progressivePercent = TariffValidation.RequirePercent(progressivePercent, nameof(progressivePercent));

        resolutionCode = TariffValidation.RequireText(resolutionCode, nameof(resolutionCode), "Resolução homologatória é obrigatória.");
        sourceUrl = TariffValidation.RequireText(sourceUrl, nameof(sourceUrl), "Fonte oficial é obrigatória.");
        sourceDocumentHash = TariffValidation.NormalizeOptionalText(sourceDocumentHash);
        accessedAt = accessedAt.ToUniversalTime();

        return new GridCompensationRule(
            id,
            distributor,
            group,
            subgroup,
            modality,
            post,
            referenceYear,
            validityStart,
            validityEnd,
            progressivePercent,
            baseComponent,
            resolutionCode,
            sourceUrl,
            sourceDocumentHash,
            accessedAt,
            isComplete);
    }
}
