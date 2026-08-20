using Microsoft.EntityFrameworkCore;
using Coaching.Domain.Entities;
using MassTransit;
using System.Reflection;
using EduPlatform.Shared.Infrastructure.Middleware;
using Coaching.Application.Exceptions;
using Npgsql;

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
    public DbSet<AdminAuditRecord> AdminAuditRecords => Set<AdminAuditRecord>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        ConfigureAdminAudit(modelBuilder);
        ConfigureIdempotency(modelBuilder);

        // Schema
        modelBuilder.HasDefaultSchema("coaching");

        // MassTransit inbox/outbox tables keep database changes and published
        // events in the same transaction and make consumer delivery idempotent.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    private static void ConfigureAdminAudit(ModelBuilder modelBuilder)
    {
        var audit = modelBuilder.Entity<AdminAuditRecord>();
        audit.ToTable("AdminAuditRecords");
        audit.HasKey(record => record.Id);
        audit.Property(record => record.ServiceName).HasMaxLength(150);
        audit.Property(record => record.ActorUserId).HasMaxLength(100);
        audit.Property(record => record.ActorRoles).HasMaxLength(500);
        audit.Property(record => record.TenantId).HasMaxLength(100);
        audit.Property(record => record.HttpMethod).HasMaxLength(10);
        audit.Property(record => record.Path).HasMaxLength(500);
        audit.Property(record => record.CorrelationId).HasMaxLength(100);
        audit.Property(record => record.ClientIp).HasMaxLength(64);
        audit.Property(record => record.UserAgent).HasMaxLength(256);
        audit.HasIndex(record => new { record.OccurredAt, record.Id });
        audit.HasIndex(record => new { record.ActorUserId, record.OccurredAt });
    }

    private static void ConfigureIdempotency(ModelBuilder modelBuilder)
    {
        var idempotency = modelBuilder.Entity<IdempotencyRecord>();
        idempotency.ToTable("IdempotencyRecords");
        idempotency.HasKey(record => record.Id);
        idempotency.Property(record => record.Scope).HasMaxLength(150).IsRequired();
        idempotency.Property(record => record.Key).HasMaxLength(128).IsRequired();
        idempotency.Property(record => record.RequestHash).HasMaxLength(64).IsRequired();
        idempotency.HasIndex(record => new { record.Scope, record.Key }).IsUnique();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ChangeTracker.Entries<AdminAuditRecord>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Admin audit records are append-only.");
        }

        // Timestamps are managed by entities themselves (CreatedAt defaults to UtcNow, UpdatedAt set manually)
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConstraintViolation(exception))
        {
            throw new IdempotencyConflictException(exception);
        }
    }

    private static bool IsIdempotencyConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && string.Equals(
            postgresException.ConstraintName,
            "IX_IdempotencyRecords_Scope_Key",
            StringComparison.Ordinal);
}
