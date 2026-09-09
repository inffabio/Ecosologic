using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ecosologic.Application.Solar;

namespace Ecosologic.Infrastructure.Solar;

/// <summary>
/// Adaptador HTTP para a fonte ANEEL (relatório Power BI do portal "Luz na Tarifa").
/// Encapsula o protocolo real do portal: busca da página (que expõe o campo
/// <c>accessToken</c> da configuração de incorporação), bootstrap da sessão
/// (modelsAndExploration + conceptualschema), extração do comando de consulta do
/// visual (usado apenas como fonte dos nomes de entidade/propriedade), construção
/// de uma consulta semântica EXPLÍCITA com filtros (distribuidoras Light/Enel RJ,
/// subgrupos B1/B2/B3, componente "TUSD Fio B" e unidade "R$/MWh") e a consulta
/// síncrona (/explore/querydata). Decodifica TODOS os segmentos DS (descriptor/DSR)
/// e devolve somente registros tipados e o hash SHA-256 do payload consultado.
/// Nenhuma etapa "finge sucesso": token ausente/inválido/expirado, comando do
/// visual não extraível, esquema divergente ou zero linhas produzem uma falha
/// tipada. Corpo de resposta, tokens e credenciais nunca aparecem em mensagens de
/// erro e nunca são retidos como exceções internas.
/// </summary>
public sealed class AneelTariffClient(HttpClient httpClient) : IAneelTariffSource
{
    private const string DistributorColumn = "PwrBI Todos_agentes_IdAgtMAX";
    private const string GroupColumn = "Componentes.Grupo";
    private const string SubgroupColumn = "Componentes.Subgrupo";
    private const string ModalityColumn = "Componentes.Modalidade";
    private const string PostColumn = "Componentes.Posto Tarifário";
    private const string ComponentColumn = "Componentes.Componente Tarifária";
    private const string UnitColumn = "Componentes.Unidade";
    private const string ValueColumn = "Componentes.Valor";
    private const string ResolutionColumn = "Componentes.Resolução";
    private const string ValidityStartColumn = "Componentes.Início Vigência";
    private const string ValidityEndColumn = "Componentes.Fim Vigência";

    private const string TargetComponent = "TUSD Fio B";
    private const string TargetUnit = "R$/MWh";

    /// <summary>
    /// Limite seguro de linhas DSR aceitas por consulta. O relatório consultado cobre
    /// apenas duas distribuidoras, três subgrupos e um componente; mesmo acumulando
    /// décadas de vigências o volume é ordens de grandeza menor. O limite evita que
    /// uma contagem malformada/excessiva em 'SH.R' provoque alocação descontrolada.
    /// </summary>
    public const int MaxRows = 100_000;

    private static readonly string[] ApprovedSubgroups = ["B1", "B2", "B3"];

    private static readonly string[] ReportLabels = AneelDistributors.All
        .Select(identity => identity.ReportLabel)
        .ToArray();

    private const string SemanticCommandName = "SemanticQueryDataShapeCommand";

    private static readonly string[] SelectColumns =
    [
        DistributorColumn,
        GroupColumn,
        SubgroupColumn,
        ModalityColumn,
        PostColumn,
        ComponentColumn,
        UnitColumn,
        ValueColumn,
        ResolutionColumn,
        ValidityStartColumn,
        ValidityEndColumn
    ];

    private static readonly HashSet<string> RequiredColumns = new(SelectColumns, StringComparer.Ordinal);

    private static readonly Regex AccessTokenPattern = new(
        "\"accessToken\"\\s*:\\s*\"([^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<AneelTariffFetchResult> FetchAsync(
        AneelTariffSyncOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.HttpTimeout);
        var ct = timeoutCts.Token;

        var portalHtml = await GetAsync(options.SourceUrl, null, ct);
        var embedToken = ExtractEmbedToken(portalHtml);

        var exploration = await GetAsync(
            $"{options.Cluster}/explore/reports/{options.ReportId}/modelsAndExploration?preferReadOnlySession=true",
            embedToken,
            ct);
        var visualCommandJson = ExtractVisualQueryCommand(exploration);

        await GetAsync(
            $"{options.Cluster}/explore/reports/{options.ReportId}/conceptualschema?userPreferredLocale=pt-BR",
            embedToken,
            ct);

        var queryBody = BuildQueryBody(visualCommandJson, options.ModelId);

        var raw = await PostAsync(
            $"{options.Cluster}/explore/querydata?synchronous=true",
            queryBody,
            embedToken,
            ct);

