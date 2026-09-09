namespace Ecosologic.Application.Solar;

/// <summary>
/// Configuração de uma sincronização ANEEL. O cliente HTTP executa uma única
/// tentativa por chamada; NÃO há retry no cliente. A política de retry é de
/// responsabilidade do Hangfire (job recorrente com <c>AutomaticRetry</c>), que
/// reexecuta a sincronização inteira quando ela falha. Isso mantém a idempotência
/// e evita duplicar parcialmente uma importação ao repetir apenas o transporte.
/// </summary>
public sealed class AneelTariffSyncOptions
{
    public static readonly TimeSpan DefaultHttpTimeout = TimeSpan.FromSeconds(30);

    public const string DefaultCluster = "https://WABI-SOUTH-CENTRAL-US-redirect.analysis.windows.net";
    public const string DefaultReportId = "df28bff5-d988-496c-93e1-d154b8b172fc";
    public const string DefaultWorkspaceId = "d7f9ec8e-b0fd-4589-ac38-47564cb07d8c";
    public const string DefaultModelId = "5039649";

    public AneelTariffSyncOptions(
        string sourceUrl,
        TimeSpan? httpTimeout = null,
        string cluster = DefaultCluster,
        string reportId = DefaultReportId,
        string workspaceId = DefaultWorkspaceId,
        string modelId = DefaultModelId)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            throw new ArgumentException("A URL da fonte ANEEL é obrigatória.", nameof(sourceUrl));

        if (httpTimeout is { } timeout && timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(httpTimeout), "O timeout deve ser positivo.");

        SourceUrl = sourceUrl.Trim();
        HttpTimeout = httpTimeout ?? DefaultHttpTimeout;
        Cluster = (string.IsNullOrWhiteSpace(cluster) ? DefaultCluster : cluster).TrimEnd('/');
        ReportId = string.IsNullOrWhiteSpace(reportId) ? DefaultReportId : reportId.Trim();
        WorkspaceId = string.IsNullOrWhiteSpace(workspaceId) ? DefaultWorkspaceId : workspaceId.Trim();
        ModelId = string.IsNullOrWhiteSpace(modelId) ? DefaultModelId : modelId.Trim();
    }

    public string SourceUrl { get; }
    public TimeSpan HttpTimeout { get; }

    public string Cluster { get; }
    public string ReportId { get; }
    public string WorkspaceId { get; }
    public string ModelId { get; }
}
