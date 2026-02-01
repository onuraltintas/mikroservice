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
    public DbSet<SupportRequest> SupportRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Notification Item
        modelBuilder.Entity<NotificationItem>().HasKey(x => x.Id);
        modelBuilder.Entity<NotificationItem>().Property(x => x.UserId).IsRequired();

        // Email Template
        modelBuilder.Entity<EmailTemplate>().HasKey(x => x.Id);
        modelBuilder.Entity<EmailTemplate>().HasIndex(x => x.TemplateName).IsUnique();

        // Support Request
        modelBuilder.Entity<SupportRequest>().HasKey(x => x.Id);

        // Seed data is now handled by NotificationDbContextSeeder via external files.
        // See: Infrastructure/Seed/seeds.json

        // MassTransit Outbox Pattern
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
