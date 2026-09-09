using System.Text.Json;
using Ecosologic.Application.Solar;
using Ecosologic.Domain.Solar;
using Ecosologic.Infrastructure.Solar;

namespace Ecosologic.Api.Tests;

public class AneelTariffNormalizerTests
{
    private const string SourceUrl = "https://www.aneel.gov.br/relatorio-tarifas";
    private const string SourceHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly AneelTariffNormalizer Normalizer = new();

    private static AneelTariffSyncOptions Options() => new(SourceUrl);

    private static AneelTariffRecord Row(
        string? distributor = "Light",
        string? group = "B",
        string? subgroup = "B1",
        string? modality = "Convencional",
        string? post = "Único",
        string? component = "TUSD Fio B",
        string? unit = "R$/MWh",
        string? value = "41.20",
        string? resolutionCode = "RES 3.242/2024",
        string? validityStart = "2024-01-01",
        string? validityEnd = "2024-12-31",
        string? sourcePage = null) =>
        new(distributor, group, subgroup, modality, post, component, unit, value, resolutionCode, validityStart, validityEnd, sourcePage);

    private static IReadOnlyList<AneelTariffRecord> FullCoverage(
        string lightName = "Light",
        string enelName = "Enel RJ")
    {
        var rows = new List<AneelTariffRecord>();
        foreach (var subgroup in new[] { "B1", "B2", "B3" })
            rows.Add(Row(distributor: lightName, subgroup: subgroup));
        foreach (var subgroup in new[] { "B1", "B2", "B3" })
            rows.Add(Row(distributor: enelName, subgroup: subgroup));
        return rows;
    }

    private static IReadOnlyList<AneelTariffRecord> WithOne(
        IReadOnlyList<AneelTariffRecord> rows,
        string distributor,
        string subgroup,
        Func<AneelTariffRecord, AneelTariffRecord> mutate) =>
        rows.Select(r => r.DistributorName == distributor && r.Subgroup == subgroup ? mutate(r) : r).ToList();

    private static IReadOnlyList<AneelTariffRecord> LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "aneel-tariffs-light-enel-b.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var rows = document.RootElement.GetProperty("rows");
        return rows.Deserialize<List<AneelTariffRecord>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    // --- Mapeamento de distribuidora ---

    [Fact]
    public void Normalize_maps_light_and_enel_by_exact_report_name()
    {
        var result = Normalizer.Normalize(FullCoverage(), SourceHash, Options());

        Assert.Equal(6, result.Count);
        Assert.Contains(result, p => p.Distributor == Distributor.Light);
        Assert.Contains(result, p => p.Distributor == Distributor.EnelRio);
    }

