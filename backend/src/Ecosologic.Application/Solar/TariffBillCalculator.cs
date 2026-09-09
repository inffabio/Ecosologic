using Ecosologic.Domain.Solar;

namespace Ecosologic.Application.Solar;

/// <summary>
/// Motor determinístico do cálculo de fatura (com e sem solar) para os grupos A e B.
///
/// Fórmulas exatas (ver spec da Task 4):
/// - Compensação no mês, por posto: compensado = min(injeção, consumo);
///   excedente = injeção - consumo vira crédito; déficit = consumo - injeção é
///   abatido por créditos (FIFO, com expiração configurável e posto cruzado).
/// - Grupo B: piso de disponibilidade 30/50/100 kWh (mono/bi/tri). Disponibilidade
///   = max(0, piso - energiaDaRede) × TE; TE/TUSD/tributos incidem sobre a energia
///   efetivamente consumida da rede; Fio B incide sobre a energia compensada.
/// - Grupo A: demanda = max(contratada, medida) × DEMAND; ultrapassagem =
///   max(0, medida - contratada) × OVERAGE; consumo ponta/fora ponta por posto.
/// - Fio B = ProgressivePercent × BaseComponente × EnergiaCompensada (via
///   <see cref="FioBCalculator"/>), nunca uma tarifa universal.
/// - Arredondamento: centavos apenas no total mensal e na fatura anual
///   (<see cref="Round"/>); linhas intermediárias mantêm precisão decimal.
///
/// Ausência/incompleteza de perfil, regra ou componente retorna resultado
/// bloqueado com erros explícitos — nunca preenche zero silenciosamente.
/// </summary>
public sealed class TariffBillCalculator
{
    public const string EngineVersion = "1.0.0";
    public const int DefaultCreditExpiryMonths = 60;

    private readonly FioBCalculator _fioB = new();

    public TariffBillResult Calculate(TariffBillInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var profile = input.Profile;
        var posts = TariffBillPosts.For(profile.Modality);

        // 1. Resolver componentes/regras exigidos; qualquer ausência/incompleteza bloqueia.
        var errors = new List<BillError>();
        if (!profile.IsComplete)
            errors.Add(new("ProfileIncomplete", "Perfil tarifário incompleto; não é possível calcular a fatura."));

        var teRates = new Dictionary<TariffPost, decimal>();
        var tusdRates = new Dictionary<TariffPost, decimal>();
        var taxRates = new Dictionary<TariffPost, decimal>();
        var fioBRules = new Dictionary<TariffPost, GridCompensationRule>();
        var fioBBase = new Dictionary<TariffPost, TariffComponent>();
        decimal taxFixedMonthly = 0m;

        foreach (var post in posts)
        {
            var te = Find(profile, TariffComponentKind.TE, TariffUnit.KWh, post);
            if (te is null)
                errors.Add(new("ComponentMissing", $"Componente TE (R$/kWh) ausente para o posto {post}."));
            else
                teRates[post] = te.Value;

            if (!TryTusdRate(profile, post, out var tusdRate))
                errors.Add(new("ComponentMissing", $"Componente TUSD (R$/kWh) ausente para o posto {post}."));
            else
                tusdRates[post] = tusdRate;

            taxRates[post] = Find(profile, TariffComponentKind.TAX, TariffUnit.KWh, post)?.Value ?? 0m;

            if (!input.FioBRules.TryGetValue(post, out var rule))
            {
                errors.Add(new("RuleMissing", $"Regra de compensação (Fio B) ausente para o posto {post}."));
            }
            else if (!rule.IsComplete)
            {
                errors.Add(new("RuleIncomplete", $"Regra de compensação (Fio B) incompleta para o posto {post}."));
            }
            else if (ValidateRuleAgainstProfile(profile, input, rule, post) is { } mismatch)
            {
                errors.Add(mismatch);
            }
            else
            {
                var baseComponent = Find(profile, rule.BaseComponent, TariffUnit.KWh, rule.Post);
                if (baseComponent is null)
                {
                    errors.Add(new("ComponentMissing", $"Base tarifária do Fio B ({rule.BaseComponent}) ausente para o posto {post}."));
                }
                else
                {
                    fioBRules[post] = rule;
                    fioBBase[post] = baseComponent;
                }
            }
        }

        foreach (var component in profile.Components)
            if (component.Kind == TariffComponentKind.TAX && component.Unit == TariffUnit.Month)
                taxFixedMonthly += component.Value;

        TariffComponent? demandComponent = null;
        TariffComponent? overageComponent = null;
        if (profile.Group == TariffGroup.A)
        {
            demandComponent = FindDemand(profile, TariffComponentKind.DEMAND);
            overageComponent = FindDemand(profile, TariffComponentKind.OVERAGE);
            if (demandComponent is null)
                errors.Add(new("ComponentMissing", "Componente DEMAND (R$/kW) ausente para o Grupo A."));
            if (overageComponent is null)
                errors.Add(new("ComponentMissing", "Componente OVERAGE (R$/kW) ausente para o Grupo A."));
        }

        if (errors.Count > 0)
            return TariffBillResult.Blocked(errors);

        // 2. Cálculo mensal.
        var months = new List<TariffBillMonth>(12);
        var ledger = new List<Credit>();
        decimal annualWithout = 0m;
        decimal annualWith = 0m;

        for (var m = 0; m < 12; m++)
        {
            RemoveExpired(ledger, m + 1);

            var withSolar = ComputeMonth(
                input, posts, m, teRates, tusdRates, taxRates, taxFixedMonthly,
                fioBRules, fioBBase, demandComponent, overageComponent, ledger);

            var totalWithout = ComputeTotalWithoutSolar(
                input, posts, m, teRates, tusdRates, taxRates, taxFixedMonthly,
                demandComponent, overageComponent);

            var month = withSolar with
            {
                TotalWithoutSolar = totalWithout,
                Savings = totalWithout - withSolar.TotalWithSolar
            };

            months.Add(month);
            annualWith += month.TotalWithSolar;
            annualWithout += month.TotalWithoutSolar;
        }

        var remainingCredits = ledger
            .Where(c => c.RemainingKWh > 0m)
            .Select(c => new CreditEntry(c.GeneratedMonth, c.Post, c.RemainingKWh, c.ExpiresAfterMonth))
            .ToList();

        var summary = new TariffBillSummary(
            Round(annualWithout),
            Round(annualWith),
            Round(annualWithout - annualWith),
            remainingCredits.Sum(c => c.RemainingKWh));

        return TariffBillResult.Calculated(months, summary, remainingCredits);
    }

