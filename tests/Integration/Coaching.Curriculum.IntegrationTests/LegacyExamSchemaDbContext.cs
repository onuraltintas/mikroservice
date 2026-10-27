using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

/// <summary>
/// Reads and writes exams on a database migrated to a point before the institution-wide exam columns (roadmap O4)
/// existed. Migrations still run through <see cref="CoachingDbContext"/>; only data access uses this context.
/// </summary>
public sealed class LegacyExamSchemaDbContext(string connectionString)
    : CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connectionString).Options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Exam>().Ignore(x => x.CreatedByAdminId).Ignore(x => x.PracticeExamCode);
    }
}
