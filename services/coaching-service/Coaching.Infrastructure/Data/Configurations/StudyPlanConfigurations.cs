using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class StudyPlanRevisionConfiguration : IEntityTypeConfiguration<StudyPlanRevision>
{
    public void Configure(EntityTypeBuilder<StudyPlanRevision> builder)
    {
        builder.ToTable("study_plan_revisions", table =>
        {
            table.HasCheckConstraint("ck_study_plan_revision_number", "\"RevisionNumber\" > 0");
            table.HasCheckConstraint("ck_study_plan_status", "\"Status\" BETWEEN 0 AND 2 AND \"IsActive\" = (\"Status\" = 1)");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.StudentId });
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => x.StudentId, "ux_study_plan_active_student").IsUnique().HasFilter("\"IsActive\"");
        builder.HasIndex(x => x.StudentId, "ux_study_plan_draft_student").IsUnique().HasFilter("\"Status\" = 0");
        builder.HasIndex(x => new { x.StudentId, x.PlanId, x.RevisionNumber }).IsUnique();
    }
}

public sealed class StudyPlanTaskConfiguration : IEntityTypeConfiguration<StudyPlanTask>
{
    public void Configure(EntityTypeBuilder<StudyPlanTask> builder)
    {
        builder.ToTable("study_plan_tasks", table =>
        {
            table.HasCheckConstraint("ck_study_task_minutes", "\"PlannedMinutes\" BETWEEN 1 AND 1440 AND (\"ActualMinutes\" IS NULL OR \"ActualMinutes\" BETWEEN 1 AND 1440)");
            table.HasCheckConstraint("ck_study_task_completion", "(\"IsCompleted\" AND \"ActualMinutes\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL) OR (NOT \"IsCompleted\" AND \"ActualMinutes\" IS NULL AND \"CompletedAt\" IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne<StudyPlanRevision>().WithMany().HasForeignKey(x => new { x.RevisionId, x.StudentId })
            .HasPrincipalKey(x => new { x.Id, x.StudentId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StudyCatalogTopic>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.StudentId, x.PlannedDate });
    }
}
