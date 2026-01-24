using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public class InstitutionConfiguration : IEntityTypeConfiguration<Institution>
{
    public void Configure(EntityTypeBuilder<Institution> builder)
    {
        builder.ToTable("institutions");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(i => i.Type)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(i => i.LicenseType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(i => i.LogoUrl)
            .HasMaxLength(500);

        builder.Property(i => i.City)
            .HasMaxLength(100);

        builder.Property(i => i.District)
            .HasMaxLength(100);

        builder.Property(i => i.Phone)
            .HasMaxLength(20);

        builder.Property(i => i.Email)
            .HasMaxLength(255);

        builder.Property(i => i.Website)
            .HasMaxLength(255);

        builder.Property(i => i.TaxNumber)
            .HasMaxLength(20);

        builder.Property(i => i.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(i => i.Type);
        builder.HasIndex(i => i.City);

        // Relationships
        builder.HasMany(i => i.Admins)
            .WithOne(a => a.Institution)
            .HasForeignKey(a => a.InstitutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Teachers)
            .WithOne(t => t.Institution)
            .HasForeignKey(t => t.InstitutionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(i => i.Students)
            .WithOne(s => s.Institution)
            .HasForeignKey(s => s.InstitutionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class InstitutionAdminConfiguration : IEntityTypeConfiguration<InstitutionAdmin>
{
    public void Configure(EntityTypeBuilder<InstitutionAdmin> builder)
    {
        builder.ToTable("institution_admins");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Role)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.Permissions)
            .HasColumnType("jsonb");

        builder.HasIndex(a => new { a.UserId, a.InstitutionId })
            .IsUnique();

        builder.HasIndex(a => a.InstitutionId);
    }
}
