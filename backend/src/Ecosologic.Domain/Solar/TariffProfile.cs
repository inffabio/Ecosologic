namespace Ecosologic.Domain.Solar;

public sealed class TariffComponent
{
    private TariffComponent(
        TariffComponentKind kind,
        TariffUnit unit,
        TariffPost post,
        decimal value,
        bool taxIncluded,
        string? sourcePage)
    {
        Kind = kind;
        Unit = unit;
        Post = post;
        Value = value;
        TaxIncluded = taxIncluded;
        SourcePage = sourcePage;
    }

    public TariffComponentKind Kind { get; }
    public TariffUnit Unit { get; }
    public TariffPost Post { get; }
    public decimal Value { get; }
    public bool TaxIncluded { get; }
    public string? SourcePage { get; }

    public static TariffComponent Create(
        TariffComponentKind kind,
        TariffUnit unit,
        TariffPost post,
        decimal value,
        bool taxIncluded,
        string? sourcePage = null)
    {
        TariffValidation.RequireTariffValue(value, nameof(value));
        return new TariffComponent(kind, unit, post, value, taxIncluded, TariffValidation.NormalizeOptionalText(sourcePage));
    }
}

public sealed class TariffProfile
{
    private TariffProfile(
        Guid id,
        Distributor distributor,
        TariffGroup group,
        TariffSubgroup subgroup,
        TariffModality modality,
        DateOnly validityStart,
        DateOnly? validityEnd,
        string resolutionCode,
        string sourceUrl,
        string? sourceDocumentHash,
        DateTimeOffset accessedAt,
        bool isComplete,
        IReadOnlyList<TariffComponent> components)
    {
        Id = id;
        Distributor = distributor;
        Group = group;
        Subgroup = subgroup;
        Modality = modality;
        ValidityStart = validityStart;
        ValidityEnd = validityEnd;
        ResolutionCode = resolutionCode;
        SourceUrl = sourceUrl;
        SourceDocumentHash = sourceDocumentHash;
        AccessedAt = accessedAt;
        IsComplete = isComplete;
        Components = components;
    }

    public Guid Id { get; }
    public Distributor Distributor { get; }
    public TariffGroup Group { get; }
    public TariffSubgroup Subgroup { get; }
    public TariffModality Modality { get; }
    public DateOnly ValidityStart { get; }
    public DateOnly? ValidityEnd { get; }
    public string ResolutionCode { get; }
    public string SourceUrl { get; }
    public string? SourceDocumentHash { get; }
    public DateTimeOffset AccessedAt { get; }
    public bool IsComplete { get; }
    public IReadOnlyList<TariffComponent> Components { get; }

    public static TariffProfile Create(
        Guid id,
        Distributor distributor,
        TariffGroup group,
        TariffSubgroup subgroup,
        TariffModality modality,
        DateOnly validityStart,
        DateOnly? validityEnd,
        string resolutionCode,
        string sourceUrl,
        string? sourceDocumentHash,
        DateTimeOffset accessedAt,
        bool isComplete,
        IReadOnlyList<TariffComponent> components)
    {
        TariffValidation.RequireGuid(id, nameof(id), "Id do perfil é obrigatório.");

        if (!TariffRules.IsSubgroupInGroup(group, subgroup))
            throw new ArgumentException("Subgrupo incompatível com o grupo tarifário.", nameof(subgroup));

        if (!TariffRules.IsModalityValid(group, subgroup, modality))
            throw new ArgumentException("Modalidade tarifária incompatível com grupo/subgrupo.", nameof(modality));

        TariffValidation.RequireValidity(validityStart, validityEnd);

        resolutionCode = TariffValidation.RequireText(resolutionCode, nameof(resolutionCode), "Resolução homologatória é obrigatória.");
        sourceUrl = TariffValidation.RequireText(sourceUrl, nameof(sourceUrl), "Fonte oficial é obrigatória.");
        sourceDocumentHash = TariffValidation.NormalizeOptionalText(sourceDocumentHash);
        accessedAt = accessedAt.ToUniversalTime();

        var list = components?.ToList() ?? [];
        var seen = new HashSet<(TariffComponentKind Kind, TariffUnit Unit, TariffPost Post)>();
        foreach (var component in list)
        {
            ArgumentNullException.ThrowIfNull(component);

            if (!TariffRules.IsPostValidForModality(modality, component.Post))
                throw new ArgumentException($"Posto {component.Post} é inválido para a modalidade {modality}.", nameof(components));

            if (!TariffRules.IsComponentKindAllowed(group, component.Kind))
                throw new ArgumentException($"Componente {component.Kind} não é permitido para o grupo {group}.", nameof(components));

            if (!seen.Add((component.Kind, component.Unit, component.Post)))
                throw new ArgumentException(
                    $"Componente duplicado: {component.Kind}/{component.Unit}/{component.Post}.", nameof(components));
        }

        if (isComplete && list.Count == 0)
            throw new ArgumentException("Perfil completo exige ao menos um componente tarifário.", nameof(components));

        return new TariffProfile(
            id,
            distributor,
            group,
            subgroup,
            modality,
            validityStart,
            validityEnd,
            resolutionCode,
            sourceUrl,
            sourceDocumentHash,
            accessedAt,
            isComplete,
            list.AsReadOnly());
    }
}
