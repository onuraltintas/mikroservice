using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;

using Notification.Application.Interfaces;
using MassTransit;

namespace Notification.Infrastructure.Persistence;

public class NotificationDbContext : DbContext, INotificationDbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<NotificationItem> Notifications { get; set; }
    public DbSet<EmailTemplate> EmailTemplates { get; set; }
    public DbSet<EmailDelivery> EmailDeliveries { get; set; }
    public DbSet<SupportRequest> SupportRequests { get; set; }
    public DbSet<SupportForwardDelivery> SupportForwardDeliveries { get; set; }

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken)
        => Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Notification Item
        modelBuilder.Entity<NotificationItem>().HasKey(x => x.Id);
        modelBuilder.Entity<NotificationItem>().Property(x => x.UserId).IsRequired();
        modelBuilder.Entity<NotificationItem>()
            .HasIndex(x => new { x.UserId, x.CreatedAt, x.Id });

        // Email Template
        modelBuilder.Entity<EmailTemplate>().HasKey(x => x.Id);
        modelBuilder.Entity<EmailTemplate>().HasIndex(x => x.TemplateName).IsUnique();

        modelBuilder.Entity<EmailDelivery>().HasKey(x => x.Id);
        modelBuilder.Entity<EmailDelivery>()
            .HasIndex(x => new { x.MessageId, x.ConsumerType })
            .IsUnique();
        modelBuilder.Entity<EmailDelivery>()
            .HasIndex(x => new { x.Status, x.NextAttemptAt, x.CreatedAt });
        modelBuilder.Entity<EmailDelivery>()
            .HasIndex(x => new { x.Status, x.LeaseUntil, x.CreatedAt });
        modelBuilder.Entity<EmailDelivery>().Property(x => x.ConsumerType).HasMaxLength(200);
        modelBuilder.Entity<EmailDelivery>().Property(x => x.Recipient).HasMaxLength(320);
        modelBuilder.Entity<EmailDelivery>().Property(x => x.Subject).HasMaxLength(998);
        modelBuilder.Entity<EmailDelivery>().Property(x => x.LeaseToken).IsConcurrencyToken();

        // Support Request
        modelBuilder.Entity<SupportRequest>().HasKey(x => x.Id);
        modelBuilder.Entity<SupportRequest>().Property(x => x.IdempotencyKey).HasMaxLength(128);
        modelBuilder.Entity<SupportRequest>()
            .HasIndex(x => new { x.Email, x.IdempotencyKey })
            .IsUnique();

        modelBuilder.Entity<SupportForwardDelivery>().HasKey(x => x.Id);
        modelBuilder.Entity<SupportForwardDelivery>()
            .HasIndex(x => x.SupportRequestId)
            .IsUnique();
        modelBuilder.Entity<SupportForwardDelivery>()
            .HasIndex(x => new { x.Status, x.NextAttemptAt, x.CreatedAt });
        modelBuilder.Entity<SupportForwardDelivery>()
            .HasIndex(x => new { x.Status, x.LeaseUntil, x.CreatedAt });
        modelBuilder.Entity<SupportForwardDelivery>()
            .Property(x => x.LeaseToken)
            .IsConcurrencyToken();

        // Seed data is now handled by NotificationDbContextSeeder via external files.
        // See: Infrastructure/Seed/seeds.json

        // MassTransit Outbox Pattern
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
