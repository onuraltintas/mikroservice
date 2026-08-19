using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Infrastructure.Middleware;
using FluentAssertions;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Notification.Infrastructure.Persistence;
using IdentityAdminAuditController = Identity.API.Controllers.AdminAuditController;
using NotificationAdminAuditController = Notification.API.Controllers.AdminAuditController;
using CoachingAdminAuditController = Coaching.API.Controllers.AdminAuditController;

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

    [Fact]
    public async Task EveryAuditEndpoint_ShouldFilterOrderAndPaginateItsLocalStore()
    {
        await AssertReadAsync<IdentityDbContext>(
            options => new IdentityDbContext(options),
            context => new IdentityAdminAuditController(context).GetAsync);
        await AssertReadAsync<NotificationDbContext>(
            options => new NotificationDbContext(options),
            context => new NotificationAdminAuditController(context).GetAsync);
        await AssertReadAsync<CoachingDbContext>(
            options => new CoachingDbContext(options),
            context => new CoachingAdminAuditController(context).GetAsync);
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

    private static async Task AssertReadAsync<TContext>(
        Func<DbContextOptions<TContext>, TContext> createContext,
        Func<TContext, Func<AdminAuditQueryParameters, CancellationToken, Task<ActionResult<AdminAuditPage>>>> createEndpoint)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = createContext(options);
        var olderMatch = CreateRecord() with
        {
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            Path = "/api/users/older"
        };
        var newestMatch = CreateRecord() with
        {
            OccurredAt = DateTimeOffset.UtcNow,
            Path = "/api/users/newest"
        };
        var excluded = CreateRecord() with { Path = "/api/roles" };
        context.AddRange(olderMatch, newestMatch, excluded);
        await context.SaveChangesAsync();

        var result = await createEndpoint(context)(new AdminAuditQueryParameters
        {
            Page = 1,
            PageSize = 1,
            Search = "/api/users"
        }, CancellationToken.None);

        var page = result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<AdminAuditPage>().Subject;
        page.TotalCount.Should().Be(2);
        page.Items.Should().ContainSingle().Which.Id.Should().Be(newestMatch.Id);
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
