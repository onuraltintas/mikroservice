using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class StudyAvailabilityConfiguration : IEntityTypeConfiguration<StudyAvailability>
{
    public void Configure(EntityTypeBuilder<StudyAvailability> builder)
    {
        builder.ToTable("study_availability", table => table.HasCheckConstraint(
            "ck_study_availability_array", "jsonb_typeof(\"WindowsJson\") = 'array'"));
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.StudentId).IsUnique();
        builder.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property<string>("WindowsJson").HasColumnType("jsonb").IsRequired();
        builder.Ignore(x => x.Windows);
        builder.Property(x => x.Version).IsConcurrencyToken();
    }
}
