using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Coaching.Infrastructure.Data;

// Scaffolding is independent of web-host security settings and live credentials.
// Actual deployment migrations continue to use the API's explicit --migrate-only mode.
public sealed class CoachingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CoachingDbContext>
{
    public CoachingDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=coaching_design_only;Username=unused;Password=unused",
                options => options.MigrationsHistoryTable("__ef_migrations_history", "coaching"))
            .Options);
}
