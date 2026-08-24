using EduPlatform.Shared.Infrastructure.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace SpeedReading.Infrastructure.Legacy;

public sealed class SpeedReadingAdminAuditWriter(IServiceScopeFactory scopeFactory) : IAdminAuditWriter
{
    public async Task WriteAsync(AdminAuditRecord record, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SpeedReadingDbContext>();
        await dbContext.AdminAuditRecords.AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
