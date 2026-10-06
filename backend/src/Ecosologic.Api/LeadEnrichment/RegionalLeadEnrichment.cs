using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Ecosologic.Api.LeadEnrichment;

public sealed class RegionalLeadEnrichmentOptions
{
    public string WebhookUrl { get; set; } = "";
    public string WebhookToken { get; set; } = "";
}

public sealed record RegionalLeadEnrichmentRequest(IReadOnlyList<string> Cities, int MaxLeadsPerCity = 10);
public sealed record RegionalLeadEnrichmentResponse(string Status, int EnrichedLeads, string Message);

public interface IRegionalLeadEnrichmentClient
{
    Task<RegionalLeadEnrichmentResponse> RunAsync(RegionalLeadEnrichmentRequest request, CancellationToken cancellationToken);
}

public sealed class RegionalLeadEnrichmentClient(
    HttpClient httpClient,
    IOptions<RegionalLeadEnrichmentOptions> options) : IRegionalLeadEnrichmentClient
{
    public async Task<RegionalLeadEnrichmentResponse> RunAsync(RegionalLeadEnrichmentRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.WebhookUrl) || string.IsNullOrWhiteSpace(settings.WebhookToken))
            throw new InvalidOperationException("A prospecção regional não está configurada.");

        using var message = new HttpRequestMessage(HttpMethod.Post, settings.WebhookUrl)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Regional-Lead-Token", settings.WebhookToken);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"O workflow regional retornou {(int)response.StatusCode}.");

        return await response.Content.ReadFromJsonAsync<RegionalLeadEnrichmentResponse>(cancellationToken)
            ?? throw new InvalidOperationException("O workflow regional retornou uma resposta vazia.");
    }
}
