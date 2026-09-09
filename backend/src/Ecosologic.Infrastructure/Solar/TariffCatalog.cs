using System.Data;
using Ecosologic.Application.Solar;
using Ecosologic.Domain.Solar;
using Ecosologic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Infrastructure.Solar;

public enum TariffLookupStatus
{
    Found,
    Incomplete,
    NotFound
}

public sealed record TariffLookupResult(TariffLookupStatus Status, TariffProfileRecord? Profile)
{
    public static TariffLookupResult Complete(TariffProfileRecord profile) => new(TariffLookupStatus.Found, profile);

    public static TariffLookupResult Incomplete(TariffProfileRecord profile) => new(TariffLookupStatus.Incomplete, profile);

    public static TariffLookupResult Missing() => new(TariffLookupStatus.NotFound, null);
}

public sealed record TariffProfileFilter(
    Distributor? Distributor = null,
    TariffGroup? Group = null,
    TariffSubgroup? Subgroup = null,
    TariffModality? Modality = null,
    TariffPost? Post = null,
    DateOnly? AsOfDate = null,
    bool? IsComplete = null);

public sealed record GridCompensationRuleFilter(
    Distributor? Distributor = null,
    TariffGroup? Group = null,
    TariffSubgroup? Subgroup = null,
    TariffModality? Modality = null,
    TariffPost? Post = null,
    int? ReferenceYear = null,
    DateOnly? AsOfDate = null,
    bool? IsComplete = null);

public sealed record GridCompensationRuleLookupResult(TariffLookupStatus Status, GridCompensationRuleRecord? Rule)
{
    public static GridCompensationRuleLookupResult Complete(GridCompensationRuleRecord rule) => new(TariffLookupStatus.Found, rule);

    public static GridCompensationRuleLookupResult Incomplete(GridCompensationRuleRecord rule) => new(TariffLookupStatus.Incomplete, rule);

    public static GridCompensationRuleLookupResult Missing() => new(TariffLookupStatus.NotFound, null);
}

public sealed record TariffReconcileResult(int InsertedProfiles, int ClosedProfiles, int UnchangedProfiles);

public sealed class TariffCatalog(EcosologicDbContext db)
{
    public async Task<TariffLookupResult> FindProfileAsync(
        Distributor distributor,
        TariffGroup group,
        TariffSubgroup subgroup,
        TariffModality modality,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var matches = await db.TariffProfiles.AsNoTracking()
            .Include(profile => profile.Components)
            .Where(profile =>
                profile.IsCurrent
                && profile.Distributor == distributor
                && profile.Group == group
                && profile.Subgroup == subgroup
                && profile.Modality == modality
                && profile.ValidityStart <= date
                && (profile.ValidityEnd == null || profile.ValidityEnd >= date))
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
            return TariffLookupResult.Missing();

        if (matches.Count > 1)
            throw new InvalidOperationException(
                "Múltiplos perfis tarifários ativos para a combinação informada na data consultada.");

        var profile = matches[0];

        return profile.IsComplete
            ? TariffLookupResult.Complete(profile)
            : TariffLookupResult.Incomplete(profile);
    }

