using Ecosologic.Domain.Crm;

namespace Ecosologic.Domain.Tests;

public class LeadStageParserTests
{
    [Fact]
    public void TryParseStage_accepts_valid_stage_names()
    {
        Assert.True(Lead.TryParseStage("Contacted", out var stage));
        Assert.Equal(LeadStage.Contacted, stage);
    }

    [Fact]
    public void TryParseStage_is_case_insensitive()
    {
        Assert.True(Lead.TryParseStage("won", out var stage));
        Assert.Equal(LeadStage.Won, stage);
    }

    [Fact]
    public void TryParseStage_rejects_invalid_stage()
    {
        Assert.False(Lead.TryParseStage("Bogus", out _));
    }

    [Fact]
    public void TryParseStage_rejects_blank()
    {
        Assert.False(Lead.TryParseStage("   ", out _));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("6")]
    [InlineData("-1")]
    public void TryParseStage_rejects_numeric_strings(string value)
    {
        Assert.False(Lead.TryParseStage(value, out _));
    }
}
