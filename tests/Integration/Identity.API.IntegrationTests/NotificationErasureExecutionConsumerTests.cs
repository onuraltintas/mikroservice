using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Privacy;

namespace Identity.API.IntegrationTests;

public sealed class NotificationErasureExecutionConsumerTests
{
    [Theory]
    [InlineData(PersonalDataScope.Account, 1)]
    [InlineData(PersonalDataScope.Coaching, 0)]
    [InlineData(PersonalDataScope.SpeedReading, 0)]
    public async Task Consumer_ShouldExecuteOnlyAccountScope(PersonalDataScope scope, int expectedCalls)
    {
        var service = new StubService();
        var services = new ServiceCollection();
        services.AddSingleton<INotificationErasureExecutionService>(service);
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.AddConsumer<NotificationErasureExecutionRequestedConsumer>();
            configurator.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        });
        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var requestId = Guid.NewGuid();
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

    private sealed class StubService : INotificationErasureExecutionService
    {
        public int CallCount { get; private set; }
        public Task<NotificationErasureExecutionResult> ExecuteAsync(
            PersonalDataErasureExecutionRequestedV1 message,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new NotificationErasureExecutionResult(
                message.RequestId, message.RequestId, 2, DateTime.UtcNow));
        }
    }
}
