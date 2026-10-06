using Ecosologic.Api.Controllers;
using Ecosologic.Api.LeadEnrichment;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ecosologic.Api.Tests;

public sealed class RegionalLeadEnrichmentTests
{
    [Fact]
    public async Task Rejects_more_than_twenty_cities()
    {
        var client = new FakeRegionalLeadEnrichmentClient();
        var controller = new RegionalLeadEnrichmentController(client);

        var result = await controller.Run(
            new RegionalLeadEnrichmentRequest(Enumerable.Range(1, 21).Select(i => $"City {i}").ToArray(), 10),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(client.Called);
    }

    [Fact]
    public async Task Forwards_valid_request_to_the_regional_workflow()
    {
        var client = new FakeRegionalLeadEnrichmentClient();
        var controller = new RegionalLeadEnrichmentController(client);

        var result = await controller.Run(
            new RegionalLeadEnrichmentRequest(["Marica", "Macae"], 10),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(["Marica", "Macae"], client.Request!.Cities);
        Assert.Equal(10, client.Request.MaxLeadsPerCity);
    }

    private sealed class FakeRegionalLeadEnrichmentClient : IRegionalLeadEnrichmentClient
    {
        public bool Called { get; private set; }
        public RegionalLeadEnrichmentRequest? Request { get; private set; }

        public Task<RegionalLeadEnrichmentResponse> RunAsync(RegionalLeadEnrichmentRequest request, CancellationToken cancellationToken)
        {
            Called = true;
            Request = request;
            return Task.FromResult(new RegionalLeadEnrichmentResponse("completed", 0, "ok"));
        }
    }
}
