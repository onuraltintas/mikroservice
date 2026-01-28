using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(200);

        builder.Property(x => x.Group)
            .HasMaxLength(50);

        builder.HasIndex(x => x.Key)
            .IsUnique();
        
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
