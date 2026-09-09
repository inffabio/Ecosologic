using Ecosologic.Domain.Crm;
using Ecosologic.Domain.Solar;
using Ecosologic.Infrastructure.Solar;
using Microsoft.EntityFrameworkCore;

namespace Ecosologic.Infrastructure.Persistence;

public sealed class EcosologicDbContext(DbContextOptions<EcosologicDbContext> options) : DbContext(options)
{
    public DbSet<LeadRecord> Leads => Set<LeadRecord>();
    public DbSet<LeadActivityRecord> LeadActivities => Set<LeadActivityRecord>();
    public DbSet<LeadTaskRecord> LeadTasks => Set<LeadTaskRecord>();
    public DbSet<CrmNotificationRecord> CrmNotifications => Set<CrmNotificationRecord>();
    public DbSet<HomeContentRecord> HomeContent => Set<HomeContentRecord>();
    public DbSet<SolarSizingRecord> SolarSizings => Set<SolarSizingRecord>();
    public DbSet<SolarQuoteRecord> SolarQuotes => Set<SolarQuoteRecord>();
    public DbSet<ProposalRecord> Proposals => Set<ProposalRecord>();
    public DbSet<TariffProfileRecord> TariffProfiles => Set<TariffProfileRecord>();
    public DbSet<TariffComponentRecord> TariffComponents => Set<TariffComponentRecord>();
    public DbSet<GridCompensationRuleRecord> GridCompensationRules => Set<GridCompensationRuleRecord>();
    public DbSet<AneelTariffImportRecord> AneelTariffImports => Set<AneelTariffImportRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadRecord>(entity =>
        {
            entity.ToTable("leads");
            entity.HasKey(lead => lead.Id);
            entity.Property(lead => lead.Name).HasMaxLength(160).IsRequired();
            entity.Property(lead => lead.Phone).HasMaxLength(30).IsRequired();
            entity.Property(lead => lead.Email).HasMaxLength(254);
            entity.Property(lead => lead.Message).HasMaxLength(4000);
            entity.Property(lead => lead.Notes).HasMaxLength(4000);
            entity.Property(lead => lead.Stage).HasConversion<string>().HasMaxLength(32);
            entity.Property(lead => lead.WonAt);
            entity.Property(lead => lead.ReminderAt);
            entity.HasIndex(lead => lead.CreatedAt);
            entity.HasMany(lead => lead.Activities)
                .WithOne(activity => activity.Lead)
                .HasForeignKey(activity => activity.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(lead => lead.Tasks)
                .WithOne(task => task.Lead)
                .HasForeignKey(task => task.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeadActivityRecord>(entity =>
        {
            entity.ToTable("lead_activities");
            entity.HasKey(activity => activity.Id);
            entity.Property(activity => activity.Type).HasMaxLength(32).IsRequired();
            entity.Property(activity => activity.Description).HasMaxLength(4000).IsRequired();
            entity.Property(activity => activity.CreatedAt).IsRequired();
            entity.HasIndex(activity => activity.LeadId);
            entity.HasIndex(activity => activity.CreatedAt);
        });

        modelBuilder.Entity<LeadTaskRecord>(entity =>
        {
            entity.ToTable("lead_tasks");
            entity.HasKey(task => task.Id);
            entity.Property(task => task.Title).HasMaxLength(160).IsRequired();
            entity.Property(task => task.Description).HasMaxLength(2000);
            entity.Property(task => task.DueAt).IsRequired();
            entity.Property(task => task.CompletedAt);
            entity.Property(task => task.CreatedAt).IsRequired();
            entity.Property(task => task.UpdatedAt).IsRequired();
            entity.HasIndex(task => task.LeadId);
            entity.HasIndex(task => task.DueAt);
        });

        modelBuilder.Entity<CrmNotificationRecord>(entity =>
        {
            entity.ToTable("crm_notifications");
            entity.HasKey(notification => notification.Id);
            entity.Property(notification => notification.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(notification => notification.Title).HasMaxLength(240).IsRequired();
            entity.Property(notification => notification.DueAt).IsRequired();
            entity.Property(notification => notification.DueDay).IsRequired();
            entity.Property(notification => notification.ReadAt);
            entity.Property(notification => notification.CreatedAt).IsRequired();
            entity.HasIndex(notification => notification.CreatedAt);
            entity.HasIndex(notification => notification.TaskId)
                .IsUnique()
                .HasFilter("\"TaskId\" IS NOT NULL");
            entity.HasIndex(notification => notification.LeadId)
                .IsUnique()
                .HasFilter("\"TaskId\" IS NULL");
            entity.HasIndex(notification => new { notification.ReadAt, notification.DueAt });
            entity.HasOne<LeadRecord>()
                .WithMany()
                .HasForeignKey(notification => notification.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<LeadTaskRecord>()
                .WithMany()
                .HasForeignKey(notification => notification.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HomeContentRecord>(entity =>
        {
            entity.ToTable("home_content");
            entity.HasKey(content => content.Id);
            entity.Property(content => content.HeroTitle).HasMaxLength(240).IsRequired();
            entity.Property(content => content.HeroText).HasMaxLength(1000).IsRequired();
            entity.Property(content => content.HeroImageUrl).HasMaxLength(500).IsRequired();
            entity.Property(content => content.ContactEmail).HasMaxLength(254).IsRequired();
            entity.Property(content => content.ContactPhone).HasMaxLength(40).IsRequired();
            entity.Property(content => content.SolutionsJson).HasColumnType("text").IsRequired();
            entity.Property(content => content.ProcessStepsJson).HasColumnType("text").IsRequired();
            entity.Property(content => content.ProjectsJson).HasColumnType("text").IsRequired();
        });

        modelBuilder.Entity<SolarSizingRecord>(entity =>
        {
            entity.ToTable("solar_sizings", table =>
            {
                table.HasCheckConstraint(
                    "CK_solar_sizings_status",
                    "\"Status\" IN ('Draft','Calculated','Approved','Cancelled')");
                table.HasCheckConstraint(
                    "CK_solar_sizings_results_for_calculated_approved",
                    "(\"Status\" NOT IN ('Calculated','Approved')) OR (\"ResultsJson\" IS NOT NULL AND \"ResultsJson\" <> '')");
                table.HasCheckConstraint("CK_solar_sizings_grupo_nonempty", "\"Grupo\" <> ''");
                table.HasCheckConstraint("CK_solar_sizings_modalidade_nonempty", "\"Modalidade\" <> ''");
            });
            entity.HasKey(sizing => sizing.Id);
            entity.Property(sizing => sizing.LeadId).IsRequired();
            entity.Property(sizing => sizing.Concessionaria).HasMaxLength(120).IsRequired();
            entity.Property(sizing => sizing.Grupo).HasMaxLength(16).IsRequired();
            entity.Property(sizing => sizing.Modalidade).HasMaxLength(64).IsRequired();
            entity.Property(sizing => sizing.EngineVersion).HasMaxLength(32).IsRequired();
            entity.Property(sizing => sizing.InputsJson).HasColumnType("text").IsRequired();
            entity.Property(sizing => sizing.ResultsJson).HasColumnType("text");
            entity.Property(sizing => sizing.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(sizing => sizing.CreatedAt).IsRequired();
            entity.Property(sizing => sizing.UpdatedAt).IsRequired();
            entity.HasIndex(sizing => sizing.LeadId);
            entity.HasIndex(sizing => sizing.CreatedAt);
            entity.HasOne<LeadRecord>()
                .WithMany()
                .HasForeignKey(sizing => sizing.LeadId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(sizing => sizing.Quotes)
                .WithOne(quote => quote.Sizing)
                .HasForeignKey(quote => quote.SizingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SolarQuoteRecord>(entity =>
        {
            entity.ToTable("solar_quotes", table =>
            {
                table.HasCheckConstraint(
                    "CK_solar_quotes_status",
                    "\"Status\" IN ('Draft','Approved','Sent','Accepted','Rejected','Expired')");
                table.HasCheckConstraint("CK_solar_quotes_total_cost_nonnegative", "\"TotalCost\" >= 0");
                table.HasCheckConstraint("CK_solar_quotes_total_price_nonnegative", "\"TotalPrice\" >= 0");
                table.HasCheckConstraint("CK_solar_quotes_margin_percent_nonnegative", "\"MarginPercent\" >= 0");
                table.HasCheckConstraint("CK_solar_quotes_tax_percent_nonnegative", "\"TaxPercent\" >= 0");
                table.HasCheckConstraint(
                    "CK_solar_quotes_approved_requires_snapshot",
                    "(\"Status\" <> 'Approved') OR (\"SnapshotJson\" IS NOT NULL AND \"SnapshotJson\" <> '')");
            });
            entity.HasKey(quote => quote.Id);
            entity.Property(quote => quote.SizingId).IsRequired();
            entity.Property(quote => quote.ItemsJson).HasColumnType("text").IsRequired();
            entity.Property(quote => quote.TotalCost).HasPrecision(18, 2).IsRequired();
            entity.Property(quote => quote.MarginPercent).HasPrecision(8, 4).IsRequired();
            entity.Property(quote => quote.TaxPercent).HasPrecision(8, 4).IsRequired();
            entity.Property(quote => quote.TotalPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(quote => quote.ConditionsJson).HasColumnType("text").IsRequired();
            entity.Property(quote => quote.ValidUntil).IsRequired();
            entity.Property(quote => quote.SnapshotJson).HasColumnType("text").IsRequired();
            entity.Property(quote => quote.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(quote => quote.CreatedAt).IsRequired();
            entity.Property(quote => quote.UpdatedAt).IsRequired();
            entity.HasIndex(quote => quote.SizingId);
            entity.HasIndex(quote => quote.CreatedAt);
            entity.HasMany(quote => quote.Proposals)
                .WithOne(proposal => proposal.Quote)
                .HasForeignKey(proposal => proposal.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProposalRecord>(entity =>
        {
            entity.ToTable("proposals", table =>
            {
                table.HasCheckConstraint(
                    "CK_proposals_status",
                    "\"Status\" IN ('Draft','Generated','Sent','Accepted','Rejected','Expired')");
                table.HasCheckConstraint(
                    "CK_proposals_generated_requires_file",
                    "(\"Status\" <> 'Generated') OR (\"FileUrl\" IS NOT NULL AND \"FileUrl\" <> '')");
            });
            entity.HasKey(proposal => proposal.Id);
            entity.Property(proposal => proposal.QuoteId).IsRequired();
            entity.Property(proposal => proposal.TemplateVersion).HasMaxLength(32).IsRequired();
            entity.Property(proposal => proposal.PayloadJson).HasColumnType("text").IsRequired();
            entity.Property(proposal => proposal.FileUrl).HasMaxLength(500);
            entity.Property(proposal => proposal.FileHash).HasMaxLength(128);
            entity.Property(proposal => proposal.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(proposal => proposal.CreatedAt).IsRequired();
            entity.Property(proposal => proposal.UpdatedAt).IsRequired();
            entity.HasIndex(proposal => proposal.QuoteId);
            entity.HasIndex(proposal => proposal.CreatedAt);
        });

        modelBuilder.Entity<TariffProfileRecord>(entity =>
        {
            entity.ToTable("tariff_profiles", table =>
            {
                table.HasCheckConstraint(
                    "CK_tariff_profiles_distributor",
                    "\"Distributor\" IN ('Light','EnelRio')");
                table.HasCheckConstraint(
                    "CK_tariff_profiles_group",
                    "\"Group\" IN ('A','B')");
                table.HasCheckConstraint(
                    "CK_tariff_profiles_subgroup",
                    "\"Subgroup\" IN ('A1','A2','A3','A3a','A4','AS','B1','B2','B3','B4')");
                table.HasCheckConstraint(
                    "CK_tariff_profiles_modality",
                    "\"Modality\" IN ('Conventional','White','Blue','Green')");
                table.HasCheckConstraint(
                    "CK_tariff_profiles_validity",
                    "\"ValidityEnd\" IS NULL OR \"ValidityEnd\" > \"ValidityStart\"");
                table.HasCheckConstraint(
                    "CK_tariff_profiles_resolution_nonempty",
                    "\"ResolutionCode\" <> ''");
            });
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.Distributor).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(profile => profile.Group).HasConversion<string>().HasMaxLength(8).IsRequired();
            entity.Property(profile => profile.Subgroup).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(profile => profile.Modality).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(profile => profile.ValidityStart).IsRequired();
            entity.Property(profile => profile.ValidityEnd);
            entity.Property(profile => profile.ResolutionCode).HasMaxLength(120).IsRequired();
            entity.Property(profile => profile.SourceUrl).HasMaxLength(500).IsRequired();
            entity.Property(profile => profile.SourceDocumentHash).HasMaxLength(128);
            entity.Property(profile => profile.AccessedAt).IsRequired();
            entity.Property(profile => profile.IsComplete).IsRequired();
            entity.Property(profile => profile.Version).IsRequired();
            entity.Property(profile => profile.IsCurrent).IsRequired();
            entity.HasIndex(profile => new
            {
                profile.Distributor,
                profile.Group,
                profile.Subgroup,
                profile.Modality,
                profile.ValidityStart,
                profile.Version
            }).IsUnique();
            entity.HasMany(profile => profile.Components)
                .WithOne(component => component.Profile)
                .HasForeignKey(component => component.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(profile => profile.Components).HasField("_components");
        });

        modelBuilder.Entity<TariffComponentRecord>(entity =>
        {
            entity.ToTable("tariff_components", table =>
            {
                table.HasCheckConstraint(
                    "CK_tariff_components_kind",
                    "\"Kind\" IN ('TE','TUSD','TUSD_DISTRIBUTION','TUSD_TRANSMISSION','FIO_B','TAX','DEMAND','OVERAGE','OTHER')");
                table.HasCheckConstraint(
                    "CK_tariff_components_unit",
                    "\"Unit\" IN ('KWh','KW','Month')");
                table.HasCheckConstraint(
                    "CK_tariff_components_post",
                    "\"Post\" IN ('Single','Peak','Intermediate','OffPeak')");
                table.HasCheckConstraint(
                    "CK_tariff_components_value_nonnegative",
                    "\"Value\" >= 0");
            });
            entity.HasKey(component => component.Id);
            entity.Property(component => component.ProfileId).IsRequired();
            entity.Property(component => component.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(component => component.Unit).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(component => component.Post).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(component => component.Value).HasPrecision(18, 8).IsRequired();
            entity.Property(component => component.TaxIncluded).IsRequired();
            entity.Property(component => component.SourcePage).HasMaxLength(50);
            entity.HasIndex(component => component.ProfileId);
            entity.HasIndex(component => new
            {
                component.ProfileId,
                component.Kind,
                component.Unit,
                component.Post
            }).IsUnique();
        });

        modelBuilder.Entity<GridCompensationRuleRecord>(entity =>
        {
            entity.ToTable("grid_compensation_rules", table =>
            {
                table.HasCheckConstraint(
                    "CK_grid_compensation_rules_distributor",
                    "\"Distributor\" IN ('Light','EnelRio')");
                table.HasCheckConstraint(
                    "CK_grid_compensation_rules_post",
                    "\"Post\" IN ('Single','Peak','Intermediate','OffPeak')");
                table.HasCheckConstraint(
                    "CK_grid_compensation_rules_base_component",
                    "\"BaseComponent\" IN ('TE','TUSD','TUSD_DISTRIBUTION','TUSD_TRANSMISSION','FIO_B','TAX','DEMAND','OVERAGE','OTHER')");
                table.HasCheckConstraint(
                    "CK_grid_compensation_rules_progressive_percent",
                    "CAST(\"ProgressivePercent\" AS REAL) >= 0 AND CAST(\"ProgressivePercent\" AS REAL) <= 100");
                table.HasCheckConstraint(
                    "CK_grid_compensation_rules_validity",
                    "\"ValidityEnd\" IS NULL OR \"ValidityEnd\" > \"ValidityStart\"");
                table.HasCheckConstraint(
                    "CK_grid_compensation_rules_resolution_nonempty",
                    "\"ResolutionCode\" <> ''");
            });
            entity.HasKey(rule => rule.Id);
            entity.Property(rule => rule.Distributor).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(rule => rule.Group).HasConversion<string>().HasMaxLength(8);
            entity.Property(rule => rule.Subgroup).HasConversion<string>().HasMaxLength(16);
            entity.Property(rule => rule.Modality).HasConversion<string>().HasMaxLength(32);
            entity.Property(rule => rule.Post).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(rule => rule.ReferenceYear).IsRequired();
            entity.Property(rule => rule.ValidityStart).IsRequired();
            entity.Property(rule => rule.ValidityEnd);
            entity.Property(rule => rule.ProgressivePercent).HasPrecision(8, 4).IsRequired();
            entity.Property(rule => rule.BaseComponent).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(rule => rule.ResolutionCode).HasMaxLength(120).IsRequired();
            entity.Property(rule => rule.SourceUrl).HasMaxLength(500).IsRequired();
            entity.Property(rule => rule.SourceDocumentHash).HasMaxLength(128);
            entity.Property(rule => rule.AccessedAt).IsRequired();
            entity.Property(rule => rule.IsComplete).IsRequired();
            entity.HasIndex(rule => new
            {
                rule.Distributor,
                rule.Group,
                rule.Subgroup,
                rule.Modality,
                rule.Post,
                rule.ReferenceYear,
                rule.ValidityStart
            }).IsUnique();
        });

        modelBuilder.Entity<AneelTariffImportRecord>(entity =>
        {
            entity.ToTable("aneel_tariff_imports", table =>
            {
                table.HasCheckConstraint(
                    "CK_aneel_tariff_imports_status",
                    "\"Status\" IN ('Running','Succeeded','Failed')");
                table.HasCheckConstraint(
                    "CK_aneel_tariff_imports_counts_nonnegative",
                    "\"RawRecordCount\" >= 0 AND \"AcceptedProfileCount\" >= 0 AND \"RejectedRecordCount\" >= 0 AND \"InsertedProfileCount\" >= 0 AND \"ClosedProfileCount\" >= 0 AND \"UnchangedProfileCount\" >= 0");
            });
            entity.HasKey(record => record.Id);
            entity.Property(record => record.JobId).HasMaxLength(128);
            entity.Property(record => record.FiltersJson).HasColumnType("text").IsRequired();
            entity.Property(record => record.SourceUrl).HasMaxLength(500).IsRequired();
            entity.Property(record => record.SourceHash).HasMaxLength(128);
            entity.Property(record => record.StartedAt).HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(record => record.CompletedAt).HasColumnType("timestamp with time zone");
            entity.Property(record => record.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(record => record.RawRecordCount).IsRequired();
            entity.Property(record => record.AcceptedProfileCount).IsRequired();
            entity.Property(record => record.RejectedRecordCount).IsRequired();
            entity.Property(record => record.InsertedProfileCount).IsRequired();
            entity.Property(record => record.ClosedProfileCount).IsRequired();
            entity.Property(record => record.UnchangedProfileCount).IsRequired();
            entity.Property(record => record.ErrorMessage).HasMaxLength(200);
            entity.HasIndex(record => new { record.Status, record.StartedAt });
            entity.HasIndex(record => record.SourceHash);
        });
    }
}

// Os records Solar* são o ÚNICO caminho seguro para criar e transicionar as
// entidades do fluxo comercial. Métodos de fábrica e de transição recebem a
// entidade pai (nunca apenas o ID) e validam o estado cross-entity antes de
// aprovar/gerar. Acesso direto ao DbContext ou SQL bruto é interno e NÃO
// revalida esses invariantes; as CHECK constraints cobrem apenas combinações
// básicas (enum de status, valores não negativos, campos exigidos por status,
// Grupo/Modalidade não vazios). Não há triggers.
//
// Validade de JSON (sintaxe) é autoridade da aplicação via SolarValidation.RequireJson.
// Não usamos CHECK de JSON no PostgreSQL porque validar JSON em coluna text exige
// cast/função não portável (quebraria o SQLite usado nos testes) e produziria uma
// falsa garantia. As colunas são text e apenas presença (não vazio) é garantida
// quando o status exige.
public sealed class SolarSizingRecord
{
    public Guid Id { get; private set; }
    public Guid LeadId { get; private set; }
    public string Concessionaria { get; private set; } = "";
    public string Grupo { get; private set; } = "";
    public string Modalidade { get; private set; } = "";
    public string EngineVersion { get; private set; } = "";
    public string InputsJson { get; private set; } = "";
    public string? ResultsJson { get; private set; }
    public SolarSizingStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<SolarQuoteRecord> Quotes { get; set; } = [];

    public static SolarSizingRecord Create(
        Guid id,
        Guid leadId,
        string concessionaria,
        string grupo,
        string modalidade,
        string engineVersion,
        string inputsJson)
    {
        SolarValidation.RequireGuid(id, nameof(id), "Id é obrigatório.");
        SolarValidation.RequireGuid(leadId, nameof(leadId), "LeadId é obrigatório.");
        concessionaria = SolarValidation.RequireText(concessionaria, nameof(concessionaria), "Concessionária é obrigatória.");
        grupo = SolarValidation.RequireText(grupo, nameof(grupo), "Grupo é obrigatório.");
        modalidade = SolarValidation.RequireText(modalidade, nameof(modalidade), "Modalidade é obrigatória.");
        engineVersion = SolarValidation.RequireText(engineVersion, nameof(engineVersion), "Versão do motor é obrigatória.");
        inputsJson = SolarValidation.RequireJson(inputsJson, nameof(inputsJson), "Snapshot de entradas é obrigatório.");

        var now = DateTimeOffset.UtcNow;
        return new SolarSizingRecord
        {
            Id = id,
            LeadId = leadId,
            Concessionaria = concessionaria,
            Grupo = grupo,
            Modalidade = modalidade,
            EngineVersion = engineVersion,
            InputsJson = inputsJson,
            Status = SolarSizingStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Calculate(string resultsJson)
    {
        resultsJson = SolarValidation.RequireJson(resultsJson, nameof(resultsJson), "Snapshot de resultados é obrigatório.");

        TransitionTo(SolarSizingStatus.Calculated);
        ResultsJson = resultsJson;
        Touch();
    }

    public void Approve()
    {
        if (string.IsNullOrWhiteSpace(ResultsJson))
            throw new InvalidOperationException("Dimensionamento aprovado exige snapshot de resultados.");

        TransitionTo(SolarSizingStatus.Approved);
        Touch();
    }

    public void Cancel()
    {
        TransitionTo(SolarSizingStatus.Cancelled);
        Touch();
    }

    public bool CanTransitionTo(SolarSizingStatus target) =>
        SolarTransitions.CanSizingTransition(Status, target);

    private void TransitionTo(SolarSizingStatus target)
    {
        if (!CanTransitionTo(target))
            throw new InvalidOperationException($"Transição inválida de SolarSizingRecord {Status} para {target}.");
        Status = target;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}

public sealed class SolarQuoteRecord
{
    public Guid Id { get; private set; }
    public Guid SizingId { get; private set; }
    public string ItemsJson { get; private set; } = "";
    public decimal TotalCost { get; private set; }
    public decimal MarginPercent { get; private set; }
    public decimal TaxPercent { get; private set; }
    public decimal TotalPrice { get; private set; }
    public string ConditionsJson { get; private set; } = "";
    public DateTimeOffset ValidUntil { get; private set; }
    public string SnapshotJson { get; private set; } = "";
    public SolarQuoteStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public SolarSizingRecord Sizing { get; set; } = null!;
    public ICollection<ProposalRecord> Proposals { get; set; } = [];

    public static SolarQuoteRecord Create(
        Guid id,
        SolarSizingRecord sizing,
        string itemsJson,
        decimal totalCost,
        decimal marginPercent,
        decimal taxPercent,
        decimal totalPrice,
        string conditionsJson,
        DateTimeOffset validUntil,
        string snapshotJson)
    {
        SolarValidation.RequireGuid(id, nameof(id), "Id é obrigatório.");
        ArgumentNullException.ThrowIfNull(sizing);

        if (sizing.Status is not (SolarSizingStatus.Calculated or SolarSizingStatus.Approved))
            throw new InvalidOperationException(
                "Cotação só pode ser criada a partir de dimensionamento calculado ou aprovado.");

        itemsJson = SolarValidation.RequireJson(itemsJson, nameof(itemsJson), "Itens são obrigatórios.");
        snapshotJson = SolarValidation.RequireJson(snapshotJson, nameof(snapshotJson), "Snapshot é obrigatório.");
        conditionsJson = SolarValidation.RequireJson(conditionsJson, nameof(conditionsJson), "Condições são obrigatórias.");

        SolarValidation.RequireMoney(totalCost, nameof(totalCost));
        SolarValidation.RequireMoney(totalPrice, nameof(totalPrice));
        SolarValidation.RequirePercent(marginPercent, nameof(marginPercent));
        SolarValidation.RequirePercent(taxPercent, nameof(taxPercent));

        var now = DateTimeOffset.UtcNow;
        return new SolarQuoteRecord
        {
            Id = id,
            SizingId = sizing.Id,
            ItemsJson = itemsJson,
            TotalCost = totalCost,
            MarginPercent = marginPercent,
            TaxPercent = taxPercent,
            TotalPrice = totalPrice,
            ConditionsJson = conditionsJson,
            ValidUntil = validUntil,
            SnapshotJson = snapshotJson,
            Status = SolarQuoteStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Approve(SolarSizingRecord sizing)
    {
        ArgumentNullException.ThrowIfNull(sizing);

        if (sizing.Id != SizingId)
            throw new InvalidOperationException(
                "Cotação só pode ser aprovada pelo dimensionamento vinculado.");

        if (sizing.Status != SolarSizingStatus.Approved)
            throw new InvalidOperationException(
                "Cotação só pode ser aprovada a partir de dimensionamento aprovado.");

        if (string.IsNullOrWhiteSpace(SnapshotJson))
            throw new InvalidOperationException("Cotação aprovada exige snapshot.");

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
            throw new InvalidOperationException($"Transição inválida de SolarQuoteRecord {Status} para {target}.");
        Status = target;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}

public sealed class ProposalRecord
{
    public Guid Id { get; private set; }
    public Guid QuoteId { get; private set; }
    public string TemplateVersion { get; private set; } = "";
    public string PayloadJson { get; private set; } = "";
    public string? FileUrl { get; private set; }
    public string? FileHash { get; private set; }
    public ProposalStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public SolarQuoteRecord Quote { get; set; } = null!;

    public static ProposalRecord Create(
        Guid id,
        SolarQuoteRecord quote,
        string templateVersion,
        string payloadJson)
    {
        SolarValidation.RequireGuid(id, nameof(id), "Id é obrigatório.");
        ArgumentNullException.ThrowIfNull(quote);

        if (quote.Status != SolarQuoteStatus.Approved)
            throw new InvalidOperationException(
                "Proposta só pode ser criada a partir de cotação aprovada.");

        templateVersion = SolarValidation.RequireText(templateVersion, nameof(templateVersion), "Versão do template é obrigatória.");
        payloadJson = SolarValidation.RequireJson(payloadJson, nameof(payloadJson), "Snapshot do payload é obrigatório.");

        var now = DateTimeOffset.UtcNow;
        return new ProposalRecord
        {
            Id = id,
            QuoteId = quote.Id,
            TemplateVersion = templateVersion,
            PayloadJson = payloadJson,
            Status = ProposalStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Generate(SolarQuoteRecord quote, string fileUrl, string? fileHash)
    {
        ArgumentNullException.ThrowIfNull(quote);

        if (quote.Id != QuoteId)
            throw new InvalidOperationException(
                "Proposta só pode ser gerada pela cotação vinculada.");

        if (quote.Status != SolarQuoteStatus.Approved)
            throw new InvalidOperationException(
                "Proposta só pode ser gerada a partir de cotação aprovada.");

        if (string.IsNullOrWhiteSpace(PayloadJson))
            throw new InvalidOperationException("Proposta gerada exige snapshot do payload.");

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
            throw new InvalidOperationException($"Transição inválida de ProposalRecord {Status} para {target}.");
        Status = target;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}

public sealed class TariffProfileRecord
{
    private readonly List<TariffComponentRecord> _components = [];

    public Guid Id { get; private set; }
    public Distributor Distributor { get; private set; }
    public TariffGroup Group { get; private set; }
    public TariffSubgroup Subgroup { get; private set; }
    public TariffModality Modality { get; private set; }
    public DateOnly ValidityStart { get; private set; }
    public DateOnly? ValidityEnd { get; private set; }
    public string ResolutionCode { get; private set; } = "";
    public string SourceUrl { get; private set; } = "";
    public string? SourceDocumentHash { get; private set; }
    public DateTimeOffset AccessedAt { get; private set; }
    public bool IsComplete { get; private set; }
    public int Version { get; private set; } = 1;
    public bool IsCurrent { get; private set; } = true;
    public IReadOnlyCollection<TariffComponentRecord> Components => _components.AsReadOnly();

    public static TariffProfileRecord Create(
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
        IReadOnlyList<TariffComponent> components,
        int version = 1,
        bool isCurrent = true)
    {
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version), "Versão do perfil deve ser maior ou igual a 1.");

        var domain = TariffProfile.Create(
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
            components);

        var record = new TariffProfileRecord
        {
            Id = domain.Id,
            Distributor = domain.Distributor,
            Group = domain.Group,
            Subgroup = domain.Subgroup,
            Modality = domain.Modality,
            ValidityStart = domain.ValidityStart,
            ValidityEnd = domain.ValidityEnd,
            ResolutionCode = domain.ResolutionCode,
            SourceUrl = domain.SourceUrl,
            SourceDocumentHash = domain.SourceDocumentHash,
            AccessedAt = domain.AccessedAt,
            IsComplete = domain.IsComplete,
            Version = version,
            IsCurrent = isCurrent
        };

        record._components.AddRange(domain.Components.Select(component => TariffComponentRecord.FromDomain(domain.Id, component)));

        return record;
    }

    /// <summary>
    /// Encerra a vigência do perfil na véspera do início de uma versão substituta.
    /// Nunca estende a vigência: se o perfil já encerra em data anterior, permanece
    /// inalterado. Uma versão encerrada é preservada (imutável) em vez de removida.
    /// </summary>
    internal void CloseAt(DateOnly newEnd)
    {
        if (newEnd <= ValidityStart)
            throw new InvalidOperationException(
                "Não é possível encerrar um perfil com vigência final anterior ou igual à inicial.");

        if (ValidityEnd is { } currentEnd && currentEnd <= newEnd)
            return;

        ValidityEnd = newEnd;
    }

    /// <summary>
    /// Marca o perfil como substituído por uma versão posterior com a mesma vigência
    /// (retificação de mesmo período). O perfil permanece armazenado para
    /// reprodutibilidade, mas deixa de ser elegível em buscas e listagens.
    /// </summary>
    internal void Supersede() => IsCurrent = false;
}

public sealed class TariffComponentRecord
{
    public Guid Id { get; private set; }
    public Guid ProfileId { get; private set; }
    public TariffComponentKind Kind { get; private set; }
    public TariffUnit Unit { get; private set; }
    public TariffPost Post { get; private set; }
    public decimal Value { get; private set; }
    public bool TaxIncluded { get; private set; }
    public string? SourcePage { get; private set; }
    public TariffProfileRecord Profile { get; set; } = null!;

    internal static TariffComponentRecord FromDomain(Guid profileId, TariffComponent component) => new()
    {
        Id = Guid.NewGuid(),
        ProfileId = profileId,
        Kind = component.Kind,
        Unit = component.Unit,
        Post = component.Post,
        Value = component.Value,
        TaxIncluded = component.TaxIncluded,
        SourcePage = component.SourcePage
    };
}

public sealed class GridCompensationRuleRecord
{
    public Guid Id { get; private set; }
    public Distributor Distributor { get; private set; }
    public TariffGroup? Group { get; private set; }
    public TariffSubgroup? Subgroup { get; private set; }
    public TariffModality? Modality { get; private set; }
    public TariffPost Post { get; private set; }
    public int ReferenceYear { get; private set; }
    public DateOnly ValidityStart { get; private set; }
    public DateOnly? ValidityEnd { get; private set; }
    public decimal ProgressivePercent { get; private set; }
    public TariffComponentKind BaseComponent { get; private set; }
    public string ResolutionCode { get; private set; } = "";
    public string SourceUrl { get; private set; } = "";
    public string? SourceDocumentHash { get; private set; }
    public DateTimeOffset AccessedAt { get; private set; }
    public bool IsComplete { get; private set; }

    public static GridCompensationRuleRecord Create(
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
        var domain = GridCompensationRule.Create(
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

        return new GridCompensationRuleRecord
        {
            Id = domain.Id,
            Distributor = domain.Distributor,
            Group = domain.Group,
            Subgroup = domain.Subgroup,
            Modality = domain.Modality,
            Post = domain.Post,
            ReferenceYear = domain.ReferenceYear,
            ValidityStart = domain.ValidityStart,
            ValidityEnd = domain.ValidityEnd,
            ProgressivePercent = domain.ProgressivePercent,
            BaseComponent = domain.BaseComponent,
            ResolutionCode = domain.ResolutionCode,
            SourceUrl = domain.SourceUrl,
            SourceDocumentHash = domain.SourceDocumentHash,
            AccessedAt = domain.AccessedAt,
            IsComplete = domain.IsComplete
        };
    }
}

public sealed class LeadRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Message { get; set; } = "";
    public string Notes { get; set; } = "";
    public LeadStage Stage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? WonAt { get; set; }
    public DateTimeOffset? ReminderAt { get; set; }
    public ICollection<LeadActivityRecord> Activities { get; set; } = [];
    public ICollection<LeadTaskRecord> Tasks { get; set; } = [];
}

public sealed class LeadActivityRecord
{
    public Guid Id { get; set; }
    public Guid LeadId { get; set; }
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public LeadRecord Lead { get; set; } = null!;
}

public sealed class LeadTaskRecord
{
    public Guid Id { get; set; }
    public Guid LeadId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public LeadRecord Lead { get; set; } = null!;
}

public sealed class CrmNotificationRecord
{
    public Guid Id { get; set; }
    public CrmNotificationKind Kind { get; set; }
    public Guid LeadId { get; set; }
    public Guid? TaskId { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset DueAt { get; set; }
    public DateOnly DueDay { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
