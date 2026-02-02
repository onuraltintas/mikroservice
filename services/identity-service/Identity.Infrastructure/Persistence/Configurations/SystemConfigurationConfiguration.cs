using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public class SystemConfigurationConfiguration : IEntityTypeConfiguration<SystemConfiguration>
{
    public void Configure(EntityTypeBuilder<SystemConfiguration> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Value)
            .IsRequired(); // Value can be empty string but not null usually, or allow null? Domain says string.Empty.

        builder.Property(c => c.Group)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(c => c.DataType)
            .HasConversion<string>(); // Store enum as string for readability

        builder.HasIndex(c => c.Key)
            .IsUnique();
            
        builder.ToTable("Configurations");
    }
}
