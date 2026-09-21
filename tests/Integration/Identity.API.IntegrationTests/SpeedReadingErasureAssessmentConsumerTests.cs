using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using SpeedReading.Application.Privacy;

namespace Identity.API.IntegrationTests;

public sealed class SpeedReadingErasureAssessmentConsumerTests
{
    [Theory]
    [InlineData(PersonalDataScope.SpeedReading, 1)]
    [InlineData(PersonalDataScope.Account, 1)]
    [InlineData(PersonalDataScope.Coaching, 0)]
    public async Task Consumer_ShouldProcessOnlyApplicableScopes(
        PersonalDataScope scope,
        int expectedCalls)
    {
        var service = new StubAssessmentService();
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedReadingErasureAssessmentService>(service);
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.SetTestTimeouts(testInactivityTimeout: TimeSpan.FromSeconds(5));
            configurator.AddConsumer<SpeedReadingErasureAssessmentRequestedConsumer>();
            configurator.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        });

        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var requestId = Guid.NewGuid();
            await harness.Bus.Publish(new PersonalDataErasureAssessmentRequestedV1(
                Guid.NewGuid(), requestId, Guid.NewGuid(), DateTime.UtcNow,
                DryRun: true, Scope: scope));

            (await harness.Consumed.Any<PersonalDataErasureAssessmentRequestedV1>()).Should().BeTrue();
            service.CallCount.Should().Be(expectedCalls);
            var results = harness.Published.Select<PersonalDataErasureAssessmentCompletedV1>()
                .Select(item => item.Context.Message).ToArray();
            results.Should().HaveCount(expectedCalls);
            if (expectedCalls == 1)
            {
                results[0].ServiceName.Should().Be("SpeedReading");
                results[0].RecordCounts.Should().Contain("profiles", 1);
            }
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class StubAssessmentService : ISpeedReadingErasureAssessmentService
    {
        public int CallCount { get; private set; }

        public Task<SpeedReadingErasureAssessment> AssessAsync(
            PersonalDataErasureAssessmentRequestedV1 message,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new SpeedReadingErasureAssessment(
                message.RequestId,
                message.SubjectUserId,
                CanProceed: true,
                HasActiveLegalHold: false,
                new Dictionary<string, int> { ["profiles"] = 1 },
                DateTime.UtcNow));
        }
    }
}