    public async Task<TariffProfileRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.TariffProfiles.AsNoTracking()
            .Include(profile => profile.Components)
            .SingleOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TariffProfileRecord>> ListAsync(
        TariffProfileFilter filter,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TariffProfileRecord> query = db.TariffProfiles.AsNoTracking().Include(profile => profile.Components);

        if (filter.Distributor.HasValue)
            query = query.Where(profile => profile.Distributor == filter.Distributor.Value);

        if (filter.Group.HasValue)
            query = query.Where(profile => profile.Group == filter.Group.Value);

        if (filter.Subgroup.HasValue)
            query = query.Where(profile => profile.Subgroup == filter.Subgroup.Value);

        if (filter.Modality.HasValue)
            query = query.Where(profile => profile.Modality == filter.Modality.Value);

        if (filter.Post.HasValue)
            query = query.Where(profile => profile.Components.Any(component => component.Post == filter.Post.Value));

        if (filter.AsOfDate.HasValue)
        {
            var date = filter.AsOfDate.Value;
            query = query.Where(profile =>
                profile.ValidityStart <= date
                && (profile.ValidityEnd == null || profile.ValidityEnd >= date));
        }

        if (filter.IsComplete.HasValue)
            query = query.Where(profile => profile.IsComplete == filter.IsComplete.Value);

        query = query.Where(profile => profile.IsCurrent);

        return await query
            .OrderByDescending(profile => profile.ValidityStart)
            .ThenBy(profile => profile.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddProfileAsync(TariffProfileRecord profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // A vigência (não sobreposição de perfis) NÃO possui uma constraint portável
        // entre PostgreSQL e SQLite (a exclusão por intervalo exigiria a extensão
        // btree_gist no PostgreSQL, que quebraria os testes em SQLite). A aplicação
        // é a autoridade: a checagem de sobreposição roda na mesma transação que o
        // INSERT, sob isolamento Serializable, de modo que duas inclusões concorrentes
        // não consigam ambas persistir intervalos sobrepostos. Em PostgreSQL, uma
        // falha de serialização (40001) deve ser reapresentada ao chamador para retry;
        // em SQLite o write-lock já serializa o commit.
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var overlaps = await db.TariffProfiles.AsNoTracking().AnyAsync(existing =>
                existing.Distributor == profile.Distributor
                && existing.Group == profile.Group
                && existing.Subgroup == profile.Subgroup
                && existing.Modality == profile.Modality
                && existing.ValidityStart <= (profile.ValidityEnd ?? DateOnly.MaxValue)
                && (existing.ValidityEnd ?? DateOnly.MaxValue) >= profile.ValidityStart,
                cancellationToken);

            if (overlaps)
                throw new InvalidOperationException(
                    "Já existe perfil tarifário com vigência sobreposta para esta combinação.");

            db.TariffProfiles.Add(profile);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // A transação já pode ter sido desfeita pelo provider.
            }

            throw;
        }
    }

