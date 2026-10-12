using CourseManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Infrastructure;

public sealed class CourseManagementDbContext(DbContextOptions<CourseManagementDbContext> options) : DbContext(options)
{
    public DbSet<CourseUserProfile> UserProfiles => Set<CourseUserProfile>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CourseUserProfile>(entity =>
        {
            entity.ToTable("UserProfiles");
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.DisplayName).HasMaxLength(200).IsRequired();
        });
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.ToTable("Workspaces");
            entity.HasKey(workspace => workspace.Id);
            entity.Property(workspace => workspace.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(workspace => workspace.OwnerUserId);
        });
    }
}
