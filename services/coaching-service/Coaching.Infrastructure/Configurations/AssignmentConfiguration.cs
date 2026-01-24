using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;

namespace Coaching.Infrastructure.Configurations;

/// <summary>
/// Assignment entity configuration
/// </summary>
public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("assignments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever(); // Guid generated in domain

        builder.Property(x => x.TeacherId)
            .HasColumnName("teacher_id")
            .IsRequired();

        builder.Property(x => x.InstitutionId)
            .HasColumnName("institution_id");

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(x => x.Subject)
            .HasColumnName("subject")
            .HasMaxLength(100);

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.TargetGradeLevel)
            .HasColumnName("target_grade_level");

        builder.Property(x => x.DueDate)
            .HasColumnName("due_date")
            .IsRequired();

        builder.Property(x => x.EstimatedDurationMinutes)
            .HasColumnName("estimated_duration_minutes");

        builder.Property(x => x.MaxScore)
            .HasColumnName("max_score")
            .HasPrecision(5, 2);

        builder.Property(x => x.PassingScore)
            .HasColumnName("passing_score")
            .HasPrecision(5, 2);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // Relationships
        builder.HasMany(x => x.AssignedStudents)
            .WithOne(x => x.Assignment)
            .HasForeignKey(x => x.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.TeacherId)
            .HasDatabaseName("ix_assignments_teacher_id");

        builder.HasIndex(x => x.InstitutionId)
            .HasDatabaseName("ix_assignments_institution_id");

        builder.HasIndex(x => x.DueDate)
            .HasDatabaseName("ix_assignments_due_date");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_assignments_status");
    }
}

/// <summary>
/// AssignmentStudent entity configuration
/// </summary>
public class AssignmentStudentConfiguration : IEntityTypeConfiguration<AssignmentStudent>
{
    public void Configure(EntityTypeBuilder<AssignmentStudent> builder)
    {
        builder.ToTable("assignment_students");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.AssignmentId)
            .HasColumnName("assignment_id")
            .IsRequired();

        builder.Property(x => x.StudentId)
            .HasColumnName("student_id")
            .IsRequired();

        builder.Property(x => x.SubmittedAt)
            .HasColumnName("submitted_at");

        builder.Property(x => x.Score)
            .HasColumnName("score")
            .HasPrecision(5, 2);

        builder.Property(x => x.TeacherFeedback)
            .HasColumnName("teacher_feedback")
            .HasColumnType("text");

        builder.Property(x => x.StudentNote)
            .HasColumnName("student_note")
            .HasColumnType("text");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("ix_assignment_students_student_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_assignment_students_status");

        builder.HasIndex(x => new { x.AssignmentId, x.StudentId })
            .HasDatabaseName("ix_assignment_students_assignment_student")
            .IsUnique();
    }
}