    [Theory]
    [InlineData(" LIGHT ")]
    [InlineData("light")]
    [InlineData("ENEL RJ")]
    [InlineData("enel rj")]
    public void Normalize_canonicalizes_case_and_whitespace_for_matching(string name)
    {
        var isEnel = name.Trim().StartsWith("enel", StringComparison.OrdinalIgnoreCase);
        var rows = isEnel ? FullCoverage(enelName: name) : FullCoverage(lightName: name);

        var result = Normalizer.Normalize(rows, SourceHash, Options());

        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void Normalize_accepts_canonical_distributor_names()
    {
        var result = Normalizer.Normalize(
            FullCoverage(
                lightName: "Light Serviços de Eletricidade S.A.",
                enelName: "Enel Distribuição Rio"),
            SourceHash,
            Options());

        Assert.Equal(6, result.Count);
        Assert.Contains(result, p => p.Distributor == Distributor.Light);
        Assert.Contains(result, p => p.Distributor == Distributor.EnelRio);
    }

    [Fact]
    public void Normalize_maps_report_label_and_canonical_name_to_same_distributor()
    {
        var reportLabel = Normalizer.Normalize(FullCoverage(), SourceHash, Options());
        var canonical = Normalizer.Normalize(
            FullCoverage(
                lightName: "Light Serviços de Eletricidade S.A.",
                enelName: "Enel Distribuição Rio"),
            SourceHash,
            Options());

        Assert.Equal(
            reportLabel.Select(p => p.Distributor).OrderBy(d => d),
            canonical.Select(p => p.Distributor).OrderBy(d => d));
    }

    [Fact]
    public void Normalize_preserves_source_distributor_label_for_provenance()
    {
        var canonical = Normalizer.Normalize(
            FullCoverage(
                lightName: "Light Serviços de Eletricidade S.A.",
                enelName: "Enel Distribuição Rio"),
            SourceHash,
            Options());

        Assert.All(canonical, p => Assert.Equal(
            p.Distributor == Distributor.Light ? "Light Serviços de Eletricidade S.A." : "Enel Distribuição Rio",
            p.SourceDistributorName));

        var reportLabels = Normalizer.Normalize(FullCoverage(), SourceHash, Options());

        Assert.All(reportLabels, p => Assert.Equal(
            p.Distributor == Distributor.Light ? "Light" : "Enel RJ",
            p.SourceDistributorName));
    }

    [Fact]
    public void AneelDistributors_registry_distinguishes_report_labels_from_canonical_names()
    {
        Assert.Equal("Light", AneelDistributors.Light.ReportLabel);
        Assert.Equal("Light Serviços de Eletricidade S.A.", AneelDistributors.Light.CanonicalName);
        Assert.Equal(Distributor.Light, AneelDistributors.Light.Distributor);

        Assert.Equal("Enel RJ", AneelDistributors.EnelRio.ReportLabel);
        Assert.Equal("Enel Distribuição Rio", AneelDistributors.EnelRio.CanonicalName);
        Assert.Equal(Distributor.EnelRio, AneelDistributors.EnelRio.Distributor);
    }

    [Fact]
    public void Normalize_output_feeds_domain_tariff_profile_creation()
    {
        var profiles = Normalizer.Normalize(FullCoverage(), SourceHash, Options());

        foreach (var profile in profiles)
        {
            var components = profile.Components
                .Select(c => TariffComponent.Create(
                    c.Kind, c.Unit, c.Post, c.Value, c.TaxIncluded, c.SourcePage))
                .ToList();

            var domain = TariffProfile.Create(
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
                components);

            Assert.Equal(profile.Distributor, domain.Distributor);
            Assert.Equal(profile.Components.Count, domain.Components.Count);
        }
    }

    [Theory]
    [InlineData("Companhia Paranaense de Energia - Copel")]
    [InlineData("Light Energia")]
    [InlineData("Enel Distribuição")]
    public void Normalize_rejects_unknown_distributor_aliases(string name)
    {
        Assert.Throws<AneelNormalizationException>(() =>
            Normalizer.Normalize([Row(distributor: name)], SourceHash, Options()));
    }

    // --- Filtros de subgrupo e grupo ---

    [Fact]
    public void Normalize_accepts_B1_B2_B3_subgroups()
    {
        var result = Normalizer.Normalize(FullCoverage(), SourceHash, Options());

        var subgroups = result.Select(p => p.Subgroup).Distinct().ToHashSet();
        Assert.Contains(TariffSubgroup.B1, subgroups);
        Assert.Contains(TariffSubgroup.B2, subgroups);
        Assert.Contains(TariffSubgroup.B3, subgroups);
    }

    [Theory]
    [InlineData("B4", "B")]
    [InlineData("A4", "A")]
    public void Normalize_rejects_non_B1B2B3_subgroups(string subgroup, string group)
    {
        Assert.Throws<AneelNormalizationException>(() =>
            Normalizer.Normalize([Row(group: group, subgroup: subgroup)], SourceHash, Options()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    public void Normalize_rejects_missing_or_invalid_group(string? group)
    {
        Assert.Throws<AneelNormalizationException>(() =>
            Normalizer.Normalize(WithOne(FullCoverage(), "Light", "B1", r => r with { Group = group }), SourceHash, Options()));
    }

    // --- Filtros de componente e unidade ---

    [Theory]
    [InlineData("TE")]
    [InlineData("TUSD")]
    [InlineData("TUSD Distribuição")]
    public void Normalize_rejects_other_components(string component)
    {
        Assert.Throws<AneelNormalizationException>(() =>
            Normalizer.Normalize([Row(component: component)], SourceHash, Options()));
    }

    [Theory]
    [InlineData("R$/kW")]
    [InlineData("R$/kWh")]
    public void Normalize_rejects_other_units(string unit)
    {
        Assert.Throws<AneelNormalizationException>(() =>
            Normalizer.Normalize([Row(unit: unit)], SourceHash, Options()));
    }

    // --- Preservação da unidade e conversão ---

    [Fact]
    public void Normalize_preserves_source_unit_and_value()
    {
        var result = Normalizer.Normalize(FullCoverage(), SourceHash, Options());

        var component = LightB1(result).Components.Single();
        Assert.Equal("R$/MWh", component.SourceUnit);
        Assert.Equal(41.20m, component.SourceValue);
    }

    [Fact]
    public void Normalize_converts_mwh_to_kwh_for_internal_consumers()
    {
        var result = Normalizer.Normalize(FullCoverage(), SourceHash, Options());

        var component = LightB1(result).Components.Single();
        Assert.Equal(TariffUnit.KWh, component.Unit);
        Assert.Equal(0.0412m, component.Value);
    }

    // --- Vigência ---

    [Fact]
    public void Normalize_parses_validity_dates()
    {
        var result = Normalizer.Normalize(FullCoverage(), SourceHash, Options());

        Assert.All(result, profile =>
        {
            Assert.Equal(new DateOnly(2024, 1, 1), profile.ValidityStart);
            Assert.Equal(new DateOnly(2024, 12, 31), profile.ValidityEnd);
        });
    }

    // --- Rejeição de valores ausentes ou malformados ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-1")]
    public void Normalize_rejects_missing_or_malformed_value(string? value) =>
        Assert.Throws<AneelNormalizationException>(() =>
            Normalizer.Normalize(WithOne(FullCoverage(), "Light", "B1", r => r with { Value = value }), SourceHash, Options()));

    [Theory]
    [InlineData("41,20")]
    [InlineData("1,234.56")]
    [InlineData("41.200,50")]
    public void Normalize_rejects_group_separator_in_value(string value) =>
        Assert.Throws<AneelNormalizationException>(() =>
            Normalizer.Normalize(WithOne(FullCoverage(), "Light", "B1", r => r with { Value = value }), SourceHash, Options()));

    [Fact]
    public void Normalize_parses_explicit_decimal_point_value()
    {
        var result = Normalizer.Normalize(FullCoverage(), SourceHash, Options());

        Assert.Equal(41.20m, LightB1(result).Components.Single().SourceValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("01/01/2024")]
    public void Normalize_rejects_missing_or_malformed_validity_start(string? validityStart) =>
        Assert.Throws<AneelNormalizationException>(() =>
            Normalizer.Normalize(WithOne(FullCoverage(), "Light", "B1", r => r with { ValidityStart = validityStart }), SourceHash, Options()));

    [Fact]
    public void Normalize_rejects_missing_resolution_code() =>
        Assert.Throws<AneelNormalizationException>(() =>
            Normalizer.Normalize(WithOne(FullCoverage(), "Light", "B1", r => r with { ResolutionCode = "" }), SourceHash, Options()));

    // --- Fonte obrigatória ---

    [Fact]
    public void Options_requires_source_url() =>
        Assert.Throws<ArgumentException>(() => new AneelTariffSyncOptions(""));

    // --- Cobertura obrigatória (ambas distribuidoras + B1/B2/B3) ---

    [Fact]
    public void Normalize_rejects_incomplete_coverage_missing_subgroup()
    {
        var rows = FullCoverage().Where(r => !(r.DistributorName == "Enel RJ" && r.Subgroup == "B3")).ToList();

        Assert.Throws<AneelCoverageException>(() =>
            Normalizer.Normalize(rows, SourceHash, Options()));
    }

    [Fact]
    public void Normalize_rejects_incomplete_coverage_missing_distributor()
    {
        var rows = FullCoverage().Where(r => r.DistributorName == "Light").ToList();

        Assert.Throws<AneelCoverageException>(() =>
            Normalizer.Normalize(rows, SourceHash, Options()));
    }

    // --- Integração com a fixture ---

    [Fact]
    public void Normalize_fixture_returns_only_approved_light_enel_b123_fiob()
    {
        var profiles = Normalizer.Normalize(LoadFixture(), SourceHash, Options());

        Assert.Equal(6, profiles.Count);
        Assert.Equal([Distributor.Light, Distributor.EnelRio], profiles.Select(p => p.Distributor).Distinct().OrderBy(d => d).ToArray());
        Assert.All(profiles, p => Assert.Equal(TariffGroup.B, p.Group));
        Assert.All(profiles, p => Assert.Contains(p.Subgroup, new[] { TariffSubgroup.B1, TariffSubgroup.B2, TariffSubgroup.B3 }));
        Assert.All(profiles, p => Assert.All(p.Components, c => Assert.Equal(TariffComponentKind.FIO_B, c.Kind)));
        Assert.All(profiles, p => Assert.All(p.Components, c => Assert.Equal("R$/MWh", c.SourceUnit)));
        Assert.All(profiles, p => Assert.All(p.Components, c => Assert.Equal(TariffUnit.KWh, c.Unit)));
    }

    // --- Preservação do hash da fonte ---

    [Fact]
    public void Normalize_preserves_source_hash_on_profile_and_components()
    {
        var result = Normalizer.Normalize(FullCoverage(), SourceHash, Options());

        var profile = LightB1(result);
        Assert.Equal(SourceHash, profile.SourceDocumentHash);

        var component = profile.Components.Single();
        Assert.Equal(SourceHash, component.SourceDocumentHash);
    }

    // --- Múltiplos períodos de vigência ---

    [Fact]
    public void Normalize_models_multiple_validity_periods_as_distinct_profiles()
    {
        var rows = FullCoverage().ToList();
        rows.Add(Row(distributor: "Light", subgroup: "B1", validityStart: "2025-01-01", validityEnd: "2025-12-31"));

        var result = Normalizer.Normalize(rows, SourceHash, Options());

        Assert.Equal(7, result.Count);
        Assert.Contains(result, p => p.ValidityStart == new DateOnly(2024, 1, 1));
        Assert.Contains(result, p => p.ValidityStart == new DateOnly(2025, 1, 1));
    }

    // --- Sobreposição de vigência rejeitada ---

    [Fact]
    public void Normalize_rejects_overlapping_validity_periods()
    {
        var rows = FullCoverage().ToList();
        rows.Add(Row(distributor: "Light", subgroup: "B1", validityStart: "2024-06-01", validityEnd: "2025-06-30"));

        Assert.Throws<AneelNormalizationException>(() =>
            Normalizer.Normalize(rows, SourceHash, Options()));
    }

    // --- Deduplicação determinística ---

    [Fact]
    public void Normalize_deduplicates_identical_rows_within_period()
    {
        var rows = FullCoverage().ToList();
        rows.Add(Row(distributor: "Light", subgroup: "B1"));

        var result = Normalizer.Normalize(rows, SourceHash, Options());

        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void Normalize_rejects_conflicting_values_within_same_period()
    {
        var rows = FullCoverage().ToList();
        rows.Add(Row(distributor: "Light", subgroup: "B1", value: "42.10"));

        Assert.Throws<AneelNormalizationException>(() =>
            Normalizer.Normalize(rows, SourceHash, Options()));
    }

    [Fact]
    public void Normalize_throws_when_no_rows_are_accepted()
    {
        Assert.Throws<AneelNormalizationException>(() =>
            Normalizer.Normalize([Row(distributor: "Companhia Paranaense de Energia - Copel")], SourceHash, Options()));
    }

    // --- Contagens de aceite/rejeição expostas pelo normalizador ---

    [Fact]
    public void NormalizeWithCounts_reports_accepted_and_rejected_raw_counts()
    {
        var result = Normalizer.NormalizeWithCounts(LoadFixture(), SourceHash, Options());

        Assert.Equal(6, result.Profiles.Count);
        Assert.Equal(6, result.AcceptedRawRecordCount);
        Assert.Equal(5, result.RejectedRawRecordCount);
    }

    [Fact]
    public void NormalizeWithCounts_counts_deduplicated_rows_as_accepted_not_rejected()
    {
        var rows = FullCoverage().ToList();
        rows.Add(Row(distributor: "Light", subgroup: "B1"));

        var result = Normalizer.NormalizeWithCounts(rows, SourceHash, Options());

        Assert.Equal(6, result.Profiles.Count);
        Assert.Equal(7, result.AcceptedRawRecordCount);
        Assert.Equal(0, result.RejectedRawRecordCount);
    }

    [Fact]
    public void Normalize_wrapper_returns_same_profiles_as_counted_variant()
    {
        var counted = Normalizer.NormalizeWithCounts(FullCoverage(), SourceHash, Options());
        var plain = Normalizer.Normalize(FullCoverage(), SourceHash, Options());

        Assert.Equal(plain.Count, counted.Profiles.Count);
    }

    private static AneelNormalizedProfile LightB1(IReadOnlyList<AneelNormalizedProfile> profiles) =>
        profiles.Single(p => p.Distributor == Distributor.Light && p.Subgroup == TariffSubgroup.B1);
}
