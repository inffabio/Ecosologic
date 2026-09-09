namespace Ecosologic.Application.Solar;

/// <summary>
/// Fonte externa das tarifas ANEEL. Retorna registros brutos já tipados e o hash
/// determinístico do payload consultado, sem expor o protocolo HTTP/Power BI aos
/// demais consumidores.
/// </summary>
public interface IAneelTariffSource
{
    Task<AneelTariffFetchResult> FetchAsync(AneelTariffSyncOptions options, CancellationToken cancellationToken = default);
}

public sealed record AneelTariffFetchResult(
    IReadOnlyList<AneelTariffRecord> Records,
    string SourceHash,
    string SourceUrl,
    DateTimeOffset FetchedAt);

/// <summary>
/// Códigos de diagnóstico seguros para falhas da fonte ANEEL. São estáveis e não
/// contêm conteúdo de resposta, tokens ou credenciais; servem para correlacionar
/// erros nos logs sem vazar dados sensíveis.
/// </summary>
public static class AneelErrorCode
{
    public const string Unknown = "ANEEL_UNKNOWN";
    public const string Transport = "ANEEL_TRANSPORT";
    public const string InvalidPayload = "ANEEL_INVALID_PAYLOAD";
    public const string MissingColumns = "ANEEL_SCHEMA_MISSING_COLUMNS";
    public const string EmptyData = "ANEEL_SCHEMA_EMPTY";
    public const string DsrInvalid = "ANEEL_DSR_INVALID";
    public const string EmbedTokenMissing = "ANEEL_EMBED_TOKEN_MISSING";
    public const string EmbedTokenInvalid = "ANEEL_EMBED_TOKEN_INVALID";
    public const string EmbedTokenExpired = "ANEEL_EMBED_TOKEN_EXPIRED";
    public const string VisualQueryMissing = "ANEEL_VISUAL_QUERY_MISSING";
    public const string InvalidModel = "ANEEL_INVALID_MODEL";
}

/// <summary>
/// Falha genérica e sanitizada ao consultar a fonte ANEEL. Nunca expõe corpo de
/// resposta, tokens ou credenciais na mensagem nem retém exceções de transporte
/// ou parsing como <see cref="Exception.InnerException"/> (que poderiam vazar
/// conteúdo). O <see cref="Code"/> preserva um diagnóstico seguro e estável.
/// </summary>
public class AneelTariffSourceException : Exception
{
    public AneelTariffSourceException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>
/// O portal ANEEL não expôs um token de incorporação (embed token) no fetch da
/// página. Falha explícita: a sincronização não deve prosseguir sem token válido.
/// </summary>
public sealed class AneelEmbedTokenException : AneelTariffSourceException
{
    public AneelEmbedTokenException(string code, string message) : base(code, message) { }
}

/// <summary>
/// O comando de consulta do visual (SemanticQueryDataShapeCommand) não pôde ser
/// extraído de forma confiável do bootstrap do relatório Power BI. Falha explícita
/// em vez de fabricar um envelope de linhas ou retornar vazio.
/// </summary>
public sealed class AneelVisualQueryException : AneelTariffSourceException
{
    public AneelVisualQueryException(string code, string message) : base(code, message) { }
}

/// <summary>
/// A resposta da consulta não atende ao esquema esperado (colunas obrigatórias
/// ausentes, estrutura descriptor/DSR inesperada ou nenhuma linha). Falha explícita.
/// </summary>
public sealed class AneelTariffSchemaException : AneelTariffSourceException
{
    public AneelTariffSchemaException(string code, string message) : base(code, message) { }
}
