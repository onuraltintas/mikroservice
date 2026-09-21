using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Configurations;

public sealed class CoachingLegalHoldConfiguration : IEntityTypeConfiguration<CoachingLegalHold>
{
    public void Configure(EntityTypeBuilder<CoachingLegalHold> builder)
    {
        builder.ToTable("CoachingLegalHolds");
        builder.HasKey(hold => hold.Id);
        builder.Property(hold => hold.Reason).HasMaxLength(2_000).IsRequired();
        builder.HasIndex(hold => new { hold.SubjectUserId, hold.ReleasedAt });
    }
}

public sealed class CoachingErasureAssessmentConfiguration : IEntityTypeConfiguration<CoachingErasureAssessment>
{
    public void Configure(EntityTypeBuilder<CoachingErasureAssessment> builder)
    {
        builder.ToTable("CoachingErasureAssessments");
        builder.HasKey(assessment => assessment.Id);
        builder.HasIndex(assessment => assessment.RequestId).IsUnique();
        builder.HasIndex(assessment => new { assessment.SubjectUserId, assessment.AssessedAt });
    }
}

public sealed class CoachingErasureExecutionConfiguration : IEntityTypeConfiguration<CoachingErasureExecution>
{
    public void Configure(EntityTypeBuilder<CoachingErasureExecution> builder)
    {
        builder.ToTable("CoachingErasureExecutions");
        builder.HasKey(execution => execution.Id);
        builder.HasIndex(execution => execution.RequestId).IsUnique();
        builder.HasIndex(execution => execution.CompletedAt);
    }
}
