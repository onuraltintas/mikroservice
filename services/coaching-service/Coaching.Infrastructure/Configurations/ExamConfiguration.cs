using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Coaching.Domain.Entities;

namespace Coaching.Infrastructure.Configurations;

/// <summary>
/// Exam entity configuration
/// </summary>
public class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("exams");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.InstitutionId)
            .HasColumnName("institution_id");

        builder.Property(x => x.CreatedByTeacherId)
            .HasColumnName("created_by_teacher_id")
            .IsRequired();

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ExamType)
            .HasColumnName("exam_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Subject)
            .HasColumnName("subject")
            .HasMaxLength(100);

        builder.Property(x => x.ExamDate)
            .HasColumnName("exam_date")
            .IsRequired();

        builder.Property(x => x.DurationMinutes)
            .HasColumnName("duration_minutes");

        builder.Property(x => x.MaxScore)
            .HasColumnName("max_score")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.TargetGradeLevel)
            .HasColumnName("target_grade_level");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // Relationships
        builder.HasMany(x => x.Results)
            .WithOne(x => x.Exam)
            .HasForeignKey(x => x.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.InstitutionId)
            .HasDatabaseName("ix_exams_institution_id");

        builder.HasIndex(x => x.CreatedByTeacherId)
            .HasDatabaseName("ix_exams_teacher_id");

        builder.HasIndex(x => x.ExamDate)
            .HasDatabaseName("ix_exams_exam_date");

        builder.HasIndex(x => x.ExamType)
            .HasDatabaseName("ix_exams_exam_type");
    }
}

/// <summary>
/// ExamResult entity configuration
/// </summary>
public class ExamResultConfiguration : IEntityTypeConfiguration<ExamResult>
{
    public void Configure(EntityTypeBuilder<ExamResult> builder)
    {
        builder.ToTable("exam_results");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.ExamId)
            .HasColumnName("exam_id")
            .IsRequired();

        builder.Property(x => x.StudentId)
            .HasColumnName("student_id")
            .IsRequired();

        builder.Property(x => x.Score)
            .HasColumnName("score")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.CorrectAnswers)
            .HasColumnName("correct_answers");

        builder.Property(x => x.WrongAnswers)
            .HasColumnName("wrong_answers");

        builder.Property(x => x.EmptyAnswers)
            .HasColumnName("empty_answers");

        builder.Property(x => x.SubjectScoresJson)
            .HasColumnName("subject_scores")
            .HasColumnType("jsonb");

        builder.Property(x => x.Ranking)
            .HasColumnName("ranking");

        builder.Property(x => x.TeacherNotes)
            .HasColumnName("teacher_notes")
            .HasColumnType("text");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("ix_exam_results_student_id");

        builder.HasIndex(x => new { x.ExamId, x.StudentId })
            .HasDatabaseName("ix_exam_results_exam_student")
            .IsUnique();
    }
}
