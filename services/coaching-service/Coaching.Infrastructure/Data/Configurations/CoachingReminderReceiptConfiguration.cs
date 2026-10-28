using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class CoachingReminderReceiptConfiguration : IEntityTypeConfiguration<CoachingReminderReceipt>
{
    public void Configure(EntityTypeBuilder<CoachingReminderReceipt> builder)
    {
        builder.ToTable("reminder_receipts", table => table.HasCheckConstraint("ck_reminder_kind", "\"Kind\" IN ('StudyDay', 'StudyOverdue', 'Session', 'FeeOverdue', 'SubscriptionExpiring', 'SubscriptionExpired', 'ParentWeeklySummary', 'AssignmentDue')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasMaxLength(20).IsRequired();
        builder.Property(x => x.OccurrenceKey).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.RecipientUserId);
        builder.HasIndex(x => new { x.Kind, x.SourceId, x.RecipientUserId, x.OccurrenceKey }).IsUnique();
    }
}
