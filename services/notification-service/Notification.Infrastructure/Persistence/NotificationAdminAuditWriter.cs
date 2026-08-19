using EduPlatform.Shared.Infrastructure.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace Notification.Infrastructure.Persistence;

public sealed class NotificationAdminAuditWriter(IServiceScopeFactory scopeFactory) : IAdminAuditWriter
{
    public async Task WriteAsync(AdminAuditRecord record, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await dbContext.AdminAuditRecords.AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
