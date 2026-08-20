using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Serilog;
using EduPlatform.Shared.Kernel.Primitives;
using MassTransit;
using EduPlatform.Shared.Infrastructure.Middleware;
using Identity.Application.Exceptions;
using Npgsql;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Identity Service DbContext
/// </summary>
public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<InstitutionAdmin> InstitutionAdmins => Set<InstitutionAdmin>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();
    public DbSet<ParentProfile> ParentProfiles => Set<ParentProfile>();
    public DbSet<TeacherStudentAssignment> TeacherStudentAssignments => Set<TeacherStudentAssignment>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserLogin> UserLogins => Set<UserLogin>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<SystemConfiguration> Configurations => Set<SystemConfiguration>();
    public DbSet<AdminAuditRecord> AdminAuditRecords => Set<AdminAuditRecord>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        ConfigureAdminAudit(modelBuilder);
        ConfigureIdempotency(modelBuilder);

        // Set default schema
        modelBuilder.HasDefaultSchema("identity");

        // MassTransit Outbox Pattern
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
        EnsureAdminAuditIsAppendOnly();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Modified)
            {
                var updatedAtProperty = entry.Entity.GetType().GetProperty("UpdatedAt");
                if (updatedAtProperty != null && updatedAtProperty.CanWrite)
                {
                    updatedAtProperty.SetValue(entry.Entity, DateTime.UtcNow);
                }

                if (entry.Entity is AggregateRoot aggregate)
                {
                    var versionProperty = entry.Entity.GetType().GetProperty("Version");
                    if (versionProperty != null && versionProperty.CanWrite)
                    {
                        var currentVersion = (int)(versionProperty.GetValue(entry.Entity) ?? 0);
                        versionProperty.SetValue(entry.Entity, currentVersion + 1);
                    }
                }
            }
        }

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

    private void EnsureAdminAuditIsAppendOnly()
    {
        if (ChangeTracker.Entries<AdminAuditRecord>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Admin audit records are append-only.");
        }
    }
}
