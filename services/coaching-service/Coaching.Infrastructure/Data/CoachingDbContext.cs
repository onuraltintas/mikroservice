using Microsoft.EntityFrameworkCore;
using Coaching.Domain.Entities;
using System.Reflection;

namespace Coaching.Infrastructure.Data;

/// <summary>
/// Coaching Service Database Context
/// </summary>
public class CoachingDbContext : DbContext
{
    public CoachingDbContext(DbContextOptions<CoachingDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentStudent> AssignmentStudents => Set<AssignmentStudent>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<CoachingSession> CoachingSessions => Set<CoachingSession>();
    public DbSet<SessionAttendance> SessionAttendances => Set<SessionAttendance>();
    public DbSet<AcademicGoal> AcademicGoals => Set<AcademicGoal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Schema
        modelBuilder.HasDefaultSchema("coaching");
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Timestamps are managed by entities themselves (CreatedAt defaults to UtcNow, UpdatedAt set manually)
        return await base.SaveChangesAsync(cancellationToken);
    }
}