    /// <summary>
    /// Reconcilia o lote de perfis normalizados vindos da ANEEL de forma atômica:
    /// distribuidoras são processadas juntas, em uma única transação Serializable, e
    /// uma falha em qualquer distribuidora desfaz o lote inteiro preservando os
    /// registros já vigentes. Um perfil idêntico (mesma identidade, vigência,
    /// resolução, hash e componentes) é um no-op; uma alteração com início de vigência
    /// posterior encerra a versão anterior na véspera do novo início e insere a nova
    /// versão; uma alteração de mesmo período (retificação) substitui a versão vigente
    /// preservando a anterior como inativa. Versões antigas são preservadas (nunca
    /// removidas). Sobreposições não resolvíveis são rejeitadas antes do commit.
    /// Este método abre e comita a própria transação quando chamado isoladamente.
    /// </summary>
    public async Task<TariffReconcileResult> ReconcileImportedProfilesAsync(
        IReadOnlyList<AneelNormalizedProfile> profiles,
        CancellationToken cancellationToken = default)
    {
        RequireProfiles(profiles);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var result = await ReconcileCoreAsync(profiles, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // A transação já pode ter sido desfeita pelo provider.
            }

            db.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>
    /// Variante da reconciliação que usa a transação ambiente já iniciada pelo
    /// chamador (<see cref="AneelTariffImportService"/>) sem abrir nem comitar uma
    /// transação aninhada. O chamador é responsável pelo commit/rollback.
    /// </summary>
    internal async Task<TariffReconcileResult> ReconcileImportedProfilesInTransactionAsync(
        IReadOnlyList<AneelNormalizedProfile> profiles,
        CancellationToken cancellationToken = default)
    {
        RequireProfiles(profiles);
        return await ReconcileCoreAsync(profiles, cancellationToken);
    }

    private static void RequireProfiles(IReadOnlyList<AneelNormalizedProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        if (profiles.Count == 0)
            throw new ArgumentException("A reconciliação exige ao menos um perfil importado.", nameof(profiles));
    }

    private async Task<TariffReconcileResult> ReconcileCoreAsync(
        IReadOnlyList<AneelNormalizedProfile> profiles,
        CancellationToken cancellationToken)
    {
        var inserted = 0;
        var closed = 0;
        var unchanged = 0;

        foreach (var group in profiles.GroupBy(profile =>
                     (profile.Distributor, profile.Group, profile.Subgroup, profile.Modality)))
        {
            var identity = group.Key;
            var incoming = group
                .OrderBy(profile => profile.ValidityStart)
                .ThenBy(profile => profile.ValidityEnd ?? DateOnly.MaxValue)
                .ToList();

            var existing = await db.TariffProfiles
                .Include(profile => profile.Components)
                .Where(profile =>
                    profile.Distributor == identity.Distributor
                    && profile.Group == identity.Group
                    && profile.Subgroup == identity.Subgroup
                    && profile.Modality == identity.Modality)
                .ToListAsync(cancellationToken);

            var current = existing.Where(profile => profile.IsCurrent).ToList();

            foreach (var profile in incoming)
            {
                var equivalent = current.FirstOrDefault(candidate => IsEquivalent(candidate, profile));
                if (equivalent is not null)
                {
                    unchanged++;
                    continue;
                }

                foreach (var candidate in current.Where(candidate => Overlaps(candidate, profile)).ToList())
                {
                    if (SamePeriod(candidate, profile))
                    {
                        candidate.Supersede();
                        current.Remove(candidate);
                        closed++;
                    }
                    else if (candidate.ValidityStart < profile.ValidityStart)
                    {
                        var previousEnd = candidate.ValidityEnd;
                        candidate.CloseAt(profile.ValidityStart.AddDays(-1));
                        if (previousEnd != candidate.ValidityEnd)
                            closed++;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "Sobreposição de vigência não resolvível na reconciliação ANEEL: a versão substituta inicia na data ou antes da versão vigente.");
                    }
                }

                var version = existing
                    .Where(record => record.ValidityStart == profile.ValidityStart)
                    .Select(record => record.Version)
                    .DefaultIfEmpty(0)
                    .Max() + 1;

                var record = ToRecord(profile, version, isCurrent: true);
                db.TariffProfiles.Add(record);
                existing.Add(record);
                current.Add(record);
                inserted++;
            }

            RejectOverlaps(current);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new TariffReconcileResult(inserted, closed, unchanged);
    }

    private static bool IsEquivalent(TariffProfileRecord existing, AneelNormalizedProfile incoming)
    {
        if (existing.ValidityStart != incoming.ValidityStart)
            return false;

        if (existing.ValidityEnd != incoming.ValidityEnd)
            return false;

        if (existing.ResolutionCode != incoming.ResolutionCode)
            return false;

        if (existing.SourceDocumentHash != incoming.SourceDocumentHash)
            return false;

        return ComponentsEquivalent(existing.Components, incoming.Components);
    }

    private static bool ComponentsEquivalent(
        IReadOnlyCollection<TariffComponentRecord> existing,
        IReadOnlyList<AneelNormalizedComponent> incoming)
    {
        if (existing.Count != incoming.Count)
            return false;

        var a = existing
            .OrderBy(component => component.Kind)
            .ThenBy(component => component.Unit)
            .ThenBy(component => component.Post)
            .Select(component => (component.Kind, component.Unit, component.Post, component.Value, component.TaxIncluded, component.SourcePage));

        var b = incoming
            .OrderBy(component => component.Kind)
            .ThenBy(component => component.Unit)
            .ThenBy(component => component.Post)
            .Select(component => (component.Kind, component.Unit, component.Post, component.Value, component.TaxIncluded, component.SourcePage));

        return a.SequenceEqual(b);
    }

    private static bool Overlaps(TariffProfileRecord existing, AneelNormalizedProfile incoming) =>
        existing.ValidityStart <= (incoming.ValidityEnd ?? DateOnly.MaxValue)
        && (existing.ValidityEnd ?? DateOnly.MaxValue) >= incoming.ValidityStart;

    private static bool SamePeriod(TariffProfileRecord existing, AneelNormalizedProfile incoming) =>
        existing.ValidityStart == incoming.ValidityStart
        && existing.ValidityEnd == incoming.ValidityEnd;

    private static void RejectOverlaps(IReadOnlyList<TariffProfileRecord> profiles)
    {
        var sorted = profiles.OrderBy(profile => profile.ValidityStart).ToList();
        for (var index = 0; index < sorted.Count - 1; index++)
        {
            var currentEnd = sorted[index].ValidityEnd ?? DateOnly.MaxValue;
            if (sorted[index + 1].ValidityStart <= currentEnd)
            {
                throw new InvalidOperationException(
                    "Sobreposição de vigência remanescente após a reconciliação ANEEL.");
            }
        }
    }

    private static TariffProfileRecord ToRecord(AneelNormalizedProfile profile, int version, bool isCurrent)
    {
        var components = profile.Components
            .Select(component => TariffComponent.Create(
                component.Kind,
                component.Unit,
                component.Post,
                component.Value,
                component.TaxIncluded,
                component.SourcePage))
            .ToList();

        return TariffProfileRecord.Create(
            Guid.NewGuid(),
            profile.Distributor,
            profile.Group,
            profile.Subgroup,
            profile.Modality,
            profile.ValidityStart,
            profile.ValidityEnd,
            profile.ResolutionCode,
            profile.SourceUrl,
            profile.SourceDocumentHash,
            profile.AccessedAt,
            profile.IsComplete,
            components,
            version,
            isCurrent);
    }

    public async Task<IReadOnlyList<GridCompensationRuleRecord>> ListGridCompensationRulesAsync(
        GridCompensationRuleFilter filter,
        CancellationToken cancellationToken = default)
    {
        IQueryable<GridCompensationRuleRecord> query = db.GridCompensationRules.AsNoTracking();

        if (filter.Distributor.HasValue)
            query = query.Where(rule => rule.Distributor == filter.Distributor.Value);

        if (filter.Group.HasValue)
            query = query.Where(rule => rule.Group == filter.Group.Value);

        if (filter.Subgroup.HasValue)
            query = query.Where(rule => rule.Subgroup == filter.Subgroup.Value);

        if (filter.Modality.HasValue)
            query = query.Where(rule => rule.Modality == filter.Modality.Value);

        if (filter.Post.HasValue)
            query = query.Where(rule => rule.Post == filter.Post.Value);

        if (filter.ReferenceYear.HasValue)
            query = query.Where(rule => rule.ReferenceYear == filter.ReferenceYear.Value);

        if (filter.AsOfDate.HasValue)
        {
            var date = filter.AsOfDate.Value;
            query = query.Where(rule =>
                rule.ValidityStart <= date
                && (rule.ValidityEnd == null || rule.ValidityEnd >= date));
        }

        if (filter.IsComplete.HasValue)
            query = query.Where(rule => rule.IsComplete == filter.IsComplete.Value);

        return await query
            .OrderByDescending(rule => rule.ValidityStart)
            .ThenBy(rule => rule.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<GridCompensationRuleLookupResult> FindGridCompensationRuleAsync(
        Distributor distributor,
        TariffGroup group,
        TariffSubgroup subgroup,
        TariffModality modality,
        TariffPost post,
        int referenceYear,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var matches = await db.GridCompensationRules.AsNoTracking()
            .Where(rule =>
                rule.Distributor == distributor
                && rule.Group == group
                && rule.Subgroup == subgroup
                && rule.Modality == modality
                && rule.Post == post
                && rule.ReferenceYear == referenceYear
                && rule.ValidityStart <= date
                && (rule.ValidityEnd == null || rule.ValidityEnd >= date))
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
            return GridCompensationRuleLookupResult.Missing();

        if (matches.Count > 1)
            throw new InvalidOperationException(
                "Múltiplas regras de compensação ativas para a combinação informada na data consultada.");

        var rule = matches[0];

        return rule.IsComplete
            ? GridCompensationRuleLookupResult.Complete(rule)
            : GridCompensationRuleLookupResult.Incomplete(rule);
    }

    public async Task AddGridCompensationRuleAsync(
        GridCompensationRuleRecord rule,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        // Mesma garantia do AddProfileAsync: a não sobreposição de vigência não tem
        // constraint portável entre PostgreSQL e SQLite. A aplicação é a autoridade e
        // roda a checagem na mesma transação do INSERT sob isolamento Serializable.
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var overlaps = await db.GridCompensationRules.AsNoTracking().AnyAsync(existing =>
                existing.Distributor == rule.Distributor
                && existing.Group == rule.Group
                && existing.Subgroup == rule.Subgroup
                && existing.Modality == rule.Modality
                && existing.Post == rule.Post
                && existing.ReferenceYear == rule.ReferenceYear
                && existing.ValidityStart <= (rule.ValidityEnd ?? DateOnly.MaxValue)
                && (existing.ValidityEnd ?? DateOnly.MaxValue) >= rule.ValidityStart,
                cancellationToken);

            if (overlaps)
                throw new InvalidOperationException(
                    "Já existe regra de compensação com vigência sobreposta para esta combinação e ano de referência.");

            db.GridCompensationRules.Add(rule);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // A transação já pode ter sido desfeita pelo provider.
            }

            throw;
        }
    }
}
