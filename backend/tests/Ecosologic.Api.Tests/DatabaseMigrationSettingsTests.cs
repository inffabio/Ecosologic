using System.Globalization;
using Ecosologic.Api.Configuration;
using Ecosologic.Infrastructure.Persistence;

namespace Ecosologic.Api.Tests;

public class DatabaseMigrationSettingsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseBackoff_defaults_to_two_seconds_when_missing_or_blank(string? value)
    {
        var backoff = DatabaseMigrationSettings.ParseBackoff(value);

        Assert.Equal(DatabaseMigrator.DefaultBaseDelay, backoff);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("0")]
    [InlineData("-0")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e308")]
    [InlineData("61")]
    [InlineData("3600")]
    [InlineData("1,5")]
    [InlineData("1,000")]
    [InlineData("0,5")]
    public void ParseBackoff_falls_back_to_default_when_invalid_or_out_of_range(string value)
    {
        var backoff = DatabaseMigrationSettings.ParseBackoff(value);

        Assert.Equal(DatabaseMigrator.DefaultBaseDelay, backoff);
    }

    [Theory]
    [InlineData("0.5")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("60")]
    public void ParseBackoff_uses_configured_value_when_valid(string value)
    {
        var backoff = DatabaseMigrationSettings.ParseBackoff(value);

        Assert.Equal(TimeSpan.FromSeconds(double.Parse(value, CultureInfo.InvariantCulture)), backoff);
    }

    [Fact]
    public void ParseBackoff_accepts_exactly_the_maximum_of_sixty_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), DatabaseMigrationSettings.ParseBackoff("60"));
    }

    [Fact]
    public void ParseBackoff_reads_configuration_using_invariant_culture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            Assert.Equal(TimeSpan.FromSeconds(0.5), DatabaseMigrationSettings.ParseBackoff("0.5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [InlineData("1e-9")]
    [InlineData("0.000000001")]
    [InlineData("5e-8")]
    public void ParseBackoff_falls_back_to_default_when_value_rounds_to_zero(string value)
    {
        var backoff = DatabaseMigrationSettings.ParseBackoff(value);

        Assert.Equal(DatabaseMigrator.DefaultBaseDelay, backoff);
    }
}
