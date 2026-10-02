using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class TargetUniversityProgramConfiguration : IEntityTypeConfiguration<TargetUniversityProgram>
{
    public void Configure(EntityTypeBuilder<TargetUniversityProgram> builder)
    {
        builder.ToTable("target_university_programs", table => table.HasCheckConstraint("ck_target_program_score", "\"MinimumScore\" IS NULL OR \"MinimumScore\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Source).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.UniversityName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ProgramCode).HasMaxLength(50);
        builder.Property(x => x.ScoreType).HasMaxLength(30);
        builder.Property(x => x.MinimumScore).HasPrecision(10, 4);
        builder.HasIndex(x => new { x.Source, x.SourceId }).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.ScoreType });
    }
}

public sealed class TargetSchoolConfiguration : IEntityTypeConfiguration<TargetSchool>
{
    public void Configure(EntityTypeBuilder<TargetSchool> builder)
    {
        builder.ToTable("target_schools", table =>
        {
            table.HasCheckConstraint("ck_target_school_score", "\"MinimumScore\" IS NULL OR \"MinimumScore\" BETWEEN 0 AND 500");
            table.HasCheckConstraint("ck_target_school_location_pair", "(\"ProvinceId\" IS NULL AND \"DistrictId\" IS NULL) OR (\"ProvinceId\" IS NOT NULL AND \"DistrictId\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Source).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.City).HasMaxLength(100).IsRequired();
        builder.Property(x => x.District).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProvinceId).HasMaxLength(20);
        builder.Property(x => x.DistrictId).HasMaxLength(20);
        builder.Property(x => x.MinimumScore).HasPrecision(10, 4);
        builder.HasIndex(x => new { x.Source, x.SourceId }).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.City, x.District });
        builder.HasIndex(x => new { x.IsActive, x.ProvinceId, x.DistrictId });
    }
}
