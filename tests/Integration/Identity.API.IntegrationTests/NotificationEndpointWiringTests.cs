using System.Reflection;
using System.Collections.Concurrent;
using EduPlatform.Shared.Contracts.Events.Coaching;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Notification.API.Messaging;
using Notification.Application.Interfaces;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Production endpoint wiring. Consumer tests use ConfigureEndpoints, which binds every
/// consumer automatically, so they could not notice a consumer that production never
/// bound to a queue. These checks run against the list production actually uses.
/// </summary>
public sealed class NotificationEndpointWiringTests
{
    // Queues that exist in production RabbitMQ today; renaming one would orphan it.
    private static readonly string[] ExistingQueues =
    [
        "invitation-created", "user-registered", "user-email-confirmed", "user-created", "send-notification",
        "user-forgot-password", "coaching-assignment-created", "coaching-assignment-submitted",
        "coaching-assignment-graded", "coaching-exam-result-added", "coaching-session-scheduled",
        "coaching-session-cancelled", "coaching-goal-created", "coaching-study-plan-published",
        "coaching-study-plan-updated", "coaching-weekly-reflection-saved", "coaching-weekly-decision-changed",
        "coaching-reminder-due", "course-management-lesson-scheduled", "course-management-lesson-rescheduled",
        "course-management-lesson-cancelled", "course-management-assignment-published",
        "course-management-assignment-submitted", "course-management-assignment-reviewed",
        "course-management-payment-recorded", "course-management-reminder-due",
        "notification-privacy-erasure-assessment", "notification-privacy-erasure-execution",
    ];

