using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.InviterId)
            .IsRequired();

        builder.Property(i => i.InviteeEmail)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(i => i.Type)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(i => i.ExpiresAt)
            .IsRequired();

        builder.Property(i => i.Message)
            .HasMaxLength(500);

        // Indexes for performance
        builder.HasIndex(i => i.InviteeEmail);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => new { i.InviteeEmail, i.Status });
    }
}
