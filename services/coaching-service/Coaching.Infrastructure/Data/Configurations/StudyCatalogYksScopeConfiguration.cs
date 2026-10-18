using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class StudyCatalogYksScopeConfiguration:IEntityTypeConfiguration<StudyCatalogYksScope>
{
    public void Configure(EntityTypeBuilder<StudyCatalogYksScope> b)
    {
        b.ToTable("study_catalog_yks_scopes",t=>t.HasCheckConstraint("ck_yks_scope",
            "\"ExamCode\" IN ('TYT','AYT','YDT') AND \"ScoreType\" IN ('TYT','SAY','EA','SOZ','DIL') AND (\"ExamCode\" <> 'AYT' OR \"ScoreType\" IN ('SAY','EA','SOZ')) AND ((\"ExamCode\" = 'YDT' AND \"ScoreType\" = 'DIL' AND \"ForeignLanguage\" IS NOT NULL AND \"ForeignLanguage\" = 'English') OR (\"ExamCode\" <> 'YDT' AND \"ForeignLanguage\" IS NULL))"));
        b.HasKey(x=>new{x.TopicId,x.ScoreType,x.TestCode});
        b.Property(x=>x.ExamCode).HasMaxLength(10);b.Property(x=>x.ScoreType).HasMaxLength(10);
        b.Property(x=>x.TestCode).HasMaxLength(40);b.Property(x=>x.ForeignLanguage).HasMaxLength(30);b.Property(x=>x.Evidence).HasMaxLength(2000);
        b.HasIndex(x=>new{x.ScoreType,x.ExamCode,x.ForeignLanguage});
        b.HasOne<StudyCatalogTopic>().WithMany().HasForeignKey(x=>x.TopicId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StudyCatalogExam>().WithMany().HasForeignKey(x=>x.ExamCode).HasPrincipalKey(x=>x.Code).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OfficialCurriculumLessonBindingConfiguration:IEntityTypeConfiguration<OfficialCurriculumLessonBinding>
{
    public void Configure(EntityTypeBuilder<OfficialCurriculumLessonBinding> b)
    {
        b.ToTable("official_curriculum_lesson_bindings");b.HasKey(x=>new{x.EditionId,x.LessonId});
        b.HasOne<OfficialCurriculumEdition>().WithMany().HasForeignKey(x=>x.EditionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StudyCatalogLesson>().WithMany().HasForeignKey(x=>x.LessonId).OnDelete(DeleteBehavior.Restrict);
    }
}
