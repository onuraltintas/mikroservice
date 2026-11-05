using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Configurations;

/// <summary>Roadmap O5: yearly score models for LGS and YKS estimates.</summary>
public sealed class CoachingScoreModelConfiguration : IEntityTypeConfiguration<CoachingScoreModel>
{
    /// <summary>Paket B (05.11.2026): every score type ScoreModelRules knows (LGS, YKS and the MEB-AGS P1 / P2 types).</summary>
    public static string TypeCheck => $"\"ScoreType\" IN ({string.Join(", ", ScoreModelRules.Types.Select(x => $"'{x.Code}'"))})";

    public void Configure(EntityTypeBuilder<CoachingScoreModel> builder)
    {
        builder.ToTable("score_models", table =>
        {
            table.HasCheckConstraint("ck_score_model_type", TypeCheck);
            table.HasCheckConstraint("ck_score_model_year", "\"Year\" BETWEEN 2018 AND 2100");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ScoreType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.BaseScore).HasPrecision(8, 3);
        builder.Property(x => x.DiplomaWeight).HasPrecision(6, 4);
        builder.Property(x => x.CoefficientsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.RanksJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.UncertaintyShare).HasPrecision(6, 4);
        builder.Property(x => x.UncertaintyCap).HasPrecision(8, 3);
        builder.HasIndex(x => new { x.ScoreType, x.Year }).IsUnique();
    }
}

/// <summary>KM7: score model versions for history and restore.</summary>
public sealed class CoachingScoreModelVersionConfiguration : IEntityTypeConfiguration<CoachingScoreModelVersion>
{
    public void Configure(EntityTypeBuilder<CoachingScoreModelVersion> builder)
    {
        builder.ToTable("score_model_versions", table =>
        {
            table.HasCheckConstraint("ck_score_model_version_type", CoachingScoreModelConfiguration.TypeCheck);
            table.HasCheckConstraint("ck_score_model_version_action", "\"Action\" IN ('Upload', 'Restore', 'Delete')");
            table.HasCheckConstraint("ck_score_model_version_year", "\"Year\" BETWEEN 2018 AND 2100");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ScoreType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(10).IsRequired();
        builder.Property(x => x.BaseScore).HasPrecision(8, 3);
        builder.Property(x => x.DiplomaWeight).HasPrecision(6, 4);
        builder.Property(x => x.CoefficientsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.RanksJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.UncertaintyShare).HasPrecision(6, 4);
        builder.Property(x => x.UncertaintyCap).HasPrecision(8, 3);
        builder.Property(x => x.FileSha256).HasMaxLength(64);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => new { x.Year, x.ScoreType, x.CreatedAt });
        builder.HasIndex(x => x.UploadId);
    }
}
