using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public class UserLoginConfiguration : IEntityTypeConfiguration<UserLogin>
{
    public void Configure(EntityTypeBuilder<UserLogin> builder)
    {
        builder.HasKey(x => x.Id);

        builder.ToTable("UserLogins");

        builder.Property(x => x.LoginProvider)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ProviderKey)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ProviderDisplayName)
            .HasMaxLength(255);

        // Bir sağlayıcıda aynı key sadece bir kez olabilir (Örn: Google'da ID=123 benzersizdir)
        builder.HasIndex(x => new { x.LoginProvider, x.ProviderKey })
            .IsUnique();

        // Foreign Key
        builder.HasOne<User>()
            .WithMany(u => u.Logins)
            .HasForeignKey(ul => ul.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
