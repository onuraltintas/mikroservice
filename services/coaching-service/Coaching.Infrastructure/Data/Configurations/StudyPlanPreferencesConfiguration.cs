using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class StudyPlanPreferencesConfiguration : IEntityTypeConfiguration<StudyPlanPreferences>
{
    public void Configure(EntityTypeBuilder<StudyPlanPreferences> builder)
    {
        builder.ToTable("study_plan_preferences", table =>
        {
            table.HasCheckConstraint("ck_study_plan_preferences_exam_focus", "\"ExamFocus\" IS NULL OR \"ExamFocus\" IN ('Tyt', 'Balanced', 'Ayt')");
            table.HasCheckConstraint("ck_study_plan_preferences_tempo", "\"Tempo\" IS NULL OR \"Tempo\" IN ('Relaxed', 'Balanced', 'Intense')");
        });
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.StudentId).IsUnique();
        builder.Property(x => x.ExamFocus).HasMaxLength(16);
        builder.Property(x => x.Tempo).HasMaxLength(16);
        builder.Property(x => x.Version).IsConcurrencyToken();
    }
}
