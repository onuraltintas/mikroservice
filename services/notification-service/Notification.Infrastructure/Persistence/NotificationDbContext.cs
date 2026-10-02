using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;

using Notification.Application.Interfaces;
using MassTransit;
using EduPlatform.Shared.Infrastructure.Middleware;

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
    public DbSet<AdminAuditRecord> AdminAuditRecords => Set<AdminAuditRecord>();
    public DbSet<NotificationErasureExecutionReceipt> ErasureExecutions => Set<NotificationErasureExecutionReceipt>();
    public DbSet<NotificationErasedRecipient> ErasedRecipients => Set<NotificationErasedRecipient>();

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken)
        => Database.BeginTransactionAsync(cancellationToken);

    public async Task<bool> LockAndCheckErasedRecipientAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Recipient is required.");
        if (Database.IsRelational())
        {
            if (Database.CurrentTransaction is null) throw new InvalidOperationException("A recipient transaction is required.");
            var key = $"notification-recipient:{userId:N}";
            await Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
        }
        return await ErasedRecipients.AnyAsync(x => x.UserId == userId, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureAdminAudit(modelBuilder);
        modelBuilder.Entity<NotificationErasedRecipient>(entity =>
        {
            entity.ToTable("PrivacyErasedRecipients");
            entity.HasKey(x => x.UserId);
        });
        modelBuilder.Entity<NotificationErasureExecutionReceipt>(entity =>
        {
            entity.ToTable("PrivacyErasureExecutions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.RequestId).IsUnique();
            entity.HasIndex(item => item.CompletedAt);
        });

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
        modelBuilder.Entity<EmailDelivery>().HasIndex(x => x.SubjectUserId);
        modelBuilder.Entity<EmailDelivery>().Property(x => x.Recipient).HasMaxLength(320);
        modelBuilder.Entity<EmailDelivery>().Property(x => x.Subject).HasMaxLength(998);
        modelBuilder.Entity<EmailDelivery>().Property(x => x.LeaseToken).IsConcurrencyToken();

        // Support Request
        modelBuilder.Entity<SupportRequest>().HasKey(x => x.Id);
        modelBuilder.Entity<SupportRequest>().Property(x => x.IdempotencyKey).HasMaxLength(128);
        modelBuilder.Entity<SupportRequest>().HasIndex(x => x.SubjectUserId);
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

    private static void ConfigureAdminAudit(ModelBuilder modelBuilder)
    {
        var audit = modelBuilder.Entity<AdminAuditRecord>();
        audit.ToTable("AdminAuditRecords");
        audit.HasKey(record => record.Id);
        audit.Property(record => record.ServiceName).HasMaxLength(150);
        audit.Property(record => record.ActorUserId).HasMaxLength(100);
        audit.Property(record => record.ActorRoles).HasMaxLength(500);
        audit.Property(record => record.TenantId).HasMaxLength(100);
        audit.Property(record => record.HttpMethod).HasMaxLength(10);
        audit.Property(record => record.Path).HasMaxLength(500);
        audit.Property(record => record.CorrelationId).HasMaxLength(100);
        audit.Property(record => record.ClientIp).HasMaxLength(64);
        audit.Property(record => record.UserAgent).HasMaxLength(256);
        audit.Property(record => record.Action).HasMaxLength(32);
        audit.Property(record => record.ResourceType).HasMaxLength(100);
        audit.Property(record => record.ResourceId).HasMaxLength(100);
        audit.Property(record => record.ChangedFieldsJson).HasMaxLength(2_000);
        audit.HasIndex(record => new { record.OccurredAt, record.Id });
        audit.HasIndex(record => new { record.ActorUserId, record.OccurredAt });
        audit.HasIndex(record => new { record.ResourceType, record.ResourceId, record.OccurredAt });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ChangeTracker.Entries<NotificationErasedRecipient>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Erased recipient guards are immutable.");
        if (ChangeTracker.Entries<AdminAuditRecord>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Admin audit records are append-only.");
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
