using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class WeeklyReflectionConfiguration : IEntityTypeConfiguration<WeeklyReflection>
{
    public void Configure(EntityTypeBuilder<WeeklyReflection> b)
    {
        b.ToTable("weekly_reflections", t => {
            t.HasCheckConstraint("ck_weekly_reflection_monday", "extract(isodow from \"WeekStart\")=1");
            t.HasCheckConstraint("ck_weekly_reflection_rating", "\"Rating\" IS NULL OR \"Rating\" BETWEEN 1 AND 5");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Version).IsConcurrencyToken();
        b.Property(x => x.Successes).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Obstacles).HasMaxLength(2000).IsRequired();
        b.Property(x => x.NextFocus).HasMaxLength(2000).IsRequired();
        b.Property(x => x.ResultsJson).HasColumnType("jsonb");
        b.Property(x => x.ReflectionHistoryJson).HasColumnType("jsonb").HasDefaultValue("[]").IsRequired();
        b.Ignore(x => x.ReflectionHistory);
        b.HasIndex(x => new { x.StudentId, x.WeekStart }).IsUnique();
        b.HasIndex(x=>new {x.WeekStart,x.Id});
    }
}
public sealed class WeeklyDecisionConfiguration : IEntityTypeConfiguration<WeeklyDecision>
{
    public void Configure(EntityTypeBuilder<WeeklyDecision> b)
    {
        b.ToTable("weekly_decisions", t => {
            t.HasCheckConstraint("ck_weekly_decision_week", "extract(isodow from \"ReviewWeekStart\")=1 AND \"DueOn\" >= \"ReviewWeekStart\"+7");
            t.HasCheckConstraint("ck_weekly_decision_status", "\"Status\" IN ('Planned','InProgress','Completed','Cancelled') AND ((\"Status\"='Completed')=(\"CompletedAt\" IS NOT NULL))");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Version).IsConcurrencyToken();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired(); b.Property(x => x.Note).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired(); b.Property(x => x.HistoryJson).HasColumnType("jsonb").IsRequired(); b.Ignore(x => x.History); b.Ignore(x => x.IsOpen);
        b.Property(x=>x.SourceReflectionSnapshotJson).HasColumnType("jsonb");
        b.Property(x=>x.PreviousDecisionSnapshotJson).HasColumnType("jsonb");
        b.HasIndex(x=>x.SourceReflectionId);b.HasIndex(x=>x.PreviousDecisionId);
        b.HasIndex(x => new { x.StudentId, x.ReviewWeekStart, x.Status }); b.HasIndex(x => x.TeacherId); b.HasIndex(x => new { x.ResponsibleTeacherId, x.StudentId, x.Status });
    }
}

public sealed class WeeklyReflectionReviewConfiguration : IEntityTypeConfiguration<WeeklyReflectionReview>
{
    public void Configure(EntityTypeBuilder<WeeklyReflectionReview> b)
    {
        b.ToTable("weekly_reflection_reviews"); b.HasKey(x => x.Id);
        b.Property(x => x.Note).HasMaxLength(2000).IsRequired();
        b.Property(x => x.ReflectionSnapshotJson).HasColumnType("jsonb").IsRequired();
        b.HasIndex(x => new { x.ReflectionId, x.TeacherId, x.ReflectionVersion });
        b.HasIndex(x=>new {x.TeacherId,x.ReflectionId,x.CreatedAt,x.Id}).IsDescending(false,false,true,true);
        b.Property(x=>x.EvidenceFingerprint).HasMaxLength(64);
        b.Property(x=>x.EvidenceSnapshotJson).HasColumnType("jsonb");
        b.HasIndex(x => x.StudentId);
        b.HasOne<WeeklyReflection>().WithMany().HasForeignKey(x => x.ReflectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
