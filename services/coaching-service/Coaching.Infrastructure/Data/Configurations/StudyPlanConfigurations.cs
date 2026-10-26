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
        builder.Property(x=>x.WeeklySource).HasColumnType("jsonb")
            .HasConversion(x=>x==null?null:System.Text.Json.JsonSerializer.Serialize(x,(System.Text.Json.JsonSerializerOptions?)null),
                x=>x==null?null:System.Text.Json.JsonSerializer.Deserialize<WeeklyPlanSource>(x,(System.Text.Json.JsonSerializerOptions?)null));
        builder.Property(x=>x.AutomaticOutcomeEvidence).HasColumnType("jsonb")
            .HasConversion(x=>x==null?null:System.Text.Json.JsonSerializer.Serialize(x,(System.Text.Json.JsonSerializerOptions?)null),
                x=>x==null?null:System.Text.Json.JsonSerializer.Deserialize<AutomaticOutcomeEvidence>(x,(System.Text.Json.JsonSerializerOptions?)null));
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
            table.HasCheckConstraint("ck_study_task_remaining", "(\"RemainingMinutes\" IS NULL OR (NOT \"IsCompleted\" AND \"RemainingMinutes\" BETWEEN 1 AND 1440)) AND (\"RemainingQuestions\" IS NULL OR (\"RemainingMinutes\" IS NOT NULL AND \"RemainingQuestions\" BETWEEN 0 AND 100000)) AND ((\"PredecessorTaskId\" IS NULL AND \"PredecessorVersion\" IS NULL) OR (\"PredecessorTaskId\" IS NOT NULL AND \"PredecessorVersion\" >= 0 AND \"OriginalTaskId\" IS NOT NULL))");
            table.HasCheckConstraint("ck_study_task_minutes", "\"PlannedMinutes\" BETWEEN 1 AND 1440 AND (\"ActualMinutes\" IS NULL OR \"ActualMinutes\" BETWEEN 1 AND 1440)");
            table.HasCheckConstraint("ck_study_task_completion", "(\"IsCompleted\" AND (\"ActualMinutes\" IS NOT NULL OR \"CompletionSourceTaskId\" IS NOT NULL) AND \"CompletedAt\" IS NOT NULL) OR (NOT \"IsCompleted\" AND \"CompletedAt\" IS NULL AND \"CompletionSourceTaskId\" IS NULL)");
            table.HasCheckConstraint("ck_study_task_questions", "(\"PlannedQuestions\" IS NULL OR \"PlannedQuestions\" BETWEEN 0 AND 100000) AND (\"ActualQuestions\" IS NULL OR \"ActualQuestions\" BETWEEN 0 AND 100000) AND (\"ActualQuestions\" IS NULL OR \"ActualMinutes\" IS NOT NULL)");
            table.HasCheckConstraint("ck_study_task_weekly_time","(\"PlannedStartMinute\" IS NULL AND \"WeeklyLessonId\" IS NULL AND \"LessonNameSnapshot\" IS NULL) OR (\"PlannedStartMinute\" IS NOT NULL AND \"PlannedStartMinute\" BETWEEN 0 AND 1439 AND \"PlannedStartMinute\"+\"PlannedMinutes\"<=1440 AND ((\"WeeklyLessonId\" IS NULL AND \"LessonNameSnapshot\" IS NULL) OR (\"WeeklyLessonId\" IS NOT NULL AND \"LessonNameSnapshot\" IS NOT NULL)))");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RealizationHistoryJson).HasColumnType("jsonb").HasDefaultValue("[]").IsRequired();
        builder.Ignore(x => x.RealizationHistory);
        builder.Ignore(x => x.WorkDays);
        builder.Property(x=>x.ActualWorkJson).HasColumnType("jsonb");
        builder.HasIndex(x=>new { x.StudentId,x.WorkFromDate,x.WorkToDate });
        builder.Property(x => x.SourceName).HasMaxLength(300);
        builder.Property(x=>x.LessonNameSnapshot).HasMaxLength(300);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne<StudyPlanRevision>().WithMany().HasForeignKey(x => new { x.RevisionId, x.StudentId })
            .HasPrincipalKey(x => new { x.Id, x.StudentId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StudyCatalogTopic>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.StudentId, x.PlannedDate });
    }
}
