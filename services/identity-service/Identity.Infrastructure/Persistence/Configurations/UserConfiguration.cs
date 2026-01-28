using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.FirstName)
            .HasMaxLength(100)
            .HasDefaultValue("");

        builder.Property(u => u.LastName)
            .HasMaxLength(100)
            .HasDefaultValue("");

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(u => u.CreatedAt)
            .HasDefaultValueSql("NOW()");

        // Relationships are configured in individual entity configurations or navigation side
        // builder.HasMany(u => u.Roles)... handled in UserRoleConfiguration

        builder.HasOne(u => u.StudentProfile)
            .WithOne(s => s.User)
            .HasForeignKey<StudentProfile>(s => s.UserId);

        builder.HasOne(u => u.TeacherProfile)
            .WithOne(t => t.User)
            .HasForeignKey<TeacherProfile>(t => t.UserId);

        builder.HasOne(u => u.ParentProfile)
            .WithOne(p => p.User)
            .HasForeignKey<ParentProfile>(p => p.UserId);
    }
}


