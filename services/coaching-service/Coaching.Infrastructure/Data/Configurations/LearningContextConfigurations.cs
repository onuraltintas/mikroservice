using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class CurriculumVersionConfiguration : IEntityTypeConfiguration<CurriculumVersion>
{
    public void Configure(EntityTypeBuilder<CurriculumVersion> b)
    {
        b.ToTable("curriculum_versions", t => t.HasCheckConstraint("ck_curriculum_years", "\"ValidFromYear\" BETWEEN 2000 AND 2200 AND \"ValidToYear\" BETWEEN \"ValidFromYear\" AND 2200"));
        b.HasKey(x => x.Id); b.HasIndex(x => x.Code).IsUnique(); b.Property(x => x.Version).IsConcurrencyToken();
        b.Property(x => x.Code).HasMaxLength(100); b.Property(x => x.Name).HasMaxLength(300);
        b.Property(x => x.VersionLabel).HasMaxLength(100); b.Property(x => x.Source).HasMaxLength(300); b.Property(x => x.SourceUrl).HasMaxLength(1000);
    }
}
public sealed class StudentLearningContextConfiguration : IEntityTypeConfiguration<StudentLearningContext>
{
    public void Configure(EntityTypeBuilder<StudentLearningContext> b)
    {
        b.ToTable("student_learning_contexts", t =>
        {
            t.HasCheckConstraint("ck_learning_year", "\"AcademicYearStart\" BETWEEN 2000 AND 2200");
            t.HasCheckConstraint("ck_learning_purpose", "\"Purpose\" BETWEEN 0 AND 4");
        });
        b.HasKey(x => x.Id); b.HasIndex(x => x.StudentUserId).IsUnique(); b.Property(x => x.Version).IsConcurrencyToken();
        b.Property(x => x.PrimaryScoreType).HasMaxLength(10); b.Property(x => x.ForeignLanguage).HasMaxLength(30);
        b.HasOne<CoachingStudentProfile>().WithMany().HasForeignKey(x => x.StudentUserId).HasPrincipalKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CurriculumVersion>().WithMany().HasForeignKey(x => x.CurriculumVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.CoursePreferences).WithOne().HasForeignKey(x => x.ContextId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.TopicStates).WithOne().HasForeignKey(x => x.ContextId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class StudentCoursePreferenceConfiguration : IEntityTypeConfiguration<StudentCoursePreference>
{
    public void Configure(EntityTypeBuilder<StudentCoursePreference> b)
    {
        b.ToTable("student_course_preferences", t => t.HasCheckConstraint("ck_course_priority", "\"Priority\" BETWEEN 1 AND 5 AND (\"SelfReportedDifficulty\" IS NULL OR \"SelfReportedDifficulty\" BETWEEN 1 AND 5)"));
        b.HasKey(x => new { x.ContextId, x.LessonId });
        b.HasOne<StudyCatalogLesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class StudentTopicStateConfiguration : IEntityTypeConfiguration<StudentTopicState>
{
    public void Configure(EntityTypeBuilder<StudentTopicState> b)
    {
        b.ToTable("student_topic_states", t => t.HasCheckConstraint("ck_topic_declaration", "\"DeclaredStatus\" BETWEEN 0 AND 2"));
        b.HasKey(x => new { x.ContextId, x.TopicId }); b.Property(x => x.Source).HasMaxLength(30);
        b.HasOne<StudyCatalogTopic>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
    }
}
