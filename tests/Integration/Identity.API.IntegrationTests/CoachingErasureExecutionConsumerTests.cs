using Coaching.Application.Consumers;
using Coaching.Application.Privacy;
using Coaching.Domain.Entities;
using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

public sealed class CoachingErasureExecutionConsumerTests
{
    [Theory]
    [InlineData(PersonalDataScope.Account, 1)]
    [InlineData(PersonalDataScope.Coaching, 1)]
    [InlineData(PersonalDataScope.SpeedReading, 0)]
    public async Task Consumer_ShouldExecuteOnlyScopesContainingCoaching(
        PersonalDataScope scope,
        int expectedCalls)
    {
        var requestId = Guid.NewGuid();
        var service = new StubExecutionService(requestId);
        var services = new ServiceCollection();
        services.AddSingleton<ICoachingErasureExecutionService>(service);
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.SetTestTimeouts(testInactivityTimeout: TimeSpan.FromSeconds(5));
            configurator.AddConsumer<PersonalDataErasureExecutionRequestedConsumer>();
            configurator.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        });

        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new PersonalDataErasureExecutionRequestedV1(
                requestId, requestId, Guid.NewGuid(), DateTime.UtcNow, scope));

            (await harness.Consumed.Any<PersonalDataErasureExecutionRequestedV1>()).Should().BeTrue();
            service.CallCount.Should().Be(expectedCalls);
            var completions = harness.Published
                .Select<PersonalDataErasureExecutionCompletedV1>()
                .Select(context => context.Context.Message)
                .ToArray();
            completions.Should().HaveCount(expectedCalls);
            if (expectedCalls == 1)
            {
                completions[0].RequestId.Should().Be(requestId);
                completions[0].ServiceName.Should().Be("Coaching");
                completions[0].DeletedRecordCount.Should().Be(6);
            }
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class StubExecutionService(Guid requestId) : ICoachingErasureExecutionService
    {
        public int CallCount { get; private set; }

        public Task<CoachingErasureExecution> ExecuteAsync(
            PersonalDataErasureExecutionRequestedV1 message,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(CoachingErasureExecution.Complete(
                requestId, deletedRecordCount: 6, DateTime.UtcNow));
        }
    }
}
