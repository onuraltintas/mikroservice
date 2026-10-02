using Coaching.Application.Consumers;
using Coaching.Application.Privacy;
using Coaching.Domain.Entities;
using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

public sealed class CoachingErasureAssessmentConsumerTests
{
    [Fact]
    public async Task Consumer_ShouldIgnoreSpeedReadingOnlyRequest()
    {
        var service = new StubAssessmentService(Guid.NewGuid(), Guid.NewGuid());
        var services = new ServiceCollection();
        services.AddSingleton<ICoachingErasureAssessmentService>(service);
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.SetTestTimeouts(testInactivityTimeout: TimeSpan.FromSeconds(5));
            configurator.AddConsumer<PersonalDataErasureAssessmentRequestedConsumer>();
            configurator.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        });

        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new PersonalDataErasureAssessmentRequestedV1(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow,
                DryRun: true, Scope: PersonalDataScope.SpeedReading));

            (await harness.Consumed.Any<PersonalDataErasureAssessmentRequestedV1>()).Should().BeTrue();
            service.CallCount.Should().Be(0);
            (await harness.Published.Any<PersonalDataErasureAssessmentCompletedV1>()).Should().BeFalse();
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consumer_ShouldPublishCoachingDryRunResult()
    {
        var requestId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddSingleton<ICoachingErasureAssessmentService>(
            new StubAssessmentService(requestId, studentId));
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.SetTestTimeouts(testInactivityTimeout: TimeSpan.FromSeconds(5));
            configurator.AddConsumer<PersonalDataErasureAssessmentRequestedConsumer>();
            configurator.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        });

        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new PersonalDataErasureAssessmentRequestedV1(
                requestId, requestId, studentId, DateTime.UtcNow, DryRun: true));

            (await harness.Consumed.Any<PersonalDataErasureAssessmentRequestedV1>()).Should().BeTrue();
            var published = harness.Published
                .Select<PersonalDataErasureAssessmentCompletedV1>()
                .Select(context => context.Context.Message)
                .Should()
                .ContainSingle()
                .Subject;
            published.RequestId.Should().Be(requestId);
            published.ServiceName.Should().Be("Coaching");
            published.CanProceed.Should().BeTrue();
            published.RecordCounts.Should().NotBeNull();
            published.RecordCounts!["StudyPlanning"].Should().Be(4);
            published.RecordCounts.Values.Sum().Should().Be(15);
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class StubAssessmentService(Guid requestId, Guid studentId)
        : ICoachingErasureAssessmentService
    {
        public int CallCount { get; private set; }

        public Task<CoachingErasureAssessment> AssessAsync(
            PersonalDataErasureAssessmentRequestedV1 message,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(CoachingErasureAssessment.Create(
                requestId, studentId, true, false,
                assignmentCount: 1,
                attachmentCount: 0,
                examResultCount: 2,
                goalCount: 3,
                sessionCount: 4,
                agreementCount: 1,
                DateTime.UtcNow, studyPlanningRecordCount: 4));
        }
    }
}
