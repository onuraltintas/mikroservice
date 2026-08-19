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

    [Fact]
    public async Task EveryApplicationDbContext_ShouldRejectAdminAuditMutationAndDeletion()
    {
        await AssertAppendOnlyAsync<IdentityDbContext>(options => new IdentityDbContext(options));
        await AssertAppendOnlyAsync<NotificationDbContext>(options => new NotificationDbContext(options));
        await AssertAppendOnlyAsync<CoachingDbContext>(options => new CoachingDbContext(options));
    }

    private static async Task AssertAppendOnlyAsync<TContext>(
        Func<DbContextOptions<TContext>, TContext> createContext)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = createContext(options);
        var record = CreateRecord();
        context.Add(record);
        await context.SaveChangesAsync();

        context.Entry(record).Property(nameof(AdminAuditRecord.StatusCode)).CurrentValue = 500;
        var update = () => context.SaveChangesAsync();
        await update.Should().ThrowAsync<InvalidOperationException>();

        context.Entry(record).State = EntityState.Unchanged;
        context.Remove(record);
        var deletion = () => context.SaveChangesAsync();
        await deletion.Should().ThrowAsync<InvalidOperationException>();
    }

    private static AdminAuditRecord CreateRecord() => new(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        "test-service",
        Guid.NewGuid().ToString(),
        "SystemAdmin",
        null,
        "POST",
        "/api/test",
        200,
        Guid.NewGuid().ToString(),
        "127.0.0.1",
        "integration-test");
}
