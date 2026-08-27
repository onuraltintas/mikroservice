using EduPlatform.Shared.Infrastructure.Middleware;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingAdminAuditWriter(OwnedSpeedReadingDbContext db) : IAdminAuditWriter
{
    public async Task WriteAsync(AdminAuditRecord record, CancellationToken cancellationToken)
    {
        await db.AdminAuditRecords.AddAsync(record, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
