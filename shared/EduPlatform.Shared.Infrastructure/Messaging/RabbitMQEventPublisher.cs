using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace EduPlatform.Shared.Infrastructure.Messaging;

/// <summary>
/// Event publisher interface
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, string? routingKey = null, CancellationToken cancellationToken = default) where T : class;
}

/// <summary>
/// RabbitMQ settings
/// </summary>
public class RabbitMQSettings
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "eduplatform";
    public string Password { get; set; } = "rabbitmq_secret_2024";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "eduplatform.events";
}

/// <summary>
/// RabbitMQ event publisher
/// </summary>
public class RabbitMQEventPublisher : IEventPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly RabbitMQSettings _settings;
    private bool _disposed;

    public RabbitMQEventPublisher(RabbitMQSettings settings)
    {
        _settings = settings;
        
        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
        
        // Declare exchange
        _channel.ExchangeDeclareAsync(
            exchange: settings.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false).GetAwaiter().GetResult();
    }

    public async Task PublishAsync<T>(T @event, string? routingKey = null, CancellationToken cancellationToken = default) 
        where T : class
    {
        var eventType = typeof(T).Name;
        var key = routingKey ?? eventType.ToLowerInvariant();
        
        var message = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Type = eventType
        };

        await _channel.BasicPublishAsync(
            exchange: _settings.ExchangeName,
            routingKey: key,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _channel?.Dispose();
        _connection?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// RabbitMQ configuration extensions
/// </summary>
public static class RabbitMQConfiguration
{
    public static IServiceCollection AddRabbitMQ(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = new RabbitMQSettings();
        configuration.GetSection("RabbitMQ").Bind(settings);

        services.AddSingleton(settings);
        services.AddSingleton<IEventPublisher, RabbitMQEventPublisher>();

        return services;
    }
}
