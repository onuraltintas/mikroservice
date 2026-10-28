using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Configurations;

/// <summary>Roadmap O5: yearly score models for LGS and YKS estimates.</summary>
public sealed class CoachingScoreModelConfiguration : IEntityTypeConfiguration<CoachingScoreModel>
{
    public void Configure(EntityTypeBuilder<CoachingScoreModel> builder)
    {
        builder.ToTable("score_models", table =>
        {
            table.HasCheckConstraint("ck_score_model_type", "\"ScoreType\" IN ('LGS', 'TYT', 'SAY', 'EA', 'SOZ', 'DIL')");
            table.HasCheckConstraint("ck_score_model_year", "\"Year\" BETWEEN 2018 AND 2100");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ScoreType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.BaseScore).HasPrecision(8, 3);
        builder.Property(x => x.DiplomaWeight).HasPrecision(6, 4);
        builder.Property(x => x.CoefficientsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.RanksJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.ScoreType, x.Year }).IsUnique();
    }
}
