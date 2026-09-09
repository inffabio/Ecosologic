using Ecosologic.Domain.Solar;

namespace Ecosologic.Application.Solar;

/// <summary>
/// Postos aplicáveis por modalidade tarifária (espelha as regras do domínio).
/// </summary>
internal static class TariffBillPosts
{
    public static IReadOnlyList<TariffPost> For(TariffModality modality) => modality switch
    {
        TariffModality.Conventional => [TariffPost.Single],
        TariffModality.White => [TariffPost.Peak, TariffPost.Intermediate, TariffPost.OffPeak],
        TariffModality.Blue => [TariffPost.Peak, TariffPost.OffPeak],
        TariffModality.Green => [TariffPost.Peak, TariffPost.OffPeak],
        _ => throw new ArgumentOutOfRangeException(nameof(modality), modality, "Modalidade tarifária desconhecida.")
    };
}

/// <summary>
/// Entradas normalizadas do cálculo de fatura (com e sem solar). Recebe o perfil
/// tarifário e as regras de Fio B JÁ RESOLVIDAS (por posto) e as séries mensais de
/// consumo/injeção. Não depende de banco ou HTTP.
///
/// Tipos monetários e de energia usam <see cref="decimal"/> (sem NaN/Infinity);
/// negativos são rejeitados; overflow de multiplicação propaga como exceção.
/// </summary>
public sealed class TariffBillInput
{
    private TariffBillInput(
        TariffProfile profile,
        IReadOnlyDictionary<TariffPost, GridCompensationRule> fioBRules,
        IReadOnlyDictionary<TariffPost, IReadOnlyList<decimal>> consumptionByPost,
        IReadOnlyDictionary<TariffPost, IReadOnlyList<decimal>> injectionByPost,
        ConnectionPhase? connectionPhase,
        IReadOnlyList<decimal>? contractedDemandKw,
        IReadOnlyList<decimal>? measuredDemandKw,
        int creditExpiryMonths,
        int? referenceYear,
        DateOnly? referenceDate)
    {
        Profile = profile;
        FioBRules = fioBRules;
        ConsumptionByPost = consumptionByPost;
        InjectionByPost = injectionByPost;
        ConnectionPhase = connectionPhase;
        ContractedDemandKw = contractedDemandKw;
        MeasuredDemandKw = measuredDemandKw;
        CreditExpiryMonths = creditExpiryMonths;
        ReferenceYear = referenceYear;
        ReferenceDate = referenceDate;
    }

    public TariffProfile Profile { get; }
    public IReadOnlyDictionary<TariffPost, GridCompensationRule> FioBRules { get; }
    public IReadOnlyDictionary<TariffPost, IReadOnlyList<decimal>> ConsumptionByPost { get; }
    public IReadOnlyDictionary<TariffPost, IReadOnlyList<decimal>> InjectionByPost { get; }
    public ConnectionPhase? ConnectionPhase { get; }
    public IReadOnlyList<decimal>? ContractedDemandKw { get; }
    public IReadOnlyList<decimal>? MeasuredDemandKw { get; }
    public int CreditExpiryMonths { get; }
    public int? ReferenceYear { get; }
    public DateOnly? ReferenceDate { get; }

    public static TariffBillInput Create(
        TariffProfile profile,
        IReadOnlyDictionary<TariffPost, GridCompensationRule> fioBRules,
        IReadOnlyDictionary<TariffPost, IReadOnlyList<decimal>> consumptionByPost,
        IReadOnlyDictionary<TariffPost, IReadOnlyList<decimal>> injectionByPost,
        ConnectionPhase? connectionPhase = null,
        IReadOnlyList<decimal>? contractedDemandKw = null,
        IReadOnlyList<decimal>? measuredDemandKw = null,
        int creditExpiryMonths = 60,
        int? referenceYear = null,
        DateOnly? referenceDate = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(fioBRules);
        ArgumentNullException.ThrowIfNull(consumptionByPost);
        ArgumentNullException.ThrowIfNull(injectionByPost);

        if (creditExpiryMonths < 1)
            throw new ArgumentOutOfRangeException(nameof(creditExpiryMonths), "Prazo de expiração de créditos deve ser no mínimo 1 mês.");

        if (referenceYear is < 2000 or > 2200)
            throw new ArgumentOutOfRangeException(nameof(referenceYear), "Ano de referência fora do intervalo esperado.");

        var posts = TariffBillPosts.For(profile.Modality);

        var consumption = new Dictionary<TariffPost, IReadOnlyList<decimal>>();
        var injection = new Dictionary<TariffPost, IReadOnlyList<decimal>>();
        var rules = new Dictionary<TariffPost, GridCompensationRule>();

        foreach (var post in posts)
        {
            if (!consumptionByPost.TryGetValue(post, out var cons))
                throw new ArgumentException($"Consumo ausente para o posto {post}.", nameof(consumptionByPost));

            if (!injectionByPost.TryGetValue(post, out var inj))
                throw new ArgumentException($"Injeção ausente para o posto {post}.", nameof(injectionByPost));

            consumption[post] = RequireMonthlySeries(cons, $"consumption[{post}]");
            injection[post] = RequireMonthlySeries(inj, $"injection[{post}]");

            // A ausência/incompleteza da regra é tratada como bloqueio no cálculo
            // (não como erro estrutural de entrada); regra presente mas nula é erro.
            if (fioBRules.TryGetValue(post, out var rule))
            {
                ArgumentNullException.ThrowIfNull(rule);

                // A chave do dicionário deve ser igual ao posto da própria regra;
                // chave divergente aplicaria Fio B/base no posto errado.
                if (rule.Post != post)
                    throw new ArgumentException(
                        $"A regra de compensação (Fio B) tem posto {rule.Post}, mas está associada à chave {post}.",
                        nameof(fioBRules));

                rules[post] = rule;
            }
        }

        ConnectionPhase? phase = null;
        if (profile.Group == TariffGroup.B)
        {
            if (connectionPhase is null)
                throw new ArgumentException("Grupo B exige a fase de ligação.", nameof(connectionPhase));
            phase = connectionPhase;
        }

        IReadOnlyList<decimal>? contracted = null;
        IReadOnlyList<decimal>? measured = null;
        if (profile.Group == TariffGroup.A)
        {
            contracted = RequireMonthlySeries(contractedDemandKw, nameof(contractedDemandKw));
            measured = RequireMonthlySeries(measuredDemandKw, nameof(measuredDemandKw));
        }

        return new TariffBillInput(
            profile,
            rules,
            consumption,
            injection,
            phase,
            contracted,
            measured,
            creditExpiryMonths,
            referenceYear,
            referenceDate);
    }

    private static IReadOnlyList<decimal> RequireMonthlySeries(IReadOnlyList<decimal>? values, string name)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count != 12)
            throw new ArgumentException($"{name} deve conter exatamente 12 valores (um por mês).", name);

        var copy = new decimal[12];
        for (var i = 0; i < 12; i++)
        {
            if (values[i] < 0)
                throw new ArgumentOutOfRangeException(name, $"{name}[{i}] não pode ser negativo.");
            copy[i] = values[i];
        }

        return Array.AsReadOnly(copy);
    }
}
