using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;

namespace Notification.Application.Interfaces;

public interface INotificationDbContext
{
    DbSet<EmailTemplate> EmailTemplates { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