        var body = StripBomAndLeadingQuestionMark(raw);
        var records = DecodeComponentRows(body);

        return new AneelTariffFetchResult(
            records,
            ComputeHash(body),
            options.SourceUrl,
            DateTimeOffset.UtcNow);
    }

    private async Task<string> GetAsync(string url, string? embedToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (embedToken is not null)
            request.Headers.TryAddWithoutValidation("Authorization", $"EmbedToken {embedToken}");

        return await SendAsync(request, ct);
    }

    private async Task<string> PostAsync(string url, string body, string embedToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"EmbedToken {embedToken}");

        return await SendAsync(request, ct);
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new AneelTariffSourceException(
                    AneelErrorCode.Transport,
                    $"A fonte ANEEL respondeu com status HTTP {(int)response.StatusCode} ({response.StatusCode}).");
            }

            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (AneelTariffSourceException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            // Sanitiza a cadeia de exceção: a mensagem interna pode conter host/URL
            // e a resposta pode carregar tokens; nenhum deles é retido como InnerException.
            throw new AneelTariffSourceException(
                AneelErrorCode.Transport,
                "Falha de transporte ao consultar a fonte ANEEL.");
        }
    }

    private static string ExtractEmbedToken(string portalHtml)
    {
        if (string.IsNullOrWhiteSpace(portalHtml))
        {
            throw new AneelEmbedTokenException(
                AneelErrorCode.EmbedTokenMissing,
                "O portal ANEEL não retornou conteúdo para extrair o token de incorporação.");
        }

        var match = AccessTokenPattern.Match(portalHtml);
        if (!match.Success)
        {
            throw new AneelEmbedTokenException(
                AneelErrorCode.EmbedTokenMissing,
                "O portal ANEEL não expôs o campo accessToken da configuração de incorporação.");
        }

        var token = match.Groups[1].Value;
        ValidateEmbedToken(token);
        return token;
    }

    private static void ValidateEmbedToken(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3 || parts.Any(static part => string.IsNullOrWhiteSpace(part)))
        {
            throw new AneelEmbedTokenException(
                AneelErrorCode.EmbedTokenInvalid,
                "O token de incorporação do portal ANEEL não possui o formato JWT esperado.");
        }

        var expiry = ReadTokenExpiry(token);
        if (expiry is { } exp && exp <= DateTimeOffset.UtcNow)
        {
            throw new AneelEmbedTokenException(
                AneelErrorCode.EmbedTokenExpired,
                "O token de incorporação do portal ANEEL está expirado.");
        }
    }

    private static DateTimeOffset? ReadTokenExpiry(string token)
    {
        try
        {
            var payload = Base64UrlDecode(token.Split('.')[1]);
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("exp", out var exp)
                && exp.ValueKind == JsonValueKind.Number
                && exp.TryGetInt64(out var seconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            }
        }
        catch
        {
            // Payload não decodificável: validação de expiração indisponível;
            // a forma (3 segmentos) já foi validada acima.
        }

        return null;
    }

    private static string Base64UrlDecode(string input)
    {
        var normalized = input.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
    }

    private static string ExtractVisualQueryCommand(string exploration)
    {
        using var document = ParseDocument(exploration, "exploração do relatório Power BI");

        var command = FindCommand(document.RootElement);
        if (command is null)
        {
            throw new AneelVisualQueryException(
                AneelErrorCode.VisualQueryMissing,
                "Não foi possível extrair o comando de consulta do visual (SemanticQueryDataShapeCommand) do relatório Power BI.");
        }

        return command;
    }

    private static string? FindCommand(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals(SemanticCommandName) && property.Value.ValueKind == JsonValueKind.Object)
                        return property.Value.GetRawText();

                    if (FindCommand(property.Value) is { } found)
                        return found;
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (FindCommand(item) is { } found)
                        return found;
                }
                break;
        }

        return null;
    }

    private static string BuildQueryBody(string visualCommandJson, string modelId)
    {
        if (!long.TryParse(modelId, out var modelIdValue))
        {
            throw new AneelTariffSourceException(
                AneelErrorCode.InvalidModel,
                "O identificador de modelo (modelId) da fonte ANEEL é inválido.");
        }

        var command = BuildSemanticCommand(visualCommandJson);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("version", "1.0.0");
            writer.WriteNumber("modelId", modelIdValue);
            writer.WriteStartArray("queries");
            writer.WriteStartObject();
            writer.WriteStartObject("Query");
            writer.WriteStartArray("Commands");
            writer.WriteStartObject();
            writer.WritePropertyName(SemanticCommandName);
            command.WriteTo(writer);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static JsonObject BuildSemanticCommand(string visualCommandJson)
    {
        var visual = ParseJsonObject(visualCommandJson, "comando de consulta do visual");
        var source = ResolveSourceName(visual);

        var select = new JsonArray();
        foreach (var property in SelectColumns)
            select.Add(ColumnRef(source, property));

        var where = new JsonArray
        {
            InCondition(source, DistributorColumn, ReportLabels),
            InCondition(source, GroupColumn, ["B"]),
            InCondition(source, SubgroupColumn, ApprovedSubgroups),
            InCondition(source, ComponentColumn, [TargetComponent]),
            InCondition(source, UnitColumn, [TargetUnit])
        };

        return new JsonObject
        {
            ["Query"] = new JsonObject
            {
                ["From"] = ExtractFrom(visual),
                ["Select"] = select,
                ["Where"] = where
            }
        };
    }

    private static JsonObject ParseJsonObject(string json, string context)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidOperationException("não é um objeto JSON");
        }
        catch (JsonException)
        {
            throw new AneelTariffSchemaException(
                AneelErrorCode.InvalidPayload,
                $"O payload de {context} da fonte ANEEL não é um JSON válido.");
        }
        catch (InvalidOperationException)
        {
            throw new AneelTariffSchemaException(
                AneelErrorCode.InvalidPayload,
                $"O payload de {context} da fonte ANEEL não é um objeto JSON.");
        }
    }

    private static string ResolveSourceName(JsonObject command)
    {
        if (command.TryGetPropertyValue("Query", out var queryNode) && queryNode is JsonObject query
            && query.TryGetPropertyValue("From", out var fromNode) && fromNode is JsonArray from
            && from.Count > 0 && from[0] is JsonObject first
            && first.TryGetPropertyValue("Name", out var nameNode) && nameNode is JsonValue nameValue
            && nameValue.TryGetValue<string>(out var source) && !string.IsNullOrWhiteSpace(source))
        {
            return source;
        }

        return "c";
    }

    private static JsonArray ExtractFrom(JsonObject command)
    {
        if (command.TryGetPropertyValue("Query", out var queryNode) && queryNode is JsonObject query
            && query.TryGetPropertyValue("From", out var fromNode) && fromNode is JsonArray from)
        {
            return (JsonArray)from.DeepClone();
        }

        return [];
    }

    private static JsonObject ColumnRef(string source, string property) => new()
    {
        ["Column"] = new JsonObject
        {
            ["Expression"] = new JsonObject
            {
                ["SourceRef"] = new JsonObject { ["Source"] = source }
            },
            ["Property"] = property
        }
    };

    private static JsonObject InCondition(string source, string property, IReadOnlyList<string> values)
    {
        var valuesArray = new JsonArray();
        foreach (var value in values)
        {
            valuesArray.Add(new JsonArray(
                new JsonObject { ["Literal"] = new JsonObject { ["Value"] = $"'{value}'" } }));
        }

        return new JsonObject
        {
            ["Condition"] = new JsonObject
            {
                ["In"] = new JsonObject
                {
                    ["Expressions"] = new JsonArray(ColumnRef(source, property)),
                    ["Values"] = valuesArray
                }
            }
        };
    }

    private static string StripBomAndLeadingQuestionMark(string body)
    {
        var span = body.AsSpan();

        if (span.StartsWith("\uFEFF"))
            span = span[1..];

        while (span.Length > 0 && span[0] is '?' or ' ' or '\t' or '\r' or '\n')
            span = span[1..];

        return span.ToString();
    }

    private static IReadOnlyList<AneelTariffRecord> DecodeComponentRows(string body)
    {
        using var document = ParseDocument(body, "dados do relatório Power BI");
        var root = document.RootElement;

        var columnNames = ReadColumnNames(root);
        EnsureRequiredColumns(columnNames);

        var dsr = FindDsr(root);
        var segments = ReadSegments(dsr, columnNames.Count, out var totalRows);

        if (totalRows == 0)
            throw new AneelTariffSchemaException(AneelErrorCode.EmptyData, "A fonte ANEEL não retornou nenhuma linha de tarifa.");

        var records = new List<AneelTariffRecord>(totalRows);
        foreach (var segment in segments)
        {
            for (var row = 0; row < segment.RowCount; row++)
            {
                var values = new string?[columnNames.Count];
                for (var column = 0; column < columnNames.Count; column++)
                    values[column] = ResolveCell(segment, dsr, column, row);

                records.Add(MapRecord(columnNames, values));
            }
        }

        return records;
    }

    private static List<string> ReadColumnNames(JsonElement root)
    {
        if (!root.TryGetProperty("descriptor", out var descriptor)
            || !descriptor.TryGetProperty("Select", out var select)
            || select.ValueKind != JsonValueKind.Array)
            throw new AneelTariffSchemaException(
                AneelErrorCode.DsrInvalid,
                "A resposta da fonte ANEEL não contém o descritor de colunas ('descriptor.Select').");

        var names = new List<string>();
        foreach (var item in select.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("Column", out var column)
                || column.ValueKind != JsonValueKind.Object
                || !column.TryGetProperty("Property", out var property)
                || property.ValueKind != JsonValueKind.String)
                throw new AneelTariffSchemaException(
                    AneelErrorCode.DsrInvalid,
                    "Coluna do descritor da fonte ANEEL sem nome ('Column.Property').");

            names.Add(property.GetString()!);
        }

        return names;
    }

    private static void EnsureRequiredColumns(IReadOnlyList<string> names)
    {
        var present = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (var required in RequiredColumns)
        {
            if (!present.Contains(required))
            {
                throw new AneelTariffSchemaException(
                    AneelErrorCode.MissingColumns,
                    $"A fonte ANEEL não forneceu a coluna obrigatória '{required}'.");
            }
        }
    }

    private static JsonElement FindDsr(JsonElement root)
    {
        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var result in results.EnumerateArray())
            {
                if (result.TryGetProperty("result", out var inner)
                    && inner.TryGetProperty("data", out var data)
                    && data.TryGetProperty("dsr", out var dsr)
                    && dsr.ValueKind == JsonValueKind.Object)
                    return dsr;
            }
        }

        throw new AneelTariffSchemaException(
            AneelErrorCode.DsrInvalid,
            "A resposta da fonte ANEEL não contém o resultado de dados ('results[].result.data.dsr').");
    }

    private static IReadOnlyList<Segment> ReadSegments(JsonElement dsr, int columnCount, out int totalRows)
    {
        if (!dsr.TryGetProperty("DS", out var segments) || segments.ValueKind != JsonValueKind.Array || segments.GetArrayLength() == 0)
            throw new AneelTariffSchemaException(
                AneelErrorCode.DsrInvalid,
                "A resposta da fonte ANEEL não contém o segmento de dados ('DS').");

        var result = new List<Segment>(segments.GetArrayLength());
        totalRows = 0;

        foreach (var segment in segments.EnumerateArray())
        {
            if (!segment.TryGetProperty("SH", out var shape)
                || !shape.TryGetProperty("R", out var rowCountElement)
                || rowCountElement.ValueKind != JsonValueKind.Number)
                throw new AneelTariffSchemaException(
                    AneelErrorCode.DsrInvalid,
                    "O segmento de dados da fonte ANEEL não informa a contagem de linhas ('SH.R').");

            var rowCount = RequireInt32(rowCountElement, "SH.R", nonNegative: true);
            if (rowCount > MaxRows)
                throw new AneelTariffSchemaException(
                    AneelErrorCode.DsrInvalid,
                    "O segmento de dados da fonte ANEEL excede o número máximo seguro de linhas ('SH.R').");

            if (totalRows > MaxRows - rowCount)
                throw new AneelTariffSchemaException(
                    AneelErrorCode.DsrInvalid,
                    "O total de linhas da fonte ANEEL excede o número máximo seguro.");

            totalRows += rowCount;

            if (!segment.TryGetProperty("PH", out var projection) || projection.ValueKind != JsonValueKind.Array)
                throw new AneelTariffSchemaException(
                    AneelErrorCode.DsrInvalid,
                    "O segmento de dados da fonte ANEEL não informa a projeção de colunas ('PH').");

            var columnCountElement = shape.TryGetProperty("C", out var c) && c.ValueKind == JsonValueKind.Number
                ? RequireInt32(c, "SH.C", nonNegative: true)
                : -1;

            if (columnCountElement >= 0 && columnCountElement != columnCount)
                throw new AneelTariffSchemaException(
                    AneelErrorCode.DsrInvalid,
                    "O segmento de dados da fonte ANEEL informa uma contagem de colunas ('SH.C') divergente do descritor.");

            if (projection.GetArrayLength() != columnCount)
                throw new AneelTariffSchemaException(
                    AneelErrorCode.DsrInvalid,
                    "A projeção ('PH') do segmento da fonte ANEEL diverge da contagem de colunas do descritor.");

            var dictionarySlots = new int[projection.GetArrayLength()];
            var index = 0;
            foreach (var header in projection.EnumerateArray())
            {
                if (!header.TryGetProperty("DM", out var dm) || dm.ValueKind != JsonValueKind.Number)
                    throw new AneelTariffSchemaException(
                        AneelErrorCode.DsrInvalid,
                        "A projeção da fonte ANEEL não informa o índice de dicionário ('DM').");

                dictionarySlots[index++] = RequireInt32(dm, "PH.DM");
            }

            result.Add(new Segment(segment, rowCount, dictionarySlots));
        }

        return result;
    }

    private static string? ResolveCell(Segment segment, JsonElement dsr, int column, int row)
    {
        var dictionarySlot = segment.DictionarySlots[column];
        var groupKey = $"D{dictionarySlot}";

        if (!segment.Element.TryGetProperty(groupKey, out var indexColumn) || indexColumn.ValueKind != JsonValueKind.Array)
            throw new AneelTariffSchemaException(
                AneelErrorCode.DsrInvalid,
                $"O segmento da fonte ANEEL não fornece a coluna de índices '{groupKey}' esperada pela projeção.");

        if (row >= indexColumn.GetArrayLength())
            throw new AneelTariffSchemaException(
                AneelErrorCode.DsrInvalid,
                "O segmento da fonte ANEEL contém menos linhas do que a contagem declarada em 'SH.R'.");

        var indexElement = indexColumn[row];
        if (indexElement.ValueKind != JsonValueKind.Number)
            throw new AneelTariffSchemaException(
                AneelErrorCode.DsrInvalid,
                "Célula da fonte ANEEL sem índice numérico de dicionário.");

        var dictionaryIndex = RequireInt32(indexElement, "índice de dicionário");
        if (dictionaryIndex < 0)
            return null;

        if (!dsr.TryGetProperty("ValueDicts", out var valueDicts)
            || !valueDicts.TryGetProperty(groupKey, out var dictionary)
            || dictionary.ValueKind != JsonValueKind.Array)
            throw new AneelTariffSchemaException(
                AneelErrorCode.DsrInvalid,
                $"A resposta da fonte ANEEL não fornece o dicionário '{groupKey}'.");

        if (dictionaryIndex >= dictionary.GetArrayLength())
            throw new AneelTariffSchemaException(
                AneelErrorCode.DsrInvalid,
                "Índice de dicionário fora do intervalo na resposta da fonte ANEEL.");

        var value = dictionary[dictionaryIndex];
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int RequireInt32(JsonElement element, string field, bool nonNegative = false)
    {
        if (element.ValueKind != JsonValueKind.Number
            || !element.TryGetInt32(out var value)
            || (nonNegative && value < 0))
        {
            throw new AneelTariffSchemaException(
                AneelErrorCode.DsrInvalid,
                $"O campo '{field}' da fonte ANEEL não é um inteiro válido.");
        }

        return value;
    }

    private static AneelTariffRecord MapRecord(IReadOnlyList<string> columnNames, string?[] values)
    {
        string? Get(string column)
        {
            for (var index = 0; index < columnNames.Count; index++)
            {
                if (string.Equals(columnNames[index], column, StringComparison.Ordinal))
                    return values[index];
            }

            return null;
        }

        return new AneelTariffRecord(
            Get(DistributorColumn),
            Get(GroupColumn),
            Get(SubgroupColumn),
            Get(ModalityColumn),
            Get(PostColumn),
            Get(ComponentColumn),
            Get(UnitColumn),
            Get(ValueColumn),
            Get(ResolutionColumn),
            Get(ValidityStartColumn),
            Get(ValidityEndColumn),
            null);
    }

    private static JsonDocument ParseDocument(string body, string context)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            // Sanitiza a cadeia de exceção: a mensagem do parser pode conter trechos
            // do payload; nada é retido como InnerException.
            throw new AneelTariffSchemaException(
                AneelErrorCode.InvalidPayload,
                $"O payload de {context} da fonte ANEEL não é um JSON válido.");
        }
    }

    private static string ComputeHash(string body)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record Segment(JsonElement Element, int RowCount, int[] DictionarySlots);
}