    private TariffBillMonth ComputeMonth(
        TariffBillInput input,
        IReadOnlyList<TariffPost> posts,
        int monthIndex,
        IReadOnlyDictionary<TariffPost, decimal> teRates,
        IReadOnlyDictionary<TariffPost, decimal> tusdRates,
        IReadOnlyDictionary<TariffPost, decimal> taxRates,
        decimal taxFixedMonthly,
        IReadOnlyDictionary<TariffPost, GridCompensationRule> fioBRules,
        IReadOnlyDictionary<TariffPost, TariffComponent> fioBBase,
        TariffComponent? demandComponent,
        TariffComponent? overageComponent,
        List<Credit> ledger)
    {
        var profile = input.Profile;

        // Passo 1: compensação dentro do mês e contabilização de excedente/déficit por posto.
        var withinMonth = new Dictionary<TariffPost, decimal>();
        var deficits = new List<(TariffPost Post, decimal KWh)>();

        foreach (var post in posts)
        {
            var consumption = input.ConsumptionByPost[post][monthIndex];
            var injection = input.InjectionByPost[post][monthIndex];

            var within = Math.Min(injection, consumption);
            withinMonth[post] = within;

            if (injection > consumption)
            {
                ledger.Add(new Credit
                {
                    GeneratedMonth = monthIndex + 1,
                    ExpiresAfterMonth = monthIndex + 1 + input.CreditExpiryMonths,
                    Post = post,
                    RemainingKWh = injection - consumption
                });
            }
            else if (consumption > injection)
            {
                deficits.Add((post, consumption - injection));
            }
        }

        // Passo 2: aplicar créditos (FIFO, incluindo excedente do próprio mês — posto cruzado).
        var creditUsedByPost = new Dictionary<TariffPost, decimal>();
        foreach (var post in posts)
            creditUsedByPost[post] = 0m;

        foreach (var (post, deficit) in deficits)
            creditUsedByPost[post] = ConsumeCredits(ledger, deficit);

        // Passo 3: custos por posto e totais.
        decimal energyFromGridMonth = 0m;
        decimal compensatedMonth = 0m;
        decimal teMonth = 0m;
        decimal tusdMonth = 0m;
        decimal taxMonth = 0m;
        decimal fioBMonth = 0m;
        var lines = new List<PostBillLine>(posts.Count);

        foreach (var post in posts)
        {
            var consumption = input.ConsumptionByPost[post][monthIndex];
            var within = withinMonth[post];
            var viaCredit = creditUsedByPost[post];
            var compensated = within + viaCredit;
            var energyFromGrid = consumption - within - viaCredit;

            var te = energyFromGrid * teRates[post];
            var tusd = energyFromGrid * tusdRates[post];
            var tax = energyFromGrid * taxRates[post];
            var fioB = _fioB.Calculate(fioBRules[post], fioBBase[post], compensated).Cost;

            lines.Add(new PostBillLine(
                post,
                consumption,
                input.InjectionByPost[post][monthIndex],
                within,
                viaCredit,
                energyFromGrid,
                te,
                tusd,
                tax,
                fioB));

            energyFromGridMonth += energyFromGrid;
            compensatedMonth += compensated;
            teMonth += te;
            tusdMonth += tusd;
            taxMonth += tax;
            fioBMonth += fioB;
        }

        taxMonth += taxFixedMonthly;

        // Disponibilidade (Grupo B).
        decimal availabilityKWh = 0m;
        decimal availabilityCost = 0m;
        if (profile.Group == TariffGroup.B)
        {
            var minimumKWh = TariffAvailability.MinimumMonthlyKWh(input.ConnectionPhase!.Value);
            var billable = Math.Max(energyFromGridMonth, minimumKWh);
            availabilityKWh = billable - energyFromGridMonth;
            availabilityCost = availabilityKWh * AvailabilityTeRate(teRates);
        }

        // Demanda/ultrapassagem (Grupo A).
        decimal demandCost = 0m;
        decimal overageCost = 0m;
        if (profile.Group == TariffGroup.A)
        {
            var contracted = input.ContractedDemandKw![monthIndex];
            var measured = input.MeasuredDemandKw![monthIndex];
            var demandBillable = Math.Max(contracted, measured);
            var overageKw = Math.Max(0m, measured - contracted);
            demandCost = demandBillable * demandComponent!.Value;
            overageCost = overageKw * overageComponent!.Value;
        }

        var total = Round(teMonth + tusdMonth + taxMonth + fioBMonth + availabilityCost + demandCost + overageCost);

        return new TariffBillMonth(
            monthIndex + 1,
            lines.AsReadOnly(),
            energyFromGridMonth,
            compensatedMonth,
            availabilityKWh,
            availabilityCost,
            teMonth,
            tusdMonth,
            taxMonth,
            fioBMonth,
            demandCost,
            overageCost,
            total,
            total,
            total);
    }

