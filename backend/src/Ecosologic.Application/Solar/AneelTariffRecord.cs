using Ecosologic.Domain.Solar;

namespace Ecosologic.Application.Solar;

/// <summary>
/// Registro bruto tipado de uma linha do relatório ANEEL. Campos de texto são
/// mantidos como strings para que a normalização (cultura, data, mapeamentos)
/// ocorra em um único ponto e seja testável sem depender de rede.
/// </summary>
public sealed record AneelTariffRecord(
    string? DistributorName,
    string? Group,
    string? Subgroup,
    string? Modality,
    string? Post,
    string? Component,
    string? Unit,
    string? Value,
    string? ResolutionCode,
    string? ValidityStart,
    string? ValidityEnd,
    string? SourcePage);

/// <summary>
/// Componente tarifário normalizado. <see cref="Value"/>/<see cref="Unit"/> são os
/// valores internos convertidos para R$/kWh; <see cref="SourceValue"/> e
/// <see cref="SourceUnit"/> preservam o valor e a unidade originais da fonte.
/// </summary>
public sealed record AneelNormalizedComponent(
    TariffComponentKind Kind,
    TariffUnit Unit,
    TariffPost Post,
    decimal Value,
    bool TaxIncluded,
    string? SourcePage,
    decimal SourceValue,
    string SourceUnit,
    string? SourceDocumentHash);

/// <summary>
/// Perfil tarifário normalizado, pronto para alimentar <see cref="TariffProfile"/>.
/// <see cref="Distributor"/> carrega o valor de domínio persistido, enquanto
/// <see cref="SourceDistributorName"/> preserva o rótulo original da fonte (rótulo do
/// relatório ou nome canônico) para proveniência.
/// </summary>
public sealed record AneelNormalizedProfile(
    Distributor Distributor,
    string SourceDistributorName,
    TariffGroup Group,
    TariffSubgroup Subgroup,
    TariffModality Modality,
    DateOnly ValidityStart,
    DateOnly? ValidityEnd,
    string ResolutionCode,
    string SourceUrl,
    string? SourceDocumentHash,
    DateTimeOffset AccessedAt,
    bool IsComplete,
    IReadOnlyList<AneelNormalizedComponent> Components);

/// <summary>
/// Resultado da normalização de um lote ANEEL. Além dos perfis validados, expõe as
/// contagens reais de linhas brutas aceitas e rejeitadas (ignoradas pelos filtros),
/// para que a auditoria não precise inferir rejeições como "brutos menos perfis",
/// que se torna incorreto quando várias linhas colapsam em um único perfil.
/// </summary>
public sealed record AneelNormalizationResult(
    IReadOnlyList<AneelNormalizedProfile> Profiles,
    int AcceptedRawRecordCount,
    int RejectedRawRecordCount);

public class AneelNormalizationException(string message) : Exception(message);

/// <summary>
/// A cobertura esperada (ambas as distribuidoras e os subgrupos B1/B2/B3) não foi
/// atingida pelas linhas aceitas. Falha tipada e observável em vez de importação
/// parcial silenciosa.
/// </summary>
public sealed class AneelCoverageException(string message) : AneelNormalizationException(message);
