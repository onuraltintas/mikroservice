using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Privacy;

namespace Identity.API.IntegrationTests;

public sealed class NotificationErasureAssessmentConsumerTests
{
    [Theory]
    [InlineData(PersonalDataScope.Account, 1)]
    [InlineData(PersonalDataScope.Coaching, 0)]
    [InlineData(PersonalDataScope.SpeedReading, 0)]
    public async Task Consumer_ShouldProcessOnlyAccountScope(PersonalDataScope scope, int expectedCalls)
    {
        var service = new StubService();
        var services = new ServiceCollection();
        services.AddSingleton<INotificationErasureAssessmentService>(service);
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.AddConsumer<NotificationErasureAssessmentRequestedConsumer>();
            configurator.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        });
        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new PersonalDataErasureAssessmentRequestedV1(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow,
                DryRun: true, Scope: scope));

            (await harness.Consumed.Any<PersonalDataErasureAssessmentRequestedV1>()).Should().BeTrue();
            service.CallCount.Should().Be(expectedCalls);
            (await harness.Published.Any<PersonalDataErasureAssessmentCompletedV1>())
                .Should().Be(expectedCalls == 1);
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class StubService : INotificationErasureAssessmentService
    {
        public int CallCount { get; private set; }
        public Task<NotificationErasureAssessment> AssessAsync(
            PersonalDataErasureAssessmentRequestedV1 message,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new NotificationErasureAssessment(
                message.RequestId, message.SubjectUserId, true,
                new Dictionary<string, int> { ["notifications"] = 2 }, DateTime.UtcNow));
        }
    }
}
