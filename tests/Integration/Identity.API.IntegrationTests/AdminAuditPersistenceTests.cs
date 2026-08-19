using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Infrastructure.Middleware;
using FluentAssertions;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Notification.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

public sealed class AdminAuditPersistenceTests
{
    [Fact]
    public void EveryApplicationDbContext_ShouldOwnAnAdminAuditTable()
    {
        using var identity = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        using var notification = new NotificationDbContext(
            new DbContextOptionsBuilder<NotificationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        using var coaching = new CoachingDbContext(
            new DbContextOptionsBuilder<CoachingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        identity.Model.FindEntityType(typeof(AdminAuditRecord)).Should().NotBeNull();
        notification.Model.FindEntityType(typeof(AdminAuditRecord)).Should().NotBeNull();
        coaching.Model.FindEntityType(typeof(AdminAuditRecord)).Should().NotBeNull();
    }
}