    private decimal ComputeTotalWithoutSolar(
        TariffBillInput input,
        IReadOnlyList<TariffPost> posts,
        int monthIndex,
        IReadOnlyDictionary<TariffPost, decimal> teRates,
        IReadOnlyDictionary<TariffPost, decimal> tusdRates,
        IReadOnlyDictionary<TariffPost, decimal> taxRates,
        decimal taxFixedMonthly,
        TariffComponent? demandComponent,
        TariffComponent? overageComponent)
    {
        var profile = input.Profile;

        decimal te = 0m;
        decimal tusd = 0m;
        decimal tax = 0m;
        decimal energyFromGrid = 0m;

        foreach (var post in posts)
        {
            var consumption = input.ConsumptionByPost[post][monthIndex];
            energyFromGrid += consumption;
            te += consumption * teRates[post];
            tusd += consumption * tusdRates[post];
            tax += consumption * taxRates[post];
        }

        tax += taxFixedMonthly;

        decimal availability = 0m;
        if (profile.Group == TariffGroup.B)
        {
            var minimumKWh = TariffAvailability.MinimumMonthlyKWh(input.ConnectionPhase!.Value);
            var billable = Math.Max(energyFromGrid, minimumKWh);
            availability = (billable - energyFromGrid) * AvailabilityTeRate(teRates);
        }

        decimal demand = 0m;
        decimal overage = 0m;
        if (profile.Group == TariffGroup.A)
        {
            var contracted = input.ContractedDemandKw![monthIndex];
            var measured = input.MeasuredDemandKw![monthIndex];
            demand = Math.Max(contracted, measured) * demandComponent!.Value;
            overage = Math.Max(0m, measured - contracted) * overageComponent!.Value;
        }

        return Round(te + tusd + tax + availability + demand + overage);
    }

