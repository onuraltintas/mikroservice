using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class DataSubjectRequestConfiguration : IEntityTypeConfiguration<DataSubjectRequest>
{
    public void Configure(EntityTypeBuilder<DataSubjectRequest> builder)
    {
        builder.ToTable("DataSubjectRequests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.RequestType).HasConversion<string>().HasMaxLength(32);
        builder.Property(request => request.Scope).HasConversion<string>().HasMaxLength(32);
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(request => request.Reason).HasMaxLength(2_000).IsRequired();
        builder.Property(request => request.DecisionReason).HasMaxLength(2_000);
        builder.Property(request => request.FailureReason).HasMaxLength(2_000);
        builder.HasIndex(request => new { request.RequesterUserId, request.SubmittedAt });
        builder.HasIndex(request => new { request.RequesterUserId, request.RequestType, request.Scope })
            .IsUnique()
            .HasFilter("\"Status\" IN ('Submitted', 'IdentityVerified', 'Approved', 'Processing')");
    }
}
