using System.Collections.Concurrent;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Services;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;

namespace Identity.API.IntegrationTests;

public sealed class CourseManagementNotificationConsumerTests
{
    [Fact]
    public async Task CourseManagementEvents_MapToOwnRecipientsAndProductScopedTypes()
    {
        var firstStudent = Guid.NewGuid();
        var secondStudent = Guid.NewGuid();
        var teacher = Guid.NewGuid();
        var dispatcher = new RecordingDispatcher();
        var services = new ServiceCollection();
        services.AddSingleton<ICourseManagementNotificationDispatcher>(dispatcher);
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.SetTestTimeouts(testInactivityTimeout: TimeSpan.FromSeconds(5));
            configurator.AddConsumer<CourseManagementLessonScheduledConsumer>();
            configurator.AddConsumer<CourseManagementLessonRescheduledConsumer>();
            configurator.AddConsumer<CourseManagementLessonCancelledConsumer>();
            configurator.AddConsumer<CourseManagementAssignmentPublishedConsumer>();
            configurator.AddConsumer<CourseManagementAssignmentSubmittedConsumer>();
            configurator.AddConsumer<CourseManagementAssignmentReviewedConsumer>();
            configurator.AddConsumer<CourseManagementPaymentRecordedConsumer>();
            configurator.AddConsumer<CourseManagementReminderDueConsumer>();
            configurator.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        });

        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            await harness.Bus.Publish(new CourseManagementLessonScheduledV1(
                Guid.NewGuid(), Guid.NewGuid(), [firstStudent, secondStudent], DateTimeOffset.UtcNow.AddDays(1)));
            await harness.Bus.Publish(new CourseManagementLessonRescheduledV1(
                Guid.NewGuid(), Guid.NewGuid(), [firstStudent], DateTimeOffset.UtcNow.AddDays(2)));
            await harness.Bus.Publish(new CourseManagementLessonCancelledV1(
                Guid.NewGuid(), Guid.NewGuid(), [firstStudent, secondStudent], DateTimeOffset.UtcNow.AddDays(2)));
            await harness.Bus.Publish(new CourseManagementAssignmentPublishedV1(
                Guid.NewGuid(), Guid.NewGuid(), "Kesirler", DateTimeOffset.UtcNow.AddDays(3), [firstStudent, secondStudent]));
            await harness.Bus.Publish(new CourseManagementAssignmentSubmittedV1(Guid.NewGuid(), Guid.NewGuid(), teacher));
            await harness.Bus.Publish(new CourseManagementAssignmentReviewedV1(Guid.NewGuid(), Guid.NewGuid(), firstStudent));
            await harness.Bus.Publish(new CourseManagementPaymentRecordedV1(Guid.NewGuid(), Guid.NewGuid(), secondStudent));
            await harness.Bus.Publish(new CourseManagementReminderDueV1(Guid.NewGuid(), Guid.NewGuid(), secondStudent,
                "InstallmentDue", new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.FromHours(3))));

            (await harness.Consumed.Any<CourseManagementLessonScheduledV1>()).Should().BeTrue();
            (await harness.Consumed.Any<CourseManagementLessonRescheduledV1>()).Should().BeTrue();
            (await harness.Consumed.Any<CourseManagementLessonCancelledV1>()).Should().BeTrue();
            (await harness.Consumed.Any<CourseManagementAssignmentPublishedV1>()).Should().BeTrue();
            (await harness.Consumed.Any<CourseManagementAssignmentSubmittedV1>()).Should().BeTrue();
            (await harness.Consumed.Any<CourseManagementAssignmentReviewedV1>()).Should().BeTrue();
            (await harness.Consumed.Any<CourseManagementPaymentRecordedV1>()).Should().BeTrue();
            (await harness.Consumed.Any<CourseManagementReminderDueV1>()).Should().BeTrue();

            for (var attempt = 0; attempt < 50 && dispatcher.Calls.Count < 8; attempt++)
                await Task.Delay(20);

            dispatcher.Calls.Should().HaveCount(8);
            dispatcher.Calls.Single(call => call.Type == "CourseManagement.LessonScheduled").RecipientIds
                .Should().BeEquivalentTo([firstStudent, secondStudent]);
            dispatcher.Calls.Single(call => call.Type == "CourseManagement.LessonRescheduled").RecipientIds
                .Should().Equal(firstStudent);
            dispatcher.Calls.Single(call => call.Type == "CourseManagement.LessonCancelled").RecipientIds
                .Should().BeEquivalentTo([firstStudent, secondStudent]);
            dispatcher.Calls.Single(call => call.Type == "CourseManagement.AssignmentPublished").RecipientIds
                .Should().BeEquivalentTo([firstStudent, secondStudent]);
            dispatcher.Calls.Single(call => call.Type == "CourseManagement.AssignmentSubmitted").RecipientIds
                .Should().Equal(teacher);
            dispatcher.Calls.Single(call => call.Type == "CourseManagement.AssignmentReviewed").RecipientIds
                .Should().Equal(firstStudent);
            dispatcher.Calls.Single(call => call.Type == "CourseManagement.PaymentRecorded").RecipientIds
                .Should().Equal(secondStudent);
            dispatcher.Calls.Single(call => call.Type == "CourseManagement.PaymentRecorded").Message
                .Should().NotContain("TRY").And.NotContain("₺");
            var dueReminder = dispatcher.Calls.Single(call => call.Type == "CourseManagement.InstallmentDueReminder");
            dueReminder.RecipientIds.Should().Equal(secondStudent);
            dueReminder.RelatedEntityId.Should().BeNull();
            dueReminder.Message.Should().Contain("bugün").And.NotContain("TRY").And.NotContain("₺")
                .And.NotContain("taksit").And.NotContain("ödeme kaydı");
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task ReminderConsumer_RetryAfterPersistence_LeavesOneNotificationForTheRecipient()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new NotificationDbContext(options);
        var notificationService = new FailAfterFirstPersistNotificationService(db);
        var dispatcher = new CourseManagementNotificationDispatcher(notificationService);
        var services = new ServiceCollection();
        services.AddSingleton<ICourseManagementNotificationDispatcher>(dispatcher);
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.SetTestTimeouts(testInactivityTimeout: TimeSpan.FromSeconds(5));
            configurator.AddConsumer<CourseManagementReminderDueConsumer>();
            configurator.UsingInMemory((context, bus) =>
            {
                bus.UseMessageRetry(retry => retry.Immediate(1));
                bus.ConfigureEndpoints(context);
            });
        });

        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var messageId = Guid.NewGuid();

        try
        {
            await harness.Bus.Publish(new CourseManagementReminderDueV1(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Lesson", DateTimeOffset.UtcNow.AddDays(1)),
                context => context.MessageId = messageId);

            await notificationService.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

            notificationService.SourceMessageIds.Should().HaveCount(2);
            notificationService.SourceMessageIds.Distinct().Should().ContainSingle();
            (await db.Notifications.CountAsync()).Should().Be(1);
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class RecordingDispatcher : ICourseManagementNotificationDispatcher
    {
        public ConcurrentQueue<DispatchCall> Calls { get; } = new();

        public Task SendAsync(Guid eventMessageId, IReadOnlyCollection<Guid> recipientIds, string title,
            string message, string type, string? relatedEntityId, CancellationToken cancellationToken)
        {
            Calls.Enqueue(new DispatchCall(eventMessageId, recipientIds.ToArray(), title, message, type, relatedEntityId));
            return Task.CompletedTask;
        }
    }

    private sealed record DispatchCall(Guid EventMessageId, Guid[] RecipientIds, string Title,
        string Message, string Type, string? RelatedEntityId);

    private sealed class FailAfterFirstPersistNotificationService(NotificationDbContext db) : INotificationService
    {
        public ConcurrentQueue<Guid?> SourceMessageIds { get; } = new();
        public TaskCompletionSource<bool> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task SendNotificationAsync(Guid userId, string title, string message, string type,
            string? relatedEntityId = null, Guid? sourceMessageId = null)
        {
            SourceMessageIds.Enqueue(sourceMessageId);
            await NotificationRecipientWrites.PersistAsync(db, NotificationItem.Create(
                userId, title, message, type, relatedEntityId, sourceMessageId));
            if (SourceMessageIds.Count == 1)
                throw new InvalidOperationException("Simulated failure after persisting the notification.");
            Completed.TrySetResult(true);
        }
    }
}
