using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class UserProductAccessConfiguration : IEntityTypeConfiguration<UserProductAccess>
{
    public void Configure(EntityTypeBuilder<UserProductAccess> builder)
    {
        builder.ToTable("UserProductAccesses");
        builder.HasKey(access => access.Id);

        builder.Property(access => access.Product)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(access => access.Source)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(access => access.IsActive)
            .IsRequired();
        builder.Property(access => access.GrantedAt)
            .IsRequired();

        builder.HasIndex(access => new { access.UserId, access.Product })
            .IsUnique();
        builder.HasIndex(access => new { access.Product, access.IsActive });

        builder.HasOne<User>()
            .WithMany(user => user.ProductAccesses)
            .HasForeignKey(access => access.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
