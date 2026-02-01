using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Serilog;
using EduPlatform.Shared.Kernel.Primitives;
using MassTransit;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Set default schema
        modelBuilder.HasDefaultSchema("identity");

        // MassTransit Outbox Pattern
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
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

        return await base.SaveChangesAsync(cancellationToken);
    }
}
