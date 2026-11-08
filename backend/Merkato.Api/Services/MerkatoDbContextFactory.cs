using Merkato.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Merkato.Api.Services;

// Keeps EF migrations independent from web-host startup, admin seeding and .env files.
public sealed class MerkatoDbContextFactory : IDesignTimeDbContextFactory<MerkatoDbContext>
{
    public MerkatoDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSql")
            ?? Environment.GetEnvironmentVariable("MERKATO_MIGRATION_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Set ConnectionStrings__PostgreSql before running database migrations.");

        var options = new DbContextOptionsBuilder<MerkatoDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new MerkatoDbContext(options);
    }
}