    // --- Helpers ---

    private static BillError? ValidateRuleAgainstProfile(
        TariffProfile profile,
        TariffBillInput input,
        GridCompensationRule rule,
        TariffPost post)
    {
        if (rule.Distributor != profile.Distributor)
            return new("RuleDistributorMismatch",
                $"Regra de compensação (Fio B) do posto {post} é da concessionária {rule.Distributor}, divergente do perfil ({profile.Distributor}).");

        if (rule.Group != profile.Group)
            return new("RuleGroupMismatch",
                $"Regra de compensação (Fio B) do posto {post} é do grupo {rule.Group}, divergente do perfil ({profile.Group}).");

        if (rule.Subgroup != profile.Subgroup)
            return new("RuleSubgroupMismatch",
                $"Regra de compensação (Fio B) do posto {post} é do subgrupo {rule.Subgroup}, divergente do perfil ({profile.Subgroup}).");

        if (rule.Modality != profile.Modality)
            return new("RuleModalityMismatch",
                $"Regra de compensação (Fio B) do posto {post} é da modalidade {rule.Modality}, divergente do perfil ({profile.Modality}).");

        if (input.ReferenceYear is { } year && rule.ReferenceYear != year)
            return new("RuleReferenceYearMismatch",
                $"Regra de compensação (Fio B) do posto {post} é do ano de referência {rule.ReferenceYear}, divergente do ano solicitado ({year}).");

        if (input.ReferenceDate is { } date &&
            (rule.ValidityStart > date || (rule.ValidityEnd is { } end && end < date)))
            return new("RuleNotInForce",
                $"Regra de compensação (Fio B) do posto {post} não está vigente na data de referência {date:yyyy-MM-dd}.");

        return null;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static TariffComponent? Find(TariffProfile profile, TariffComponentKind kind, TariffUnit unit, TariffPost post) =>
        profile.Components.FirstOrDefault(c => c.Kind == kind && c.Unit == unit && c.Post == post);

    private static bool TryTusdRate(TariffProfile profile, TariffPost post, out decimal rate)
    {
        var combined = Find(profile, TariffComponentKind.TUSD, TariffUnit.KWh, post);
        if (combined is not null)
        {
            rate = combined.Value;
            return true;
        }

        var distribution = Find(profile, TariffComponentKind.TUSD_DISTRIBUTION, TariffUnit.KWh, post);
        var transmission = Find(profile, TariffComponentKind.TUSD_TRANSMISSION, TariffUnit.KWh, post);

        if (distribution is null && transmission is null)
        {
            rate = 0m;
            return false;
        }

        rate = (distribution?.Value ?? 0m) + (transmission?.Value ?? 0m);
        return true;
    }

    private static TariffComponent? FindDemand(TariffProfile profile, TariffComponentKind kind) =>
        Find(profile, kind, TariffUnit.KW, TariffPost.Peak)
        ?? Find(profile, kind, TariffUnit.KW, TariffPost.OffPeak)
        ?? Find(profile, kind, TariffUnit.KW, TariffPost.Single);

    // Disponibilidade usa a TE fora ponta quando disponível (tarifa branca) ou a TE única.
    private static decimal AvailabilityTeRate(IReadOnlyDictionary<TariffPost, decimal> teRates)
    {
        if (teRates.TryGetValue(TariffPost.OffPeak, out var offPeak))
            return offPeak;
        if (teRates.TryGetValue(TariffPost.Single, out var single))
            return single;
        return teRates.Values.First();
    }

    private static void RemoveExpired(List<Credit> ledger, int month) =>
        ledger.RemoveAll(c => c.ExpiresAfterMonth < month);

    private static decimal ConsumeCredits(List<Credit> ledger, decimal need)
    {
        var used = 0m;
        foreach (var credit in ledger)
        {
            if (need <= 0m)
                break;
            if (credit.RemainingKWh <= 0m)
                continue;

            var take = Math.Min(need, credit.RemainingKWh);
            credit.RemainingKWh -= take;
            used += take;
            need -= take;
        }

        return used;
    }

    private sealed class Credit
    {
        public int GeneratedMonth;
        public int ExpiresAfterMonth;
        public TariffPost Post;
        public decimal RemainingKWh;
    }
}
