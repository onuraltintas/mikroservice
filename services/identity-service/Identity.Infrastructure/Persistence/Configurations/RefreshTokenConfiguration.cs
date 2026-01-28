using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Token)
            .HasMaxLength(200)
            .IsRequired(); // Use HasIndex if lookups by Token are frequent

        builder.Property(x => x.CreatedByIp)
            .HasMaxLength(50);
            
        builder.Property(x => x.RevokedByIp)
            .HasMaxLength(50);

        builder.Property(x => x.ReasonRevoked)
            .HasMaxLength(250);

        // Configure foreign key
        builder.HasOne<User>()
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
