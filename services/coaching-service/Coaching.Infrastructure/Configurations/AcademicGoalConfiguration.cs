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
        builder.ToTable("academic_goals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.StudentId)
            .HasColumnName("student_id")
            .IsRequired();

        builder.Property(x => x.SetByTeacherId)
            .HasColumnName("set_by_teacher_id");

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

        // Indexes
        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("ix_academic_goals_student_id");

        builder.HasIndex(x => x.IsCompleted)
            .HasDatabaseName("ix_academic_goals_is_completed");

        builder.HasIndex(x => x.TargetDate)
            .HasDatabaseName("ix_academic_goals_target_date");
    }
}
