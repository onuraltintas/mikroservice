using Testcontainers.RabbitMq;
using Xunit;

namespace Shared.IntegrationTests.Fixtures;

/// <summary>
/// RabbitMQ test container fixture for integration tests.
/// Provides a real RabbitMQ instance running in Docker.
/// </summary>
public class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container;

    public RabbitMqFixture()
    {
        _container = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.12-management-alpine")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>
    /// Gets the connection string for RabbitMQ (amqp://...)
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Gets the container host
    /// </summary>
    public string Host => _container.Hostname;

    /// <summary>
    /// Gets the AMQP port (5672)
    /// </summary>
    public int AmqpPort => _container.GetMappedPublicPort(5672);

    /// <summary>
    /// Gets the Management UI port (15672)
    /// </summary>
    public int ManagementPort => _container.GetMappedPublicPort(15672);

    /// <summary>
    /// Gets the username
    /// </summary>
    public string Username => "test_user";

    /// <summary>
    /// Gets the password
    /// </summary>
    public string Password => "test_password";

    /// <summary>
    /// Starts the RabbitMQ container
    /// </summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    /// <summary>
    /// Stops and disposes the RabbitMQ container
    /// </summary>
    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}
