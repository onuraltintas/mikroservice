using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Coaching.Infrastructure.Data;

public sealed class CoachingDbContextFactory : IDesignTimeDbContextFactory<CoachingDbContext>
{
    public CoachingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql("Host=localhost;Database=coaching_design_time;Username=design_time;Password=design_time",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "coaching"))
            .Options;
        return new CoachingDbContext(options);
    }
}
