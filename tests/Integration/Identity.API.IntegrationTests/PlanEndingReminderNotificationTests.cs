using System.Collections.Concurrent;
using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;

namespace Identity.API.IntegrationTests;

// User decision 15.11.2026: whoever made a plan for part of the way is told when it ends; a teacher is told whose plan it is.
public sealed class PlanEndingReminderNotificationTests
{
    [Fact]
    public async Task TheTeachersReminderNamesTheStudentAndTheStudentsOwnDoesNot()
    {
        var recording = new Recorder(); var services = new ServiceCollection(); services.AddSingleton<ICoachingNotificationDispatcher>(recording);
        services.AddMassTransitTestHarness(c => { c.UseSuiteTimeouts(); c.AddConsumer<CoachingReminderDueConsumer>(); c.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context)); });
        await using var provider = services.BuildServiceProvider(); var harness = provider.GetRequiredService<ITestHarness>(); await harness.Start();
        try
        {
            var teacher = Guid.NewGuid(); var student = Guid.NewGuid(); var plan = Guid.NewGuid(); var own = Guid.NewGuid();
            await harness.Bus.Publish(new CoachingReminderDueEvent(Guid.NewGuid(), teacher, plan, "PlanEnding", DateTime.UtcNow) { SubjectTitle = "Deneme Öğrenci" });
            await harness.Bus.Publish(new CoachingReminderDueEvent(Guid.NewGuid(), student, own, "PlanEnding", DateTime.UtcNow));
            await TestHarnessTimeouts.UntilAsync(() => recording.Calls.Count >= 2);

            var forTeacher = Assert.Single(recording.Calls, call => call.Recipients.Single() == teacher);
            Assert.Equal(("Çalışma planı bitiyor", "StudyPlanEndingReminder", plan.ToString()), (forTeacher.Title, forTeacher.Type, forTeacher.Related));
            Assert.StartsWith("Deneme Öğrenci için hazırladığın çalışma planı bitmek üzere", forTeacher.Message);
            Assert.Contains("Gelecek haftayı ekle", forTeacher.Message);
            var forStudent = Assert.Single(recording.Calls, call => call.Recipients.Single() == student);
            Assert.StartsWith("Hazırladığın çalışma planı bitmek üzere", forStudent.Message);
        }
        finally { await harness.Stop(); }
    }

    private sealed class Recorder : ICoachingNotificationDispatcher
    {
        public ConcurrentQueue<(Guid Id, Guid[] Recipients, string Title, string Message, string Type, string Related)> Calls { get; } = new();
        public Task SendAsync(Guid eventMessageId, IReadOnlyCollection<Guid> recipientIds, string title, string message, string type, string relatedEntityId, CancellationToken cancellationToken)
        {
            Calls.Enqueue((eventMessageId, recipientIds.ToArray(), title, message, type, relatedEntityId));
            return Task.CompletedTask;
        }
    }
}
