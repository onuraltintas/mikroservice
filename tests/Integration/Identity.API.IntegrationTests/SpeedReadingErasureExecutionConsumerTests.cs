using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using SpeedReading.Application.Privacy;

namespace Identity.API.IntegrationTests;

public sealed class SpeedReadingErasureExecutionConsumerTests
{
    [Theory]
    [InlineData(PersonalDataScope.Account, 1)]
    [InlineData(PersonalDataScope.SpeedReading, 1)]
    [InlineData(PersonalDataScope.Coaching, 0)]
    public async Task Consumer_ShouldExecuteOnlyScopesContainingSpeedReading(
        PersonalDataScope scope,
        int expectedCalls)
    {
        var requestId = Guid.NewGuid();
        var service = new StubService(requestId);
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedReadingErasureExecutionService>(service);
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.SetTestTimeouts(testInactivityTimeout: TimeSpan.FromSeconds(5));
            configurator.AddConsumer<SpeedReadingErasureExecutionRequestedConsumer>();
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
            (await harness.Published.Any<PersonalDataErasureExecutionCompletedV1>())
                .Should().Be(expectedCalls == 1);
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class StubService(Guid requestId) : ISpeedReadingErasureExecutionService
    {
        public int CallCount { get; private set; }
        public Task<SpeedReadingErasureExecutionResult> ExecuteAsync(
            PersonalDataErasureExecutionRequestedV1 message,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new SpeedReadingErasureExecutionResult(
                requestId, requestId, 4, DateTime.UtcNow));
        }
    }
}
