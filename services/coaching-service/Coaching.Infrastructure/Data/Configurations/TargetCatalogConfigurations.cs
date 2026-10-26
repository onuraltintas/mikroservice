using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class TargetCatalogConfiguration : IEntityTypeConfiguration<TargetCatalogEntry>
{
    public void Configure(EntityTypeBuilder<TargetCatalogEntry> b)
    {
        b.ToTable("target_catalog", t =>
        {
            t.HasCheckConstraint("ck_target_catalog_kind", "\"CatalogType\" IN ('School','UniversityProgram')");
            t.HasCheckConstraint("ck_target_catalog_values", "(\"Quota\" IS NULL OR \"Quota\" >= 0) AND (\"Placed\" IS NULL OR \"Placed\" >= 0) AND (\"Rank\" IS NULL OR \"Rank\" > 0) AND (\"Percentile\" IS NULL OR \"Percentile\" BETWEEN 0 AND 100) AND (\"MinimumScore\" IS NULL OR \"MinimumScore\" >= 0) AND (\"MaximumScore\" IS NULL OR \"MaximumScore\" >= 0) AND (\"MinimumScore\" IS NULL OR \"MaximumScore\" IS NULL OR \"MinimumScore\" <= \"MaximumScore\")");
            t.HasCheckConstraint("ck_target_catalog_school", "\"CatalogType\" <> 'School' OR (\"City\" IS NOT NULL AND \"District\" IS NOT NULL AND (\"MinimumScore\" IS NULL OR \"MinimumScore\" <= 500) AND (\"MaximumScore\" IS NULL OR \"MaximumScore\" <= 500))");
            t.HasCheckConstraint("ck_target_catalog_degree", "\"Degree\" IS NULL OR \"Degree\" IN ('Associate','Bachelor')");
            t.HasCheckConstraint("ck_target_catalog_score_type", "\"ScoreType\" IS NULL OR (\"CatalogType\" = 'School' AND \"ScoreType\" = 'LGS') OR (\"CatalogType\" = 'UniversityProgram' AND \"ScoreType\" IN ('TYT','SAY','EA','SOZ','YDT'))");
            t.HasCheckConstraint("ck_target_catalog_location_pair", "(\"ProvinceId\" IS NULL AND \"DistrictId\" IS NULL) OR (\"ProvinceId\" IS NOT NULL AND \"DistrictId\" IS NOT NULL)");
            t.HasCheckConstraint("ck_target_catalog_year", "(\"CatalogYear\" IS NULL OR \"CatalogYear\" BETWEEN 2026 AND 2100) AND (\"PlacementYear\" IS NULL OR \"PlacementYear\" BETWEEN 1900 AND 2100)");
        });
        b.HasKey(x => x.Id);
        b.HasDiscriminator<string>("CatalogType").HasValue<TargetSchool>("School")
            .HasValue<TargetUniversityProgram>("UniversityProgram");
        b.Property("CatalogType").HasMaxLength(30);
        b.Property(x => x.Source).HasMaxLength(100).IsRequired();
        b.Property(x => x.SourceId).HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        foreach (var name in new[] { "City", "District", "InstitutionType", "Language", "Field", "Scholarship", "EducationType" })
            b.Property<string>(name).HasMaxLength(300);
        b.Property(x => x.ScoreType).HasMaxLength(30);
        b.Property(x => x.ScoreYear).HasColumnName("PlacementYear");
        b.Property(x => x.PlacementStage).HasMaxLength(40);
        b.Property(x => x.SourceUrl).HasMaxLength(2000);
        b.Property(x => x.MinimumScore).HasPrecision(12, 5);
        b.Property(x => x.MaximumScore).HasPrecision(12, 5);
        b.Property(x => x.Percentile).HasPrecision(7, 4);
        b.HasIndex(x => new { x.Source, x.SourceId }).IsUnique();
        b.HasIndex("CatalogType", "IsActive", "City", "District");
    }
}

public sealed class TargetUniversityProgramConfiguration : IEntityTypeConfiguration<TargetUniversityProgram>
{
    public void Configure(EntityTypeBuilder<TargetUniversityProgram> b)
    {
        b.Property(x => x.UniversityName).HasColumnName("InstitutionName").HasMaxLength(300).IsRequired();
        b.Property(x => x.ProgramCode).HasMaxLength(50);
        b.Property(x => x.FacultyName).HasMaxLength(300);
        b.Property(x => x.ProgramType).HasColumnName("Degree").HasMaxLength(20);
        b.HasIndex(x => x.ProgramCode).IsUnique().HasFilter("\"ProgramCode\" IS NOT NULL AND \"IsActive\" = TRUE");
        b.HasIndex(x => new { x.IsActive, x.ScoreType });
    }
}

public sealed class TargetSchoolConfiguration : IEntityTypeConfiguration<TargetSchool>
{
    public void Configure(EntityTypeBuilder<TargetSchool> b)
    {
        b.Property(x => x.ProvinceId).HasMaxLength(20);
        b.Property(x => x.DistrictId).HasMaxLength(20);
    }
}
