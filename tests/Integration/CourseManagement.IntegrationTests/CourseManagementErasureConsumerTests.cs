using CourseManagement.Domain;
using CourseManagement.Infrastructure.Privacy;
using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CourseManagement.IntegrationTests;

public sealed class CourseManagementErasureConsumerTests
{
    [Theory]
    [InlineData(PersonalDataScope.Account, 1)]
    [InlineData(PersonalDataScope.Coaching, 0)]
    [InlineData(PersonalDataScope.SpeedReading, 0)]
    public async Task Assessment_consumer_processes_only_account_scope(PersonalDataScope scope, int expectedCalls)
    {
        var service = new StubErasureService();
        var services = new ServiceCollection();
        services.AddSingleton<ICourseManagementAccountErasureService>(service);
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.SetTestTimeouts(testInactivityTimeout: TimeSpan.FromSeconds(5));
            configurator.AddConsumer<CourseManagementErasureAssessmentRequestedConsumer>();
            configurator.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        });

        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var requestId = Guid.NewGuid();
            var subjectUserId = Guid.NewGuid();
            await harness.Bus.Publish(new PersonalDataErasureAssessmentRequestedV1(
                Guid.NewGuid(), requestId, subjectUserId, DateTime.UtcNow, DryRun: true, Scope: scope));

            (await harness.Consumed.Any<PersonalDataErasureAssessmentRequestedV1>()).Should().BeTrue();
            service.AssessmentCallCount.Should().Be(expectedCalls);
            var completions = harness.Published.Select<PersonalDataErasureAssessmentCompletedV1>()
                .Select(context => context.Context.Message)
                .ToArray();
            completions.Should().HaveCount(expectedCalls);
            if (expectedCalls == 1)
            {
                completions[0].RequestId.Should().Be(requestId);
                completions[0].SubjectUserId.Should().Be(subjectUserId);
                completions[0].ServiceName.Should().Be("CourseManagement");
                completions[0].RecordCounts.Should().Contain("assignments", 2);
                completions[0].RecordCounts!.Values.Sum().Should().Be(3);
            }
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Theory]
    [InlineData(PersonalDataScope.Account, 1)]
    [InlineData(PersonalDataScope.Coaching, 0)]
    [InlineData(PersonalDataScope.SpeedReading, 0)]
    public async Task Execution_consumer_processes_only_account_scope(PersonalDataScope scope, int expectedCalls)
    {
        var service = new StubErasureService();
        var services = new ServiceCollection();
        services.AddSingleton<ICourseManagementAccountErasureService>(service);
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.SetTestTimeouts(testInactivityTimeout: TimeSpan.FromSeconds(5));
            configurator.AddConsumer<CourseManagementErasureExecutionRequestedConsumer>();
            configurator.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        });

        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var requestId = Guid.NewGuid();
            await harness.Bus.Publish(new PersonalDataErasureExecutionRequestedV1(
                Guid.NewGuid(), requestId, Guid.NewGuid(), DateTime.UtcNow, scope));

            (await harness.Consumed.Any<PersonalDataErasureExecutionRequestedV1>()).Should().BeTrue();
            service.ExecutionCallCount.Should().Be(expectedCalls);
            var completions = harness.Published.Select<PersonalDataErasureExecutionCompletedV1>()
                .Select(context => context.Context.Message)
                .ToArray();
            completions.Should().HaveCount(expectedCalls);
            if (expectedCalls == 1)
            {
                completions[0].RequestId.Should().Be(requestId);
                completions[0].ServiceName.Should().Be("CourseManagement");
                completions[0].DeletedRecordCount.Should().Be(4);
            }
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class StubErasureService : ICourseManagementAccountErasureService
    {
        public int AssessmentCallCount { get; private set; }
        public int ExecutionCallCount { get; private set; }

        public Task<CourseManagementErasureAssessment> AssessAsync(
            PersonalDataErasureAssessmentRequestedV1 message, CancellationToken cancellationToken)
        {
            AssessmentCallCount++;
            return Task.FromResult(CourseManagementErasureAssessment.Create(
                message.RequestId, message.SubjectUserId,
                new Dictionary<string, int> { ["assignments"] = 2, ["students"] = 1 },
                CourseManagementLegalHoldStatus.NoActiveHold, DateTimeOffset.UtcNow));
        }

        public Task<CourseManagementErasureExecution> ExecuteAsync(
            PersonalDataErasureExecutionRequestedV1 message, CancellationToken cancellationToken)
        {
            ExecutionCallCount++;
            return Task.FromResult(CourseManagementErasureExecution.Complete(
                message.RequestId, deletedRecordCount: 4, DateTimeOffset.UtcNow));
        }
    }
}
