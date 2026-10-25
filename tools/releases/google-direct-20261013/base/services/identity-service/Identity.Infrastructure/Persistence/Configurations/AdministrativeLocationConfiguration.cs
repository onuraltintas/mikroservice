using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> builder)
    {
        builder.ToTable("provinces");
        builder.HasKey(province => province.Id);
        builder.Property(province => province.Id).HasMaxLength(12).ValueGeneratedNever();
        builder.Property(province => province.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(province => province.Name).IsUnique();
    }
}

public sealed class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.ToTable("districts");
        builder.HasKey(district => district.Id);
        builder.Property(district => district.Id).HasMaxLength(16).ValueGeneratedNever();
        builder.Property(district => district.ProvinceId).HasMaxLength(12).IsRequired();
        builder.Property(district => district.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(district => new { district.ProvinceId, district.Name }).IsUnique();
        builder.HasOne(district => district.Province)
            .WithMany()
            .HasForeignKey(district => district.ProvinceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
