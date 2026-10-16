using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class OfficialCurriculumEditionConfiguration : IEntityTypeConfiguration<OfficialCurriculumEdition>
{
    public void Configure(EntityTypeBuilder<OfficialCurriculumEdition> b)
    {
        b.ToTable("official_curriculum_editions", t => t.HasCheckConstraint("ck_outcome_edition_scope", "(\"GradeNumber\" IS NULL OR \"GradeNumber\" BETWEEN 1 AND 12) AND \"AcademicYearStart\" BETWEEN 2000 AND 2200 AND \"AcademicYearEnd\" BETWEEN \"AcademicYearStart\" AND 2200"));
        b.HasOne<StudyCatalogExam>().WithMany().HasForeignKey(x => x.ExamCode).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
        b.HasKey(x => x.Id); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Code).HasMaxLength(100); b.Property(x => x.ExamCode).HasMaxLength(10); b.Property(x => x.SubjectName).HasMaxLength(300);
        b.Property(x => x.SourceUrl).HasMaxLength(1000); b.Property(x => x.ScopeEvidenceUrl).HasMaxLength(1000); b.Property(x => x.DocumentSha256).HasMaxLength(64);
        b.Property(x => x.PackageSha256).HasMaxLength(64);
    }
}
public sealed class OfficialCurriculumTopicConfiguration : IEntityTypeConfiguration<OfficialCurriculumTopic>
{
    public void Configure(EntityTypeBuilder<OfficialCurriculumTopic> b)
    {
        b.ToTable("official_curriculum_topics", t => t.HasCheckConstraint("ck_official_topic_budget", "\"OutcomeCount\" BETWEEN 1 AND 500 AND \"LessonPeriods\" BETWEEN 1 AND 500"));
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.Id, x.EditionId }); b.HasIndex(x => new { x.EditionId, x.Code }).IsUnique();
        b.Property(x => x.Code).HasMaxLength(100); b.Property(x => x.Name).HasMaxLength(300);
        b.HasOne<OfficialCurriculumEdition>().WithMany().HasForeignKey(x => x.EditionId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class StudyCatalogOutcomeConfiguration : IEntityTypeConfiguration<StudyCatalogOutcome>
{
    public void Configure(EntityTypeBuilder<StudyCatalogOutcome> b)
    {
        b.ToTable("study_catalog_outcomes", t => t.HasCheckConstraint("ck_outcome_source", "\"SourcePage\" > 0 AND \"DisplayOrder\" > 0"));
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.EditionId, x.Code }).IsUnique();
        b.Property(x => x.Code).HasMaxLength(100); b.Property(x => x.Name).HasMaxLength(2000); b.Property(x => x.Notes).HasMaxLength(8000);
        b.HasOne<OfficialCurriculumTopic>().WithMany().HasForeignKey(x => new { x.OfficialTopicId, x.EditionId })
            .HasPrincipalKey(x => new { x.Id, x.EditionId }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class StudyCatalogOutcomeSourceConfiguration : IEntityTypeConfiguration<StudyCatalogOutcomeSource>
{
    public void Configure(EntityTypeBuilder<StudyCatalogOutcomeSource> b)
    {
        b.ToTable("study_catalog_outcome_sources"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.Source, x.SourceId }).IsUnique();
        b.Property(x => x.Source).HasMaxLength(100); b.Property(x => x.SourceId).HasMaxLength(100);
        b.HasOne<StudyCatalogOutcome>().WithMany().HasForeignKey(x => x.OutcomeId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class StudyCatalogTopicOutcomeConfiguration : IEntityTypeConfiguration<StudyCatalogTopicOutcome>
{
    public void Configure(EntityTypeBuilder<StudyCatalogTopicOutcome> b)
    {
        b.ToTable("study_catalog_topic_outcomes"); b.HasKey(x => new { x.TopicId, x.OutcomeId }); b.Property(x => x.Evidence).HasMaxLength(2000);
        b.HasOne<StudyCatalogTopic>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StudyCatalogOutcome>().WithMany().HasForeignKey(x => x.OutcomeId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class OutcomeDurationEstimateConfiguration : IEntityTypeConfiguration<OutcomeDurationEstimate>
{
    public void Configure(EntityTypeBuilder<OutcomeDurationEstimate> b)
    {
        b.ToTable("outcome_duration_estimates", t =>
        {
            t.HasCheckConstraint("ck_outcome_minutes", "\"LearningMinutes\" >= 0 AND \"PracticeMinutes\" >= 0 AND \"CheckMinutes\" >= 0 AND (\"LearningMinutes\"::bigint + \"PracticeMinutes\" + \"CheckMinutes\") BETWEEN 1 AND 1440 AND \"Number\" > 0");
            t.HasCheckConstraint("ck_outcome_review", "\"Method\" IN ('OfficialBudgetDraft','ReviewedStudy') AND \"Confidence\" IN ('Low','Medium','High') AND (\"Method\" <> 'ReviewedStudy' OR (\"ReviewedBy\" IS NOT NULL AND \"ReviewedAt\" IS NOT NULL)) AND (NOT \"IsPublished\" OR \"Method\" = 'ReviewedStudy')");
        });
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.Id, x.OutcomeId }); b.HasIndex(x => new { x.OutcomeId, x.Number }).IsUnique();
        b.Ignore(x => x.TotalMinutes); b.Property(x => x.Method).HasMaxLength(40); b.Property(x => x.Confidence).HasMaxLength(20); b.Property(x => x.Evidence).HasMaxLength(2000);
        b.HasOne<StudyCatalogOutcome>().WithMany().HasForeignKey(x => x.OutcomeId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class StudyPlanTaskOutcomeConfiguration : IEntityTypeConfiguration<StudyPlanTaskOutcome>
{
    public void Configure(EntityTypeBuilder<StudyPlanTaskOutcome> b)
    {
        b.ToTable("study_plan_task_outcomes", t => t.HasCheckConstraint("ck_task_outcome_minutes", "\"AllocatedMinutes\" BETWEEN 1 AND 1440 AND \"Origin\" IN ('Catalog','StudentOverride')"));
        b.HasKey(x => new { x.TaskId, x.OutcomeId }); b.Property(x => x.Origin).HasMaxLength(30);
        b.Property(x => x.CodeSnapshot).HasMaxLength(100); b.Property(x => x.NameSnapshot).HasMaxLength(2000);
        b.HasOne<StudyPlanTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<OutcomeDurationEstimate>().WithMany().HasForeignKey(x => new { x.EstimateId, x.OutcomeId })
            .HasPrincipalKey(x => new { x.Id, x.OutcomeId }).OnDelete(DeleteBehavior.Restrict);
    }
}
