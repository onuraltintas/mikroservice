using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class StudyRoutineProfileConfiguration : IEntityTypeConfiguration<StudyRoutineProfile>
{
    public void Configure(EntityTypeBuilder<StudyRoutineProfile> builder)
    {
        builder.ToTable("study_routine_profiles", table =>
        {
            table.HasCheckConstraint("ck_study_routine_answers_object", "jsonb_typeof(\"AnswersJson\") = 'object'");
            table.HasCheckConstraint("ck_study_routine_schema_version", "\"SchemaVersion\" >= 1");
            table.HasCheckConstraint("ck_study_routine_availability_version", "\"AvailabilityVersion\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.StudentId).IsUnique();
        builder.Property(x => x.AnswersJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
    }
}
