using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Notification.Domain.Entities;

namespace Notification.Application.Interfaces;

public interface INotificationDbContext
{
    DbSet<EmailTemplate> EmailTemplates { get; }
    DbSet<EmailDelivery> EmailDeliveries { get; }
    DbSet<SupportRequest> SupportRequests { get; }
    DbSet<SupportForwardDelivery> SupportForwardDeliveries { get; }
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
