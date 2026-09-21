using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class DataSubjectRequestExecutionResultConfiguration
    : IEntityTypeConfiguration<DataSubjectRequestExecutionResult>
{
    public void Configure(EntityTypeBuilder<DataSubjectRequestExecutionResult> builder)
    {
        builder.ToTable("DataSubjectRequestExecutionResults");
        builder.HasKey(result => result.Id);
        builder.Property(result => result.ServiceName).HasMaxLength(100).IsRequired();
        builder.HasIndex(result => new { result.RequestId, result.ServiceName }).IsUnique();
        builder.HasIndex(result => result.CompletedAt);
    }
}
