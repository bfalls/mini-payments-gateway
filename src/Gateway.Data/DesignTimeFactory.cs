using Gateway.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Gateway.Data;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<GatewayDbContext>
{
    public GatewayDbContext CreateDbContext(string[] args)
    {
        // Prefer env var so CI/local can override without editing code
        var cs = Environment.GetEnvironmentVariable("PG_CONN")
                 ?? "Host=localhost;Port=5432;Database=gateway;Username=postgres;Password=postgres";

        var opts = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseNpgsql(cs)
            .Options;

        return new GatewayDbContext(opts);
    }
}
