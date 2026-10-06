using Ecosologic.Application.Solar;

namespace Ecosologic.Api.Tests;

public sealed class ProposalRendererTests
{
    [Fact]
    public void Render_returns_deterministic_pdf_bytes_and_sha256()
    {
        var renderer = new ProposalRenderer();

        var first = renderer.Render("{\"title\":\"Proposta Ecosologic\"}", "proposal-1.0");
        var second = renderer.Render("{\"title\":\"Proposta Ecosologic\"}", "proposal-1.0");

        Assert.Equal("%PDF-1.4", System.Text.Encoding.ASCII.GetString(first.Content, 0, 8));
        Assert.Equal(first.Content, second.Content);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(64, first.Sha256.Length);
        Assert.Equal("proposal-1.0", first.TemplateVersion);
    }

    [Fact]
    public void Render_rejects_invalid_payload()
    {
        Assert.Throws<ArgumentException>(() => new ProposalRenderer().Render("invalid", "proposal-1.0"));
    }
}