    [Fact]
    public void EveryConsumerInTheApplication_HasExactlyOneQueue()
    {
        var consumers = new[] { typeof(Notification.Application.Consumers.InvitationCreatedConsumer).Assembly, typeof(NotificationReceiveEndpoints).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>)))
            .ToHashSet();

        NotificationReceiveEndpoints.All.Select(endpoint => endpoint.Consumer)
            .Should().OnlyHaveUniqueItems()
            .And.BeEquivalentTo(consumers, "a consumer without a queue never receives a message");
    }

    [Fact]
    public void CourseManagementEndpoints_RunOnlyWhenTheFeatureIsOn()
    {
        var off = NotificationReceiveEndpoints.Active(courseManagementEnabled: false).ToList();
        var on = NotificationReceiveEndpoints.Active(courseManagementEnabled: true).ToList();

        on.Should().BeEquivalentTo(NotificationReceiveEndpoints.All);
        NotificationReceiveEndpoints.All.Where(endpoint => endpoint.CourseManagement).Select(endpoint => endpoint.Queue)
            .Should().HaveCount(8).And.OnlyContain(queue => queue.StartsWith("course-management-"));
        off.Should().NotContain(endpoint => endpoint.Queue.StartsWith("course-management-")
            || endpoint.Consumer.Name.StartsWith("CourseManagement"));
        off.Should().HaveCount(NotificationReceiveEndpoints.All.Count - 8);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void CourseManagementApi_IsMappedOnlyWhenTheFeatureIsOn(bool enabled, bool mapped)
    {
        var application = new Microsoft.AspNetCore.Mvc.ApplicationModels.ApplicationModel();
        foreach (var type in new[] { typeof(Notification.API.Controllers.CourseManagementNotificationsController), typeof(Notification.API.Controllers.SupportController) })
        {
            application.Controllers.Add(new Microsoft.AspNetCore.Mvc.ApplicationModels.ControllerModel(type.GetTypeInfo(), []));
        }

        new CourseManagementFeature.ControllerConvention(enabled).Apply(application);

        application.Controllers.Select(controller => controller.ControllerType.AsType())
            .Contains(typeof(Notification.API.Controllers.CourseManagementNotificationsController)).Should().Be(mapped);
        application.Controllers.Should().Contain(controller => controller.ControllerType == typeof(Notification.API.Controllers.SupportController));
    }

    [Fact]
    public void CourseManagementFeature_IsOffByDefault_AndOnlyTheCourseManagementOverlaysTurnItOn()
    {
        CourseManagementFeature.IsEnabled(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()).Should().BeFalse();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "EduPlatform.sln"))) root = root.Parent!;
        string[] courseManagementOverlays = ["docker-compose.course-management.yml", "docker-compose.course-management.production.yml"];
        foreach (var file in Directory.GetFiles(root.FullName, "docker-compose*.yml"))
        {
            File.ReadAllText(file).Contains("CourseManagement__Enabled").Should()
                .Be(courseManagementOverlays.Contains(Path.GetFileName(file)), Path.GetFileName(file));
        }
    }

    [Fact]
    public void QueueNames_AreUniqueAndKeepTheExistingProductionQueues()
    {
        var queues = NotificationReceiveEndpoints.All.Select(endpoint => endpoint.Queue).ToArray();

        queues.Should().OnlyHaveUniqueItems();
        queues.Should().Contain(ExistingQueues);
        queues.Except(ExistingQueues).Should().BeEquivalentTo(
            ["coaching-assignment-updated", "coaching-exam-updated", "coaching-exam-result-updated",
             "coaching-session-updated", "coaching-goal-updated", "notification-realtime", "user-password-setup-requested", "coaching-subscription-notice", "coaching-assignment-cancelled", "coaching-teacher-link-ended", "coaching-parent-weekly-summary", "coaching-message-received", "invitation-accepted"],
            "five update notifications were registered without a queue; realtime pushes now run after commit; " +
            "the password-setup e-mail no longer rides on user-created");
    }

    [Fact]
    public void ProgramUsesTheEndpointListForRegistrationAndBinding()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EduPlatform.sln"))) directory = directory.Parent;
        var program = File.ReadAllText(Path.Combine(directory!.FullName, "services", "notification-service", "Notification.API", "Program.cs"));

        program.Should().Contain("x.AddNotificationConsumers(courseManagementEnabled);");
        program.Should().Contain("cfg.ConfigureNotificationEndpoints(context, courseManagementEnabled);");
        program.Should().NotContain("AddConsumer<", "consumers are registered only through the endpoint list");
        program.Should().NotContain("ReceiveEndpoint(", "queues are bound only through the endpoint list");
    }

    [Fact]
    public async Task UpdateEvents_ReachTheirConsumersThroughTheProductionQueueList()
    {
        var dispatcher = new RecordingDispatcher();
        var services = new ServiceCollection();
        services.AddSingleton<ICoachingNotificationDispatcher>(dispatcher);
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.UseSuiteTimeouts();
            configurator.AddNotificationConsumers(courseManagementEnabled: false);
            // Same queue list as production; only the EF outbox (needs PostgreSQL) is left out.
            configurator.UsingInMemory((context, bus) =>
            {
                foreach (var endpoint in NotificationReceiveEndpoints.Active(courseManagementEnabled: false))
                    bus.ReceiveEndpoint(endpoint.Queue, e => e.ConfigureConsumer(context, endpoint.Consumer));
            });
        });
        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var teacher = Guid.NewGuid();
            var student = Guid.NewGuid();
            await harness.Bus.Publish(new AssignmentUpdatedEvent(Guid.NewGuid(), teacher, null, "Ödev", DateTime.UtcNow.AddDays(2), new[] { student }));
            await harness.Bus.Publish(new ExamUpdatedEvent(Guid.NewGuid(), teacher, null, "Deneme", DateTime.UtcNow.AddDays(3), 100, new[] { student }));
            await harness.Bus.Publish(new ExamResultUpdatedEvent(Guid.NewGuid(), Guid.NewGuid(), student, 95, 2));
            await harness.Bus.Publish(new SessionUpdatedEvent(Guid.NewGuid(), teacher, null, new[] { student }, DateTime.UtcNow.AddHours(3)));
            await harness.Bus.Publish(new GoalUpdatedEvent(Guid.NewGuid(), student, teacher, "Üç kitap oku"));

            await TestHarnessTimeouts.UntilAsync(() => dispatcher.Calls.Count >= 5);

            dispatcher.Calls.Should().HaveCount(5, "each update event is delivered to exactly one bound consumer");
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class RecordingDispatcher : ICoachingNotificationDispatcher
    {
        public ConcurrentQueue<string> Calls { get; } = new();

        public Task SendAsync(Guid eventMessageId, IReadOnlyCollection<Guid> recipientIds, string title, string message,
            string type, string relatedEntityId, CancellationToken cancellationToken)
        {
            Calls.Enqueue(type);
            return Task.CompletedTask;
        }
    }
}
