using EduPlatform.Shared.Contracts.Events.Identity;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shared.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Integration tests for RabbitMQ event publishing
/// Tests that events are correctly published to the message bus
/// </summary>
[Collection("MessageBus")]
public class EventPublishingTests : IAsyncLifetime
{
    private readonly RabbitMqFixture _rabbitMqFixture;
    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private ITestHarness? _harness;

    public EventPublishingTests(RabbitMqFixture rabbitMqFixture, ITestOutputHelper output)
    {
        _rabbitMqFixture = rabbitMqFixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine($"RabbitMQ Host: {_rabbitMqFixture.Host}");
        _output.WriteLine($"RabbitMQ Port: {_rabbitMqFixture.AmqpPort}");
        _output.WriteLine($"RabbitMQ Connection String: {_rabbitMqFixture.ConnectionString}");

        var services = new ServiceCollection();

        // Configure MassTransit with test harness
        services.AddMassTransitTestHarness(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                // Use the connection string directly
                cfg.Host(new Uri(_rabbitMqFixture.ConnectionString));
                cfg.ConfigureEndpoints(context);
            });
        });

        _serviceProvider = services.BuildServiceProvider();
        _harness = _serviceProvider.GetRequiredService<ITestHarness>();

        // Start the test harness
        await _harness.Start();
        
        // Wait a bit for RabbitMQ to be fully ready
        await Task.Delay(1000);
    }

    public async Task DisposeAsync()
    {
        if (_harness != null)
        {
            await _harness.Stop();
        }
        
        if (_serviceProvider != null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }

    [Fact]
    public async Task PublishUserCreatedEvent_ShouldBePublishedSuccessfully()
    {
        // Arrange
        var userCreatedEvent = new UserCreatedEvent(
            UserId: Guid.NewGuid(),
            Email: "test@example.com",
            FirstName: "Test",
            LastName: "User",
            Role: "Student",
            TemporaryPassword: "TempPass123!",
            CreatedAt: DateTime.UtcNow
        );

        // Act
        await _harness!.Bus.Publish(userCreatedEvent);

        // Wait for the event to be processed
        await Task.Delay(500);

        // Assert
        // Verify the event was published
        (await _harness.Published.Any<UserCreatedEvent>()).Should().BeTrue("the event should be published");

        // Verify the published event has correct data
        var publishedEvent = _harness.Published.Select<UserCreatedEvent>().FirstOrDefault();
        publishedEvent.Should().NotBeNull("the published event should be retrievable");
        
        if (publishedEvent != null)
        {
            publishedEvent.Context.Message.UserId.Should().Be(userCreatedEvent.UserId);
            publishedEvent.Context.Message.Email.Should().Be(userCreatedEvent.Email);
        }
    }

    [Fact]
    public async Task PublishMultipleEvents_ShouldAllBePublished()
    {
        // Arrange
        var event1 = new UserCreatedEvent(
            UserId: Guid.NewGuid(),
            Email: "user1@example.com",
            FirstName: "User",
            LastName: "One",
            Role: "Student",
            TemporaryPassword: "TempPass123!",
            CreatedAt: DateTime.UtcNow
        );

        var event2 = new UserCreatedEvent(
            UserId: Guid.NewGuid(),
            Email: "user2@example.com",
            FirstName: "User",
            LastName: "Two",
            Role: "Student",
            TemporaryPassword: "TempPass123!",
            CreatedAt: DateTime.UtcNow
        );

        // Act
        await _harness!.Bus.Publish(event1);
        await _harness.Bus.Publish(event2);

        // Wait for events to be processed
        await Task.Delay(1000);

        // Assert
        var publishedEvents = _harness.Published.Select<UserCreatedEvent>().ToList();
        publishedEvents.Should().HaveCount(2, "both events should be published");
    }
}
