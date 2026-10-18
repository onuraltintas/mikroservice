using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Data.Configurations;

public sealed class StudyCatalogTopicOutcomeShareConfiguration : IEntityTypeConfiguration<StudyCatalogTopicOutcomeShare>
{
    public void Configure(EntityTypeBuilder<StudyCatalogTopicOutcomeShare> b)
    {
        b.ToTable("study_catalog_topic_outcome_shares", t => t.HasCheckConstraint("ck_topic_outcome_share_minutes",
            "\"LearningMinutes\" >= 0 AND \"PracticeMinutes\" >= 0 AND \"CheckMinutes\" >= 0 AND (\"LearningMinutes\"::bigint + \"PracticeMinutes\" + \"CheckMinutes\") BETWEEN 1 AND 1440"));
        b.HasKey(x => new { x.TopicId, x.OutcomeId, x.EstimateId });
        b.Property(x => x.ScopeDescription).HasMaxLength(2000).IsRequired();
        b.Ignore(x => x.TotalMinutes);
        b.HasOne<StudyCatalogTopicOutcome>().WithMany().HasForeignKey(x => new { x.TopicId, x.OutcomeId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OutcomeDurationEstimate>().WithMany().HasForeignKey(x => new { x.EstimateId, x.OutcomeId })
            .HasPrincipalKey(x => new { x.Id, x.OutcomeId }).OnDelete(DeleteBehavior.Restrict);
    }
}
