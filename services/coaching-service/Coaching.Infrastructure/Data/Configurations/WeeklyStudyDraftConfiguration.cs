using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class WeeklyStudyDraftConfiguration:IEntityTypeConfiguration<WeeklyStudyDraft>
{
    public void Configure(EntityTypeBuilder<WeeklyStudyDraft> b)
    {
        b.ToTable("study_weekly_drafts",t=>t.HasCheckConstraint("ck_weekly_draft_slots","jsonb_typeof(\"SlotsJson\") = 'array'"));
        b.HasKey(x=>x.Id);b.HasIndex(x=>x.StudentId).IsUnique();b.Property(x=>x.Version).IsConcurrencyToken();
        b.Property(x=>x.TimeZoneId).HasMaxLength(100).IsRequired();b.Property<string>("SlotsJson").HasColumnType("jsonb").IsRequired();b.Ignore(x=>x.Slots);
    }
}
