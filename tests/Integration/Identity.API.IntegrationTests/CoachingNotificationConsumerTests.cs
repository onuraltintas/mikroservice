using EduPlatform.Shared.Contracts.Events.Coaching;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;

namespace Identity.API.IntegrationTests;

public sealed class CoachingNotificationConsumerTests
{
    [Fact]
    public async Task CoachingConsumers_MapEventsToExpectedRecipients()
    {
        var teacherId = Guid.NewGuid();
        var firstStudentId = Guid.NewGuid();
        var secondStudentId = Guid.NewGuid();
        var recordingDispatcher = new RecordingDispatcher();
        var services = new ServiceCollection();
        services.AddSingleton<ICoachingNotificationDispatcher>(recordingDispatcher);
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.AddConsumer<AssignmentCreatedConsumer>();
            configurator.AddConsumer<AssignmentSubmittedConsumer>();
            configurator.AddConsumer<AssignmentGradedConsumer>();
            configurator.AddConsumer<ExamResultAddedConsumer>();
            configurator.AddConsumer<SessionScheduledConsumer>();
            configurator.AddConsumer<GoalCreatedConsumer>();
            configurator.UsingInMemory((context, busConfigurator) => busConfigurator.ConfigureEndpoints(context));
        });

        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            await harness.Bus.Publish(new AssignmentCreatedEvent(
                Guid.NewGuid(), teacherId, null, "Assignment", DateTime.UtcNow.AddDays(1),
                new[] { firstStudentId, secondStudentId }));
            await harness.Bus.Publish(new AssignmentSubmittedEvent(
                Guid.NewGuid(), firstStudentId, DateTime.UtcNow, teacherId));
            await harness.Bus.Publish(new AssignmentGradedEvent(
                Guid.NewGuid(), firstStudentId, 85, "Well done", DateTime.UtcNow));
            await harness.Bus.Publish(new ExamResultAddedEvent(
                Guid.NewGuid(), secondStudentId, 92, 3));
            await harness.Bus.Publish(new SessionScheduledEvent(
                Guid.NewGuid(), teacherId, null, new[] { firstStudentId, secondStudentId }, DateTime.UtcNow.AddHours(2)));
            await harness.Bus.Publish(new GoalCreatedEvent(
                Guid.NewGuid(), secondStudentId, teacherId, "Read two books"));

            (await harness.Consumed.Any<AssignmentCreatedEvent>()).Should().BeTrue();
            (await harness.Consumed.Any<AssignmentSubmittedEvent>()).Should().BeTrue();
            (await harness.Consumed.Any<AssignmentGradedEvent>()).Should().BeTrue();
            (await harness.Consumed.Any<ExamResultAddedEvent>()).Should().BeTrue();
            (await harness.Consumed.Any<SessionScheduledEvent>()).Should().BeTrue();
            (await harness.Consumed.Any<GoalCreatedEvent>()).Should().BeTrue();

            for (var attempt = 0; attempt < 50 && recordingDispatcher.Calls.Count < 6; attempt++)
            {
                await Task.Delay(20);
            }

            recordingDispatcher.Calls.Should().HaveCount(6);
            recordingDispatcher.Calls.Single(call => call.Type == "AssignmentSubmitted")
                .RecipientIds.Should().Equal(teacherId);
            recordingDispatcher.Calls.Single(call => call.Type == "AssignmentGraded")
                .RecipientIds.Should().Equal(firstStudentId);
            recordingDispatcher.Calls.Single(call => call.Type == "ExamResultAdded")
                .RecipientIds.Should().Equal(secondStudentId);
            recordingDispatcher.Calls.Single(call => call.Type == "GoalCreated")
                .RecipientIds.Should().Equal(secondStudentId);

            recordingDispatcher.Calls
                .Where(call => call.Type == "AssignmentCreated" || call.Type == "SessionScheduled")
                .SelectMany(call => call.RecipientIds)
                .Should().BeEquivalentTo(new[] { firstStudentId, secondStudentId, firstStudentId, secondStudentId });
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class RecordingDispatcher : ICoachingNotificationDispatcher
    {
        public List<DispatchCall> Calls { get; } = [];

        public Task SendAsync(
            Guid eventMessageId,
            IReadOnlyCollection<Guid> recipientIds,
            string title,
            string message,
            string type,
            string relatedEntityId,
            CancellationToken cancellationToken)
        {
            Calls.Add(new DispatchCall(eventMessageId, recipientIds.ToArray(), title, message, type, relatedEntityId));
            return Task.CompletedTask;
        }
    }

    private sealed record DispatchCall(
        Guid EventMessageId,
        Guid[] RecipientIds,
        string Title,
        string Message,
        string Type,
        string RelatedEntityId);
}
