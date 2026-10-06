using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Ecosologic.Api.Suppliers;

public sealed class SolarSupplierDiscoveryOptions
{
    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
}

public sealed record DiscoveredSupplier(string Name, string? Website, string? Contact, string Source);

public sealed class SolarSupplierDiscovery(HttpClient http, IOptions<SolarSupplierDiscoveryOptions> options)
{
    public async Task<IReadOnlyList<DiscoveredSupplier>> Search(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Endpoint))
            throw new InvalidOperationException("Descoberta de fornecedores não está configurada.");

        using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.Endpoint)
        {
            Content = JsonContent.Create(new { query, country = "BR", category = "solar supplier", responseFormat = "json" })
        };
        if (!string.IsNullOrWhiteSpace(options.Value.ApiKey))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Value.ApiKey);

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DiscoveryResponse>(cancellationToken);
        return result?.Suppliers ?? [];
    }

    private sealed record DiscoveryResponse(IReadOnlyList<DiscoveredSupplier> Suppliers);
}
