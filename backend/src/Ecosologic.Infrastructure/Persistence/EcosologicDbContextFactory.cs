using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ecosologic.Infrastructure.Persistence;

public sealed class EcosologicDbContextFactory : IDesignTimeDbContextFactory<EcosologicDbContext>
{
    public EcosologicDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EcosologicDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=ecosologic;Username=postgres;Password=postgres")
            .Options;
        return new EcosologicDbContext(options);
    }
}
