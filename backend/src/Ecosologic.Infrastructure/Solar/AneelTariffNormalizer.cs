using System.Globalization;
using System.Text;
using Ecosologic.Application.Solar;
using Ecosologic.Domain.Solar;

namespace Ecosologic.Infrastructure.Solar;

/// <summary>
/// Converte registros brutos da ANEEL em perfis/componentes tarifários validados.
/// Aceita somente as distribuidoras cobertas por <see cref="AneelDistributors"/> —
/// tanto o rótulo do relatório ("Light", "Enel RJ") quanto o nome canônico oficial
/// ("Light Serviços de Eletricidade S.A.", "Enel Distribuição Rio") mapeiam para o
/// mesmo <see cref="Distributor"/> de domínio; qualquer outro nome é descartado como
/// alias desconhecido. Aceita subgrupos B1/B2/B3 com grupo "B" explícito, componente
/// "TUSD Fio B" e unidade "R$/MWh". Linhas fora desse conjunto são descartadas; linhas
/// que pertencem ao conjunto mas estão incompletas ou malformadas (incluindo grupo
/// ausente) lançam <see cref="AneelNormalizationException"/>. Vigências sobrepostas
/// para a mesma combinação são rejeitadas. Ao final, a cobertura esperada (ambas as
/// distribuidoras e os três subgrupos) é obrigatória — uma importação parcial lança
/// <see cref="AneelCoverageException"/> em vez de ser aceita silenciosamente. O
/// hash da fonte e o rótulo original da distribuidora são propagados para cada perfil
/// normalizado para proveniência.
/// </summary>
public sealed class AneelTariffNormalizer
{
    public const string TargetComponent = "TUSD Fio B";
    public const string TargetUnit = "R$/MWh";
    public const decimal MwhToKwh = 1000m;

    private static readonly IReadOnlyDictionary<string, Distributor> Distributors = BuildDistributorMap();

    private static readonly TariffSubgroup[] RequiredSubgroups =
        [TariffSubgroup.B1, TariffSubgroup.B2, TariffSubgroup.B3];

    private static readonly Distributor[] RequiredDistributors =
        [Distributor.Light, Distributor.EnelRio];

    public IReadOnlyList<AneelNormalizedProfile> Normalize(
        IEnumerable<AneelTariffRecord> records,
        string sourceHash,
        AneelTariffSyncOptions options) =>
        NormalizeWithCounts(records, sourceHash, options).Profiles;

    public AneelNormalizationResult NormalizeWithCounts(
        IEnumerable<AneelTariffRecord> records,
        string sourceHash,
        AneelTariffSyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(sourceHash))
            throw new AneelNormalizationException("O hash da fonte ANEEL é obrigatório para a normalização.");

        var profiles = new Dictionary<ProfileKey, MutableProfile>();
        var acceptedRaw = 0;
        var rejectedRaw = 0;

        foreach (var record in records)
        {
            ArgumentNullException.ThrowIfNull(record);

            if (!TryMatchDistributor(record.DistributorName, out var distributor))
            {
                rejectedRaw++;
                continue;
            }

            var sourceDistributorName = record.DistributorName!.Trim();

            if (!IsTargetComponent(record.Component))
            {
                rejectedRaw++;
                continue;
            }

            if (!IsTargetUnit(record.Unit))
            {
                rejectedRaw++;
                continue;
            }

            if (!TryParseSubgroup(record.Subgroup, out var subgroup))
            {
                rejectedRaw++;
                continue;
            }

            RequireGroupB(record.Group);

            var sourceValue = ParseValue(record.Value);
            var start = ParseStart(record.ValidityStart);
            var end = ParseEnd(record.ValidityEnd, start);
            var modality = ParseModality(record.Modality);
            var post = ParsePost(record.Post, modality);
            var resolutionCode = RequireText(record.ResolutionCode, "Resolução homologatória ausente na linha ANEEL.");

            var component = new AneelNormalizedComponent(
                TariffComponentKind.FIO_B,
                TariffUnit.KWh,
                post,
                sourceValue / MwhToKwh,
                TaxIncluded: false,
                SourcePage: NormalizeOptional(record.SourcePage),
                SourceValue: sourceValue,
                SourceUnit: record.Unit!.Trim(),
                SourceDocumentHash: sourceHash);

            var key = new ProfileKey(distributor, subgroup, modality, start, end);
            if (!profiles.TryGetValue(key, out var profile))
            {
                profile = new MutableProfile(distributor, sourceDistributorName, subgroup, modality, start, end, resolutionCode);
                profiles.Add(key, profile);
            }
            else if (profile.ResolutionCode != resolutionCode)
            {
                throw new AneelNormalizationException("Resolução inconsistente entre linhas do mesmo perfil tarifário ANEEL.");
            }

            AddComponent(profile, component);
            acceptedRaw++;
        }

