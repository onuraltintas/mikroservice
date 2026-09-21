using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class DataSubjectRequestAssessmentResultConfiguration : IEntityTypeConfiguration<DataSubjectRequestAssessmentResult>
{
    public void Configure(EntityTypeBuilder<DataSubjectRequestAssessmentResult> builder)
    {
        builder.ToTable("DataSubjectRequestAssessmentResults");
        builder.HasKey(result => result.Id);
        builder.Property(result => result.ServiceName).HasMaxLength(100).IsRequired();
        builder.Property(result => result.RecordCounts).HasColumnType("jsonb");
        builder.Ignore(result => result.TotalRecordCount);
        builder.HasIndex(result => new { result.RequestId, result.ServiceName }).IsUnique();
        builder.HasIndex(result => new { result.SubjectUserId, result.AssessedAt });
    }
}
