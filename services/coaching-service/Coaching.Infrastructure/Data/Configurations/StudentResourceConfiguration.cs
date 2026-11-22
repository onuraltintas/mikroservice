using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

/// <summary>KocApp uyumu (22.11.2026): the student's books and sets, with their parts as a JSON array.</summary>
public sealed class StudentResourceConfiguration : IEntityTypeConfiguration<StudentResource>
{
    public void Configure(EntityTypeBuilder<StudentResource> builder)
    {
        builder.ToTable("student_resources", table =>
        {
            table.HasCheckConstraint("ck_student_resource_units", "\"TotalUnits\" BETWEEN 1 AND 10000 AND \"DoneBefore\" BETWEEN 0 AND \"TotalUnits\"");
            table.HasCheckConstraint("ck_student_resource_kind", "\"Kind\" IN ('QuestionBank','TopicBook','PracticeExams','Other') AND \"Unit\" IN ('Page','Test','Question')");
            table.HasCheckConstraint("ck_student_resource_sections", "jsonb_typeof(\"SectionsJson\") = 'array'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Publisher).HasMaxLength(120);
        builder.Property(x => x.LessonName).HasMaxLength(100);
        builder.Property(x => x.ExamCode).HasMaxLength(30);
        builder.Property(x => x.Kind).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SectionsJson).HasColumnType("jsonb").IsRequired();
        builder.Ignore(x => x.Sections);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.StudentId, x.IsArchived });
    }
}
