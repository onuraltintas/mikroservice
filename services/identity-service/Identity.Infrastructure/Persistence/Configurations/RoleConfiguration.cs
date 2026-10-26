using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(x => x.Id);

        builder.ToTable("Roles");

        builder.Property(x => x.Name)
            .HasMaxLength(50)
            .IsRequired();
        
        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.Property(x => x.Description)
            .HasMaxLength(250);
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();
            
        builder.ToTable("UserRoles");

        builder.Property(userRole => userRole.Product)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(userRole => new { userRole.UserId, userRole.RoleId, userRole.Product })
            .IsUnique();

        builder.HasOne<User>()
            .WithMany(u => u.Roles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Role)
            .WithMany()
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Restrict); // Rol silinirse kullanıcı rolü kaydı ne olur? Restrict: Rol silinemez eğer kullanıcısı varsa.
    }
}

public sealed class UserProductAccessConfiguration : IEntityTypeConfiguration<UserProductAccess>
{
    public void Configure(EntityTypeBuilder<UserProductAccess> builder)
    {
        builder.HasKey(access => access.Id);
        builder.ToTable("UserProductAccess");

        builder.Property(access => access.Product)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(access => access.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(access => access.Source)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany(user => user.ProductAccesses)
            .HasForeignKey(access => access.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(access => new { access.UserId, access.Product })
            .IsUnique();
        builder.HasIndex(access => new { access.Product, access.Status, access.UserId });
    }
}
