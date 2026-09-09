using Ecosologic.Application.Solar;
using Ecosologic.Infrastructure.Solar;
using Microsoft.Extensions.DependencyInjection;

namespace Ecosologic.Api.Tests;

public class AneelTariffServiceRegistrationTests
{
    [Fact]
    public void AddAneelTariffSource_registers_typed_client_as_interface()
    {
        var services = new ServiceCollection();
        services.AddAneelTariffSource();

        using var provider = services.BuildServiceProvider();

        var source = provider.GetRequiredService<IAneelTariffSource>();

        Assert.IsType<AneelTariffClient>(source);
    }

    [Fact]
    public void AddAneelTariffSource_resolves_distinct_instances_per_scope()
    {
        var services = new ServiceCollection();
        services.AddAneelTariffSource();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<IAneelTariffSource>();
        var second = scope.ServiceProvider.GetRequiredService<IAneelTariffSource>();

        Assert.NotSame(first, second);
    }
}
