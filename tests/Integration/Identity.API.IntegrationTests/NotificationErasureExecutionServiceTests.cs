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
        context.EmailDeliveries.AddRange(
            EmailDelivery.Create(Guid.NewGuid(), "test", "subject@example.com", "Sil", "Sil", subjectId),
            EmailDelivery.Create(Guid.NewGuid(), "test", "other@example.com", "Koru", "Koru", otherUserId),
            EmailDelivery.Create(Guid.NewGuid(), "test", "subject@example.com", "Anonim", "Koru", null));
        context.SupportRequests.AddRange(
            new SupportRequest(Guid.NewGuid(), "Sil", "Kullanıcı", "subject@example.com", "Sil", "Silinecek mesaj", "subject-key-0001", subjectId),
            new SupportRequest(Guid.NewGuid(), "Koru", "Kullanıcı", "other@example.com", "Koru", "Korunacak mesaj", "other-key-000001", otherUserId),
            new SupportRequest(Guid.NewGuid(), "Anonim", "Kullanıcı", "subject@example.com", "Koru", "Anonim mesaj korunur", "anonymous-key-001", null));
        await context.SaveChangesAsync();
        var request = Request(subjectId);
        var service = new NotificationErasureExecutionService(context, TimeProvider.System);

        var first = await service.ExecuteAsync(request, CancellationToken.None);
        var replay = await service.ExecuteAsync(request, CancellationToken.None);

        first.DeletedRecordCount.Should().Be(3);
        replay.Id.Should().Be(first.Id);
        (await context.Notifications.CountAsync(item => item.UserId == subjectId)).Should().Be(0);
        (await context.Notifications.CountAsync(item => item.UserId == otherUserId)).Should().Be(1);
        (await context.EmailDeliveries.CountAsync(item => item.SubjectUserId == subjectId)).Should().Be(0);
        (await context.EmailDeliveries.CountAsync()).Should().Be(2);
        (await context.SupportRequests.CountAsync(item => item.SubjectUserId == subjectId)).Should().Be(0);
        (await context.SupportRequests.CountAsync()).Should().Be(2);
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
