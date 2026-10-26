using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Identity.Infrastructure.Persistence;

public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();

        // Design-time tooling can run from the repository root or from the
        // project directory. Prefer environment-backed configuration so CI
        // never needs a checked-in connection string.
        var currentDirectory = Directory.GetCurrentDirectory();
        var apiDirectory = Path.Combine(currentDirectory, "services", "identity-service", "Identity.API");
        if (!Directory.Exists(apiDirectory))
        {
            apiDirectory = Path.GetFullPath(Path.Combine(currentDirectory, "..", "Identity.API"));
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var host = configuration["POSTGRES_HOST"] ?? "localhost";
            var port = configuration["POSTGRES_PORT"] ?? "5432";
            var database = configuration["POSTGRES_DB_IDENTITY"] ?? "identity_db";
            var username = configuration["POSTGRES_USER"] ?? "eduplatform";
            var password = configuration["POSTGRES_PASSWORD"]
                ?? throw new InvalidOperationException("POSTGRES_PASSWORD is required for EF design-time operations.");
            connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
        }

        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
        });

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
