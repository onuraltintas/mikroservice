using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class StudyCatalogExamConfiguration : IEntityTypeConfiguration<StudyCatalogExam>
{
    public void Configure(EntityTypeBuilder<StudyCatalogExam> b)
    {
        b.ToTable("study_catalog_exams", t => t.HasCheckConstraint("ck_catalog_exam_order", "\"DisplayOrder\" >= 0"));
        b.HasKey(x => x.Id); b.HasAlternateKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(10).IsRequired(); b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        var codes = new[] { "LGS", "TYT", "AYT", "YDT", "TDP" };
        var names = new[] { "Liselere Geçiş Sistemi", "Temel Yeterlilik Testi", "Alan Yeterlilik Testleri", "Yabancı Dil Testi", "Temel Destek Paketleri" };
        for (var i = 0; i < codes.Length; i++)
            b.HasData(new { Id = Guid.Parse($"f0ca1000-0000-4000-8000-{i+1:000000000000}"), Code = codes[i], Name = names[i], DisplayOrder = i, IsActive = true });
    }
}

public sealed class StudyCatalogLessonConfiguration : IEntityTypeConfiguration<StudyCatalogLesson>
{
    public void Configure(EntityTypeBuilder<StudyCatalogLesson> builder)
    {
        builder.ToTable("study_catalog_lessons", table => table.HasCheckConstraint("ck_catalog_lesson_grade", "\"GradeNumber\" IS NULL OR \"GradeNumber\" BETWEEN 1 AND 12"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Source).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ExamCode).HasMaxLength(10);
        builder.HasOne<StudyCatalogExam>().WithMany().HasForeignKey(x => x.ExamCode).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.CurriculumVersionId).IsConcurrencyToken();
        builder.HasOne<CurriculumVersion>().WithMany().HasForeignKey(x => x.CurriculumVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.Source, x.SourceId }).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.GradeNumber, x.ExamCode });
    }
}

public sealed class StudyCatalogUnitConfiguration : IEntityTypeConfiguration<StudyCatalogUnit>
{
    public void Configure(EntityTypeBuilder<StudyCatalogUnit> builder)
    {
        builder.ToTable("study_catalog_units", table => table.HasCheckConstraint("ck_catalog_unit_order", "\"DisplayOrder\" IS NULL OR \"DisplayOrder\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.LessonId });
        builder.Property(x => x.Source).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => new { x.Source, x.SourceId }).IsUnique();
        builder.HasOne<StudyCatalogLesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StudyCatalogTopicConfiguration : IEntityTypeConfiguration<StudyCatalogTopic>
{
    public void Configure(EntityTypeBuilder<StudyCatalogTopic> builder)
    {
        builder.ToTable("study_catalog_topics", table =>
        {
            table.HasCheckConstraint("ck_catalog_topic_parent", "\"ParentId\" IS NULL OR \"ParentId\" <> \"Id\"");
            table.HasCheckConstraint("ck_catalog_topic_minutes", "\"EstimatedMinutes\" IS NULL OR \"EstimatedMinutes\" > 0");
            table.HasCheckConstraint("ck_catalog_topic_order", "\"DisplayOrder\" IS NULL OR \"DisplayOrder\" >= 0");
            table.HasCheckConstraint("ck_catalog_topic_yield", "\"YieldWeight\" BETWEEN 1 AND 5");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.UnitId, x.LessonId });
        builder.Property(x => x.Source).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => new { x.Source, x.SourceId }).IsUnique();
        builder.Property(x => x.YieldWeight).HasDefaultValue(3);
        builder.HasIndex(x => new { x.LessonId, x.IsActive, x.DisplayOrder });
        builder.HasOne<StudyCatalogUnit>().WithMany().HasForeignKey(x => new { x.UnitId, x.LessonId })
            .HasPrincipalKey(x => new { x.Id, x.LessonId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StudyCatalogTopic>().WithMany().HasForeignKey(x => new { x.ParentId, x.UnitId, x.LessonId })
            .HasPrincipalKey(x => new { x.Id, x.UnitId, x.LessonId }).OnDelete(DeleteBehavior.Restrict);
    }
}
