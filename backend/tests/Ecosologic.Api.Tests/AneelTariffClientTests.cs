using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ecosologic.Application.Solar;
using Ecosologic.Domain.Solar;
using Ecosologic.Infrastructure.Solar;

namespace Ecosologic.Api.Tests;

public class AneelTariffClientTests
{
    private const string PortalUrl = "https://portalrelatorios.aneel.gov.br/luznatarifa/basestarifas";
    private const string Cluster = "https://wabi-south-central-us-redirect.analysis.windows.net";
    private const string ReportId = "df28bff5-d988-496c-93e1-d154b8b172fc";
    private const string ModelId = "5039649";

    private const string PortalHtml = """
        <!doctype html><html><head>
        <script>window.__embedConfig = {"accessToken":"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhbmVlbC10ZXN0In0.test_signature"};</script>
        </head><body></body></html>
        """;

    private const string EmbedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhbmVlbC10ZXN0In0.test_signature";

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }

    private static HttpClient Client(StubHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://example.invalid/") };

    private static HttpResponseMessage Json(string content) =>
        new(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    private static string ReadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        return File.ReadAllText(path);
    }

    private static string ToBase64Url(string raw) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static AneelTariffSyncOptions Options(
        string? sourceUrl = null,
        string cluster = Cluster,
        string reportId = ReportId,
        string modelId = ModelId) =>
        new(sourceUrl ?? PortalUrl, cluster: cluster, reportId: reportId, modelId: modelId);

    private static StubHandler FullFlowHandler(List<HttpRequestMessage> captured)
    {
        return new StubHandler((request, _) =>
        {
            captured.Add(request);
            var path = request.RequestUri!.AbsolutePath;
            var query = request.RequestUri.Query;

            if (request.Method == HttpMethod.Get && path.EndsWith("/basestarifas"))
                return Task.FromResult(Json(PortalHtml));

            if (request.Method == HttpMethod.Get && path.Contains("/modelsAndExploration"))
                return Task.FromResult(Json(ReadFixture("aneel-models-and-exploration.json")));

            if (request.Method == HttpMethod.Get && path.Contains("/conceptualschema"))
                return Task.FromResult(Json(ReadFixture("aneel-conceptualschema.json")));

            if (request.Method == HttpMethod.Post && path.Contains("/querydata") && query.Contains("synchronous=true"))
                return Task.FromResult(Json(ReadFixture("aneel-querydata-response.json")));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
    }

    [Fact]
    public async Task Fetch_extracts_embed_token_and_uses_EmbedToken_authorization()
    {
        var captured = new List<HttpRequestMessage>();
        var client = new AneelTariffClient(Client(FullFlowHandler(captured)));

        await client.FetchAsync(Options(), CancellationToken.None);

        var authorized = captured.Where(r => r.RequestUri!.AbsolutePath.Contains("/explore/")).ToList();
        Assert.NotEmpty(authorized);
        Assert.All(authorized, request =>
            Assert.Equal($"EmbedToken {EmbedToken}", request.Headers.Authorization!.ToString()));
    }

    [Fact]
    public async Task Fetch_hits_portal_bootstrap_and_query_endpoints()
    {
        var captured = new List<HttpRequestMessage>();
        var client = new AneelTariffClient(Client(FullFlowHandler(captured)));

        await client.FetchAsync(Options(), CancellationToken.None);

        Assert.Contains(captured, r => r.RequestUri!.AbsolutePath.EndsWith("/basestarifas"));
        Assert.Contains(captured, r =>
            r.Method == HttpMethod.Get
            && r.RequestUri!.AbsolutePath.Contains($"/explore/reports/{ReportId}/modelsAndExploration"));
        Assert.Contains(captured, r =>
            r.Method == HttpMethod.Get
            && r.RequestUri!.AbsolutePath.Contains($"/explore/reports/{ReportId}/conceptualschema"));
        Assert.Contains(captured, r =>
            r.Method == HttpMethod.Post
            && r.RequestUri!.AbsolutePath.Contains("/explore/querydata")
            && r.RequestUri.Query.Contains("synchronous=true"));
    }

    [Fact]
    public async Task Fetch_decodes_dictionary_compressed_component_rows()
    {
        var client = new AneelTariffClient(Client(FullFlowHandler([])));

        var result = await client.FetchAsync(Options(), CancellationToken.None);

        Assert.Equal(6, result.Records.Count);
        Assert.Equal("Light", result.Records[0].DistributorName);
        Assert.Equal("B", result.Records[0].Group);
        Assert.Equal("B1", result.Records[0].Subgroup);
        Assert.Equal("Convencional", result.Records[0].Modality);
        Assert.Equal("Único", result.Records[0].Post);
        Assert.Equal("TUSD Fio B", result.Records[0].Component);
        Assert.Equal("R$/MWh", result.Records[0].Unit);
        Assert.Equal("41.20", result.Records[0].Value);
        Assert.Equal("RES 3.242/2024", result.Records[0].ResolutionCode);
        Assert.Equal("2024-01-01", result.Records[0].ValidityStart);
        Assert.Equal("2024-12-31", result.Records[0].ValidityEnd);

        Assert.Equal("Enel RJ", result.Records[3].DistributorName);
        Assert.Equal("B1", result.Records[3].Subgroup);
        Assert.Equal("Enel RJ", result.Records[5].DistributorName);
        Assert.Equal("B3", result.Records[5].Subgroup);
    }

    [Fact]
    public async Task Fetch_builds_semantic_query_with_explicit_filters()
    {
        string? queryBody = null;
        var handler = new StubHandler(async (request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/basestarifas"))
                return Json(PortalHtml);
            if (path.Contains("/modelsAndExploration"))
                return Json(ReadFixture("aneel-models-and-exploration.json"));
            if (path.Contains("/conceptualschema"))
                return Json(ReadFixture("aneel-conceptualschema.json"));
            if (path.Contains("/querydata"))
            {
                queryBody = await request.Content!.ReadAsStringAsync();
                return Json(ReadFixture("aneel-querydata-response.json"));
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new AneelTariffClient(Client(handler));
        await client.FetchAsync(Options(), CancellationToken.None);

        Assert.NotNull(queryBody);

        using var bodyDoc = JsonDocument.Parse(queryBody!);
        var command = bodyDoc.RootElement
            .GetProperty("queries")[0]
            .GetProperty("Query")
            .GetProperty("Commands")[0]
            .GetProperty("SemanticQueryDataShapeCommand")
            .GetProperty("Query");

        Assert.True(command.TryGetProperty("Where", out var where));
        Assert.True(command.TryGetProperty("Select", out var select));

        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in select.EnumerateArray())
            selected.Add(item.GetProperty("Column").GetProperty("Property").GetString()!);

        foreach (var column in new[]
                 {
                     "PwrBI Todos_agentes_IdAgtMAX",
                     "Componentes.Grupo",
                     "Componentes.Subgrupo",
                     "Componentes.Modalidade",
                     "Componentes.Posto Tarifário",
                     "Componentes.Componente Tarifária",
                     "Componentes.Unidade",
                     "Componentes.Valor",
                     "Componentes.Resolução",
                     "Componentes.Início Vigência",
                     "Componentes.Fim Vigência"
                 })
        {
            Assert.Contains(column, selected);
        }

        var whereText = where.GetRawText();
        Assert.Contains("Light", whereText);
        Assert.Contains("Enel RJ", whereText);
        Assert.Contains("Componentes.Grupo", whereText);
        Assert.Contains("B1", whereText);
        Assert.Contains("B2", whereText);
        Assert.Contains("B3", whereText);
        Assert.Contains("TUSD Fio B", whereText);
        Assert.Contains("R$/MWh", whereText);
        Assert.DoesNotContain("Light Serviços de Eletricidade S.A.", whereText);
        Assert.DoesNotContain("Enel Distribuição Rio", whereText);

        Assert.Equal("'B'", FindFilterValue(where, "Componentes.Grupo"));
    }

    [Fact]
    public async Task Fetch_then_normalize_produces_complete_profiles()
    {
        var client = new AneelTariffClient(Client(FullFlowHandler([])));

        var fetched = await client.FetchAsync(Options(), CancellationToken.None);

        var normalizer = new AneelTariffNormalizer();
        var profiles = normalizer.Normalize(fetched.Records, fetched.SourceHash, Options());

        Assert.Equal(6, profiles.Count);
        Assert.Equal(
            [Distributor.Light, Distributor.EnelRio],
            profiles.Select(p => p.Distributor).Distinct().OrderBy(d => d).ToArray());
        Assert.All(profiles, p => Assert.Equal(TariffGroup.B, p.Group));
        Assert.All(profiles, p => Assert.All(p.Components, c => Assert.Equal(TariffComponentKind.FIO_B, c.Kind)));
        Assert.All(profiles, p => Assert.All(p.Components, c => Assert.Equal("R$/MWh", c.SourceUnit)));
    }

    [Fact]
    public async Task Fetch_returns_deterministic_sha256_hash()
    {
        var client = new AneelTariffClient(Client(FullFlowHandler([])));

        var first = await client.FetchAsync(Options(), CancellationToken.None);
        var second = await client.FetchAsync(Options(), CancellationToken.None);

        Assert.Equal(64, first.SourceHash.Length);
        Assert.Equal(first.SourceHash, second.SourceHash);
    }

    [Fact]
    public async Task Fetch_strips_utf8_bom_and_leading_question_mark()
    {
        var handler = new StubHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/basestarifas"))
                return Task.FromResult(Json(PortalHtml));
            if (path.Contains("/modelsAndExploration"))
                return Task.FromResult(Json(ReadFixture("aneel-models-and-exploration.json")));
            if (path.Contains("/conceptualschema"))
                return Task.FromResult(Json(ReadFixture("aneel-conceptualschema.json")));
            if (path.Contains("/querydata"))
            {
                var body = "\uFEFF?" + ReadFixture("aneel-querydata-response.json");
                return Task.FromResult(Json(body));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var client = new AneelTariffClient(Client(handler));
        var result = await client.FetchAsync(Options(), CancellationToken.None);

        Assert.Equal(6, result.Records.Count);
    }

    [Fact]
    public async Task Fetch_throws_typed_failure_when_portal_exposes_no_token()
    {
        var handler = new StubHandler((request, _) =>
            Task.FromResult(Json("<html>sem token</html>")));
        var client = new AneelTariffClient(Client(handler));

        await Assert.ThrowsAsync<AneelEmbedTokenException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_extracts_configured_accessToken_not_first_jwt_shaped_string()
    {
        const string decoy = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJkZWNveSJ9.decoy_signature";
        var html = $$"""
            <!doctype html><html><head>
            <script>window.__embedConfig = {"sessionId":"{{decoy}}","accessToken":"{{EmbedToken}}"};</script>
            </head><body></body></html>
            """;

        var captured = new List<HttpRequestMessage>();
        var handler = new StubHandler((request, _) =>
        {
            captured.Add(request);
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/basestarifas"))
                return Task.FromResult(Json(html));
            if (path.Contains("/modelsAndExploration"))
                return Task.FromResult(Json(ReadFixture("aneel-models-and-exploration.json")));
            if (path.Contains("/conceptualschema"))
                return Task.FromResult(Json(ReadFixture("aneel-conceptualschema.json")));
            if (path.Contains("/querydata"))
                return Task.FromResult(Json(ReadFixture("aneel-querydata-response.json")));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var client = new AneelTariffClient(Client(handler));
        await client.FetchAsync(Options(), CancellationToken.None);

        var authorized = captured.Where(r => r.RequestUri!.AbsolutePath.Contains("/explore/")).ToList();
        Assert.NotEmpty(authorized);
        Assert.All(authorized, request =>
            Assert.Equal($"EmbedToken {EmbedToken}", request.Headers.Authorization!.ToString()));
    }

    [Fact]
    public async Task Fetch_rejects_malformed_embed_token()
    {
        var html = """<script>window.__embedConfig = {"accessToken":"not-a-jwt"};</script>""";
        var handler = new StubHandler((_, _) => Task.FromResult(Json(html)));
        var client = new AneelTariffClient(Client(handler));

        await Assert.ThrowsAsync<AneelEmbedTokenException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_rejects_expired_embed_token()
    {
        var header = ToBase64Url("""{"alg":"none"}""");
        var payload = ToBase64Url("""{"sub":"aneel-test","exp":1}""");
        var expired = $"{header}.{payload}.signature";

        var html = $$"""<script>window.__embedConfig = {"accessToken":"{{expired}}"};</script>""";
        var handler = new StubHandler((_, _) => Task.FromResult(Json(html)));
        var client = new AneelTariffClient(Client(handler));

        var exception = await Assert.ThrowsAsync<AneelEmbedTokenException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));

        Assert.Equal(AneelErrorCode.EmbedTokenExpired, exception.Code);
        Assert.DoesNotContain(expired, exception.Message);
    }

    [Fact]
    public async Task Fetch_throws_typed_failure_when_visual_query_cannot_be_extracted()
    {
        var handler = new StubHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/basestarifas"))
                return Task.FromResult(Json(PortalHtml));
            if (path.Contains("/modelsAndExploration"))
                return Task.FromResult(Json("{\"exploration\":{\"sections\":[]}}"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var client = new AneelTariffClient(Client(handler));

        await Assert.ThrowsAsync<AneelVisualQueryException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_throws_typed_failure_on_empty_rows()
    {
        var empty = JsonNode.Parse(ReadFixture("aneel-querydata-response.json"))!;
        empty["results"]![0]!["result"]!["data"]!["dsr"]!["DS"]![0]!["SH"] = JsonNode.Parse("""{"R":0,"C":11}""");

        var handler = RespondWith(empty.ToJsonString());
        var client = new AneelTariffClient(Client(handler));

        await Assert.ThrowsAsync<AneelTariffSchemaException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_throws_typed_failure_on_missing_required_column()
    {
        const string missing = """
            {"descriptor":{"Select":[{"Column":{"Property":"PwrBI Todos_agentes_IdAgtMAX"}},{"Column":{"Property":"Componentes.Subgrupo"}}]},"results":[{"result":{"data":{"dsr":{"DS":[{"PH":[{"DM":0},{"DM":1}],"SH":{"R":1,"C":2},"D0":[0],"D1":[0]}],"ValueDicts":{"D0":["Light"],"D1":["B1"]}}}}}]}
            """;
        var handler = RespondWith(missing);
        var client = new AneelTariffClient(Client(handler));

        await Assert.ThrowsAsync<AneelTariffSchemaException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_throws_typed_failure_on_column_count_mismatch()
    {
        var mismatched = JsonNode.Parse(ReadFixture("aneel-querydata-response.json"))!;
        mismatched["results"]![0]!["result"]!["data"]!["dsr"]!["DS"]![0]!["SH"] = JsonNode.Parse("""{"R":6,"C":7}""");

        var handler = RespondWith(mismatched.ToJsonString());
        var client = new AneelTariffClient(Client(handler));

        await Assert.ThrowsAsync<AneelTariffSchemaException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_throws_typed_failure_when_row_count_is_not_an_integer()
    {
        var malformed = JsonNode.Parse(ReadFixture("aneel-querydata-response.json"))!;
        malformed["results"]![0]!["result"]!["data"]!["dsr"]!["DS"]![0]!["SH"] = JsonNode.Parse("""{"R":6.5,"C":11}""");

        var handler = RespondWith(malformed.ToJsonString());
        var client = new AneelTariffClient(Client(handler));

        var exception = await Assert.ThrowsAsync<AneelTariffSchemaException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));

        Assert.Equal(AneelErrorCode.DsrInvalid, exception.Code);
    }

    [Fact]
    public async Task Fetch_throws_typed_failure_when_column_count_is_not_an_integer()
    {
        var malformed = JsonNode.Parse(ReadFixture("aneel-querydata-response.json"))!;
        malformed["results"]![0]!["result"]!["data"]!["dsr"]!["DS"]![0]!["SH"] = JsonNode.Parse("""{"R":6,"C":11.5}""");

        var handler = RespondWith(malformed.ToJsonString());
        var client = new AneelTariffClient(Client(handler));

        var exception = await Assert.ThrowsAsync<AneelTariffSchemaException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));

        Assert.Equal(AneelErrorCode.DsrInvalid, exception.Code);
    }

    [Fact]
    public async Task Fetch_rejects_row_count_above_safe_maximum()
    {
        var excessive = JsonNode.Parse(ReadFixture("aneel-querydata-response.json"))!;
        excessive["results"]![0]!["result"]!["data"]!["dsr"]!["DS"]![0]!["SH"] =
            JsonNode.Parse($$"""{"R":{{AneelTariffClient.MaxRows + 1}},"C":11}""");

        var handler = RespondWith(excessive.ToJsonString());
        var client = new AneelTariffClient(Client(handler));

        var exception = await Assert.ThrowsAsync<AneelTariffSchemaException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));

        Assert.Equal(AneelErrorCode.DsrInvalid, exception.Code);
    }

    [Fact]
    public async Task Fetch_rejects_accumulated_row_count_above_safe_maximum()
    {
        var node = JsonNode.Parse(ReadFixture("aneel-querydata-response.json"))!;
        var ds = node["results"]![0]!["result"]!["data"]!["dsr"]!["DS"]!.AsArray();
        var original = ds[0]!.DeepClone();
        ds.Add(original.DeepClone());
        ds[0]!["SH"] = JsonNode.Parse($$"""{"R":{{AneelTariffClient.MaxRows}},"C":11}""");
        ds[1]!["SH"] = JsonNode.Parse($$"""{"R":{{AneelTariffClient.MaxRows}},"C":11}""");

        var handler = RespondWith(node.ToJsonString());
        var client = new AneelTariffClient(Client(handler));

        var exception = await Assert.ThrowsAsync<AneelTariffSchemaException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));

        Assert.Equal(AneelErrorCode.DsrInvalid, exception.Code);
    }

    [Fact]
    public async Task Fetch_throws_typed_failure_when_projection_dictionary_index_is_not_an_integer()
    {
        var malformed = JsonNode.Parse(ReadFixture("aneel-querydata-response.json"))!;
        malformed["results"]![0]!["result"]!["data"]!["dsr"]!["DS"]![0]!["PH"]![0]!["DM"] = 0.5;

        var handler = RespondWith(malformed.ToJsonString());
        var client = new AneelTariffClient(Client(handler));

        var exception = await Assert.ThrowsAsync<AneelTariffSchemaException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));

        Assert.Equal(AneelErrorCode.DsrInvalid, exception.Code);
    }

    [Fact]
    public async Task Fetch_throws_typed_failure_when_cell_dictionary_index_is_not_an_integer()
    {
        var malformed = JsonNode.Parse(ReadFixture("aneel-querydata-response.json"))!;
        malformed["results"]![0]!["result"]!["data"]!["dsr"]!["DS"]![0]!["D0"]![0] = 0.5;

        var handler = RespondWith(malformed.ToJsonString());
        var client = new AneelTariffClient(Client(handler));

        var exception = await Assert.ThrowsAsync<AneelTariffSchemaException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));

        Assert.Equal(AneelErrorCode.DsrInvalid, exception.Code);
    }

    [Fact]
    public async Task Fetch_throws_typed_failure_when_segment_missing_dictionary_column()
    {
        var sparse = JsonNode.Parse(ReadFixture("aneel-querydata-response.json"))!;
        var segment = sparse["results"]![0]!["result"]!["data"]!["dsr"]!["DS"]![0]!.AsObject();
        segment.Remove("D7");

        var handler = RespondWith(sparse.ToJsonString());
        var client = new AneelTariffClient(Client(handler));

        await Assert.ThrowsAsync<AneelTariffSchemaException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_decodes_all_ds_segments()
    {
        var node = JsonNode.Parse(ReadFixture("aneel-querydata-response.json"))!;
        var source = node["results"]![0]!["result"]!["data"]!["dsr"]!["DS"]![0]!;

        var first = SplitSegment(source, 0, 3);
        var second = SplitSegment(source, 3, 6);

        var ds = node["results"]![0]!["result"]!["data"]!["dsr"]!["DS"]!.AsArray();
        ds.Clear();
        ds.Add(first);
        ds.Add(second);

        var handler = RespondWith(node.ToJsonString());
        var client = new AneelTariffClient(Client(handler));

        var result = await client.FetchAsync(Options(), CancellationToken.None);

        Assert.Equal(6, result.Records.Count);
        Assert.Equal("Light", result.Records[0].DistributorName);
        Assert.Equal("Enel RJ", result.Records[3].DistributorName);
        Assert.Equal("B3", result.Records[5].Subgroup);
    }

    private static JsonObject SplitSegment(JsonNode source, int fromRow, int toRow)
    {
        var segment = new JsonObject
        {
            ["SH"] = JsonNode.Parse($$"""{"R":{{toRow - fromRow}},"C":11}""")!
        };

        var ph = new JsonArray();
        for (var i = 0; i < 11; i++)
            ph.Add(new JsonObject { ["DM"] = i });
        segment["PH"] = ph;

        for (var i = 0; i < 11; i++)
        {
            var column = source[$"D{i}"]!.AsArray();
            var slice = new JsonArray();
            for (var row = fromRow; row < toRow; row++)
                slice.Add(column[row]!.DeepClone());
            segment[$"D{i}"] = slice;
        }

        return segment;
    }

    private static string? FindFilterValue(JsonElement where, string columnProperty)
    {
        foreach (var condition in where.EnumerateArray())
        {
            var inNode = condition.GetProperty("Condition").GetProperty("In");
            var expressions = inNode.GetProperty("Expressions");

            var matchesColumn = false;
            foreach (var expression in expressions.EnumerateArray())
            {
                if (expression.GetProperty("Column").GetProperty("Property").GetString() == columnProperty)
                    matchesColumn = true;
            }

            if (!matchesColumn)
                continue;

            foreach (var row in inNode.GetProperty("Values").EnumerateArray())
            {
                foreach (var literal in row.EnumerateArray())
                    return literal.GetProperty("Literal").GetProperty("Value").GetString();
            }
        }

        return null;
    }

    private static StubHandler RespondWith(string queryDataBody)
    {
        return new StubHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/basestarifas"))
                return Task.FromResult(Json(PortalHtml));
            if (path.Contains("/modelsAndExploration"))
                return Task.FromResult(Json(ReadFixture("aneel-models-and-exploration.json")));
            if (path.Contains("/conceptualschema"))
                return Task.FromResult(Json(ReadFixture("aneel-conceptualschema.json")));
            if (path.Contains("/querydata"))
                return Task.FromResult(Json(queryDataBody));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
    }

    [Fact]
    public async Task Fetch_throws_on_non_success_status()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var client = new AneelTariffClient(Client(handler));

        var exception = await Assert.ThrowsAsync<AneelTariffSourceException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));

        Assert.Contains("500", exception.Message);
    }

    [Fact]
    public async Task Fetch_error_does_not_leak_token_from_response_body()
    {
        const string token = "SUPER_SECRET_TOKEN";
        var handler = new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent($"{{\"error\":\"{token}\"}}", Encoding.UTF8, "application/json")
            }));
        var client = new AneelTariffClient(Client(handler));

        var exception = await Assert.ThrowsAsync<AneelTariffSourceException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));

        Assert.DoesNotContain(token, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task Fetch_sanitizes_http_client_transport_exception()
    {
        var handler = new StubHandler((_, _) =>
            throw new HttpRequestException("boom"));
        var client = new AneelTariffClient(Client(handler));

        var exception = await Assert.ThrowsAsync<AneelTariffSourceException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));

        Assert.DoesNotContain("boom", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Equal(AneelErrorCode.Transport, exception.Code);
    }

    [Fact]
    public async Task Fetch_parse_error_has_no_inner_exception()
    {
        var handler = new StubHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/basestarifas"))
                return Task.FromResult(Json(PortalHtml));
            if (path.Contains("/modelsAndExploration"))
                return Task.FromResult(Json(ReadFixture("aneel-models-and-exploration.json")));
            if (path.Contains("/conceptualschema"))
                return Task.FromResult(Json(ReadFixture("aneel-conceptualschema.json")));
            if (path.Contains("/querydata"))
                return Task.FromResult(Json("{not valid json"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var client = new AneelTariffClient(Client(handler));

        var exception = await Assert.ThrowsAsync<AneelTariffSchemaException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));

        Assert.Null(exception.InnerException);
        Assert.Equal(AneelErrorCode.InvalidPayload, exception.Code);
    }

    [Fact]
    public async Task Fetch_does_not_retry_on_transport_failure()
    {
        var calls = 0;
        var handler = new StubHandler((_, _) =>
        {
            calls++;
            throw new HttpRequestException("boom");
        });
        var client = new AneelTariffClient(Client(handler));

        await Assert.ThrowsAsync<AneelTariffSourceException>(() =>
            client.FetchAsync(Options(), CancellationToken.None));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Options_does_not_expose_client_retry_count() =>
        Assert.DoesNotContain(typeof(AneelTariffSyncOptions).GetProperties(), p => p.Name == "MaxRetries");

    [Fact]
    public async Task Fetch_honors_configured_timeout()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Json("{}");
        });
        var client = new AneelTariffClient(Client(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.FetchAsync(
                new AneelTariffSyncOptions(PortalUrl, httpTimeout: TimeSpan.FromMilliseconds(50)),
                CancellationToken.None));
    }
}
