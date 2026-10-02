using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Coaching.Domain.Entities;

namespace Coaching.Infrastructure.Configurations;

/// <summary>
/// AcademicGoal entity configuration
/// </summary>
public class AcademicGoalConfiguration : IEntityTypeConfiguration<AcademicGoal>
{
    public void Configure(EntityTypeBuilder<AcademicGoal> builder)
    {
        builder.ToTable("academic_goals", table => table.HasCheckConstraint(
            "ck_goal_single_catalog_target", "target_university_program_id IS NULL OR target_school_id IS NULL"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.StudentId)
            .HasColumnName("student_id")
            .IsRequired();

        builder.Property(x => x.SetByTeacherId)
            .HasColumnName("set_by_teacher_id");

        builder.Property(x => x.InstitutionId)
            .HasColumnName("institution_id");

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.TargetExamType)
            .HasColumnName("target_exam_type")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.TargetSubject)
            .HasColumnName("target_subject")
            .HasMaxLength(100);

        builder.Property(x => x.TargetUniversityProgramId).HasColumnName("target_university_program_id");
        builder.Property(x => x.TargetSchoolId).HasColumnName("target_school_id");
        builder.HasOne<TargetUniversityProgram>().WithMany().HasForeignKey(x => x.TargetUniversityProgramId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TargetSchool>().WithMany().HasForeignKey(x => x.TargetSchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.TargetScore)
            .HasColumnName("target_score")
            .HasPrecision(5, 2);

        builder.Property(x => x.TargetDate)
            .HasColumnName("target_date");

        builder.Property(x => x.CurrentProgress)
            .HasColumnName("current_progress")
            .IsRequired();

        builder.Property(x => x.IsCompleted)
            .HasColumnName("is_completed")
            .IsRequired();

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.Version)
            .IsConcurrencyToken()
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("ix_academic_goals_student_id");

        builder.HasIndex(x => new { x.InstitutionId, x.StudentId })
            .HasDatabaseName("ix_academic_goals_institution_student");

        builder.HasIndex(x => x.IsCompleted)
            .HasDatabaseName("ix_academic_goals_is_completed");

        builder.HasIndex(x => x.TargetDate)
            .HasDatabaseName("ix_academic_goals_target_date");
    }
}