        if (profiles.Count == 0)
            throw new AneelNormalizationException("Nenhuma linha da fonte ANEEL atende aos filtros aceitos.");

        RejectOverlappingValidity(profiles.Keys);
        EnsureCoverage(profiles.Keys);

        var normalized = profiles.Values
            .OrderBy(profile => profile.Distributor)
            .ThenBy(profile => profile.Subgroup)
            .ThenBy(profile => profile.ValidityStart)
            .Select(profile => new AneelNormalizedProfile(
                profile.Distributor,
                profile.SourceDistributorName,
                TariffGroup.B,
                profile.Subgroup,
                profile.Modality,
                profile.ValidityStart,
                profile.ValidityEnd,
                profile.ResolutionCode,
                options.SourceUrl,
                sourceHash,
                DateTimeOffset.UtcNow,
                IsComplete: true,
                profile.Components))
            .ToList();

        return new AneelNormalizationResult(normalized, acceptedRaw, rejectedRaw);
    }

    private static void AddComponent(MutableProfile profile, AneelNormalizedComponent component)
    {
        var existing = profile.Components.FirstOrDefault(candidate =>
            candidate.Kind == component.Kind
            && candidate.Unit == component.Unit
            && candidate.Post == component.Post);

        if (existing is null)
        {
            profile.Components.Add(component);
            return;
        }

        var identical =
            existing.Value == component.Value
            && existing.SourceValue == component.SourceValue
            && existing.SourceUnit == component.SourceUnit
            && existing.SourcePage == component.SourcePage;

        if (identical)
            return;

        throw new AneelNormalizationException("Valores conflitantes para o mesmo componente no mesmo período tarifário ANEEL.");
    }

    private static void RejectOverlappingValidity(IEnumerable<ProfileKey> keys)
    {
        foreach (var group in keys.GroupBy(key => (key.Distributor, key.Subgroup, key.Modality)))
        {
            var sorted = group
                .OrderBy(key => key.ValidityStart)
                .ThenBy(key => key.ValidityEnd ?? DateOnly.MaxValue)
                .ToList();

            for (var index = 0; index < sorted.Count - 1; index++)
            {
                var currentEnd = sorted[index].ValidityEnd ?? DateOnly.MaxValue;
                if (sorted[index + 1].ValidityStart <= currentEnd)
                {
                    throw new AneelNormalizationException(
                        "Vigências sobrepostas para a mesma combinação de distribuidora/subgrupo/modalidade na fonte ANEEL.");
                }
            }
        }
    }

    private static void EnsureCoverage(IReadOnlyCollection<ProfileKey> keys)
    {
        foreach (var distributor in RequiredDistributors)
        {
            var subgroups = keys
                .Where(key => key.Distributor == distributor)
                .Select(key => key.Subgroup)
                .Distinct()
                .ToHashSet();

            foreach (var subgroup in RequiredSubgroups)
            {
                if (!subgroups.Contains(subgroup))
                {
                    throw new AneelCoverageException(
                        $"Cobertura incompleta na fonte ANEEL: nenhuma linha '{TargetComponent}' para {distributor}/{subgroup}.");
                }
            }
        }
    }

    private static bool TryMatchDistributor(string? name, out Distributor distributor)
    {
        distributor = default;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return Distributors.TryGetValue(Canonicalize(name), out distributor);
    }

    private static IReadOnlyDictionary<string, Distributor> BuildDistributorMap()
    {
        var map = new Dictionary<string, Distributor>(StringComparer.Ordinal);
        foreach (var identity in AneelDistributors.All)
        {
            map[Canonicalize(identity.ReportLabel)] = identity.Distributor;
            map[Canonicalize(identity.CanonicalName)] = identity.Distributor;
        }

        return map;
    }

    private static bool TryParseSubgroup(string? subgroup, out TariffSubgroup parsed)
    {
        parsed = default;

        switch (Canonicalize(subgroup))
        {
            case "b1":
                parsed = TariffSubgroup.B1;
                return true;
            case "b2":
                parsed = TariffSubgroup.B2;
                return true;
            case "b3":
                parsed = TariffSubgroup.B3;
                return true;
            default:
                return false;
        }
    }

    private static void RequireGroupB(string? group)
    {
        if (Canonicalize(group) != "b")
            throw new AneelNormalizationException("Grupo tarifário ausente ou inválido na linha ANEEL (esperado 'B').");
    }

    private static bool IsTargetComponent(string? component) => Canonicalize(component) == "tusd fio b";

    private static bool IsTargetUnit(string? unit) => Canonicalize(unit) == "r$/mwh";

    private static decimal ParseValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new AneelNormalizationException("Valor tarifário ausente na linha ANEEL.");

        // A fonte ANEEL usa '.' como separador decimal (formato invariante, ex.: "41.20").
        // Separadores de agrupamento (',' em cultura invariante) NÃO são aceitos: sem a
        // exclusão de AllowThousands, "41,20" seria lido silenciosamente como 4120.
        const NumberStyles valueNumberStyles = NumberStyles.Number & ~NumberStyles.AllowThousands;

        if (!decimal.TryParse(value.Trim(), valueNumberStyles, CultureInfo.InvariantCulture, out var parsed))
            throw new AneelNormalizationException("Valor tarifário malformado na linha ANEEL.");

        if (parsed < 0)
            throw new AneelNormalizationException("Valor tarifário negativo na linha ANEEL.");

        return parsed;
    }

    private static DateOnly ParseStart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new AneelNormalizationException("Vigência inicial ausente na linha ANEEL.");

        if (!DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            throw new AneelNormalizationException("Vigência inicial malformada na linha ANEEL.");

        return parsed;
    }

    private static DateOnly? ParseEnd(string? value, DateOnly start)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            throw new AneelNormalizationException("Vigência final malformada na linha ANEEL.");

        if (parsed <= start)
            throw new AneelNormalizationException("Vigência final deve ser posterior à inicial na linha ANEEL.");

        return parsed;
    }

    private static TariffModality ParseModality(string? modality) => Canonicalize(modality) switch
    {
        "" or "convencional" => TariffModality.Conventional,
        "branca" => TariffModality.White,
        "azul" => TariffModality.Blue,
        "verde" => TariffModality.Green,
        _ => throw new AneelNormalizationException("Modalidade tarifária não reconhecida na linha ANEEL.")
    };

    private static TariffPost ParsePost(string? post, TariffModality modality)
    {
        var value = Canonicalize(post);

        if (modality == TariffModality.Conventional)
        {
            return value is "" or "unico" or "convencional"
                ? TariffPost.Single
                : throw new AneelNormalizationException("Posto tarifário inválido para a modalidade convencional na linha ANEEL.");
        }

        return value switch
        {
            "ponta" => TariffPost.Peak,
            "intermediario" => TariffPost.Intermediate,
            "fora de ponta" or "fora ponta" => TariffPost.OffPeak,
            _ => throw new AneelNormalizationException("Posto tarifário não reconhecido na linha ANEEL.")
        };
    }

    private static string RequireText(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new AneelNormalizationException(message);

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Canonicalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        var withoutAccents = builder.ToString().Normalize(NormalizationForm.FormC);
        return string.Join(' ', withoutAccents.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant()
            .Trim();
    }

    private sealed record ProfileKey(
        Distributor Distributor,
        TariffSubgroup Subgroup,
        TariffModality Modality,
        DateOnly ValidityStart,
        DateOnly? ValidityEnd);

    private sealed class MutableProfile
    {
        public MutableProfile(
            Distributor distributor,
            string sourceDistributorName,
            TariffSubgroup subgroup,
            TariffModality modality,
            DateOnly validityStart,
            DateOnly? validityEnd,
            string resolutionCode)
        {
            Distributor = distributor;
            SourceDistributorName = sourceDistributorName;
            Subgroup = subgroup;
            Modality = modality;
            ValidityStart = validityStart;
            ValidityEnd = validityEnd;
            ResolutionCode = resolutionCode;
        }

        public Distributor Distributor { get; }
        public string SourceDistributorName { get; }
        public TariffSubgroup Subgroup { get; }
        public TariffModality Modality { get; }
        public DateOnly ValidityStart { get; }
        public DateOnly? ValidityEnd { get; }
        public string ResolutionCode { get; }
        public List<AneelNormalizedComponent> Components { get; } = [];
    }
}
