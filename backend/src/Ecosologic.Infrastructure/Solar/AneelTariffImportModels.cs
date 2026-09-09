namespace Ecosologic.Infrastructure.Solar;

public enum AneelTariffImportStatus
{
    Running,
    Succeeded,
    Failed
}

/// <summary>
/// Registro de auditoria de uma execução de sincronização ANEEL. Persiste a
/// identidade da execução, os filtros aplicados, a URL e o hash da fonte, os
/// contadores sanitizados de cada etapa e um erro sanitizado (sem conteúdo de
/// resposta, tokens ou credenciais). O ciclo de vida é <c>Running</c> →
/// <c>Succeeded</c>/<c>Failed</c>; a falha nunca remove registros tarifários já
/// vigentes, que permanecem imutáveis do ponto de vista do serviço de importação.
/// </summary>
public sealed class AneelTariffImportRecord
{
    public Guid Id { get; private set; }
    public string? JobId { get; private set; }
    public string FiltersJson { get; private set; } = "";
    public string SourceUrl { get; private set; } = "";
    public string? SourceHash { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public AneelTariffImportStatus Status { get; private set; }
    public int RawRecordCount { get; private set; }
    public int AcceptedProfileCount { get; private set; }
    public int RejectedRecordCount { get; private set; }
    public int InsertedProfileCount { get; private set; }
    public int ClosedProfileCount { get; private set; }
    public int UnchangedProfileCount { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static AneelTariffImportRecord Create(Guid id, string? jobId, string filtersJson, string sourceUrl)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id da importação é obrigatório.", nameof(id));
        if (string.IsNullOrWhiteSpace(filtersJson))
            throw new ArgumentException("Filtros da importação são obrigatórios.", nameof(filtersJson));
        if (string.IsNullOrWhiteSpace(sourceUrl))
            throw new ArgumentException("URL da fonte ANEEL é obrigatória.", nameof(sourceUrl));

        return new AneelTariffImportRecord
        {
            Id = id,
            JobId = jobId,
            FiltersJson = filtersJson.Trim(),
            SourceUrl = sourceUrl.Trim(),
            StartedAt = DateTime.UtcNow,
            Status = AneelTariffImportStatus.Running
        };
    }

    internal void SetSource(string? sourceHash, int rawRecordCount)
    {
        SourceHash = sourceHash;
        RawRecordCount = rawRecordCount;
    }

    internal void SetValidationCounts(int acceptedProfileCount, int rejectedRecordCount)
    {
        AcceptedProfileCount = acceptedProfileCount;
        RejectedRecordCount = rejectedRecordCount;
    }

    internal void MarkSucceeded(int insertedProfiles, int closedProfiles, int unchangedProfiles)
    {
        InsertedProfileCount = insertedProfiles;
        ClosedProfileCount = closedProfiles;
        UnchangedProfileCount = unchangedProfiles;
        Status = AneelTariffImportStatus.Succeeded;
        CompletedAt = DateTime.UtcNow;
    }

    internal void MarkFailed(string errorMessage)
    {
        Status = AneelTariffImportStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reabre o registro para uma nova tentativa (retry) da MESMA execução, voltando ao
    /// estado <c>Running</c> e limpando a conclusão/erro da tentativa anterior. Preserva
    /// o <c>StartedAt</c> original (o "início" da execução) e o <c>JobId</c> já atribuído.
    /// </summary>
    internal void MarkRunning()
    {
        Status = AneelTariffImportStatus.Running;
        CompletedAt = null;
        ErrorMessage = null;
    }

    /// <summary>
    /// Define o identificador do job Hangfire de forma idempotente (apenas quando ainda
    /// nulo), para que o job em execução e o controlador que enfileira possam escrevê-lo
    /// sem risco de sobrescrever com valor nulo ou divergente durante a corrida.
    /// </summary>
    internal void SetJobId(string jobId)
    {
        if (JobId is null)
            JobId = jobId;
    }
}
