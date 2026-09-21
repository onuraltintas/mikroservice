using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;

namespace Identity.API.IntegrationTests;

public sealed class NotificationErasureExecutionServiceTests
{
    [Fact]
    public async Task Execute_ShouldDeleteOnlySubjectNotificationsAndRemainIdempotent()
    {
        await using var context = CreateContext();
        var subjectId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        context.Notifications.AddRange(
            NotificationItem.Create(subjectId, "Sil", "Sil", "Info"),
            NotificationItem.Create(otherUserId, "Koru", "Koru", "Info"));
        await context.SaveChangesAsync();
        var request = Request(subjectId);
        var service = new NotificationErasureExecutionService(context, TimeProvider.System);

        var first = await service.ExecuteAsync(request, CancellationToken.None);
        var replay = await service.ExecuteAsync(request, CancellationToken.None);

        first.DeletedRecordCount.Should().Be(1);
        replay.Id.Should().Be(first.Id);
        (await context.Notifications.CountAsync(item => item.UserId == subjectId)).Should().Be(0);
        (await context.Notifications.CountAsync(item => item.UserId == otherUserId)).Should().Be(1);
        (await context.ErasureExecutions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Execute_ShouldRejectProductOnlyScopes()
    {
        await using var context = CreateContext();
        var service = new NotificationErasureExecutionService(context, TimeProvider.System);
        var request = Request(Guid.NewGuid()) with { Scope = PersonalDataScope.Coaching };

        var action = () => service.ExecuteAsync(request, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*scope*");
    }

    private static PersonalDataErasureExecutionRequestedV1 Request(Guid subjectId)
    {
        var requestId = Guid.NewGuid();
        return new PersonalDataErasureExecutionRequestedV1(
            requestId, requestId, subjectId, DateTime.UtcNow, PersonalDataScope.Account);
    }

    private static NotificationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new NotificationDbContext(options);
    }
}
