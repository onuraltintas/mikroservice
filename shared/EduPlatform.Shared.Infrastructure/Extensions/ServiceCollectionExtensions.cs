using EduPlatform.Shared.Infrastructure.Behaviors;
using EduPlatform.Shared.Infrastructure.Caching;
using EduPlatform.Shared.Infrastructure.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace EduPlatform.Shared.Infrastructure.Extensions;

/// <summary>
/// Central service collection extensions for all microservices
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all shared infrastructure services (Redis, RabbitMQ, Behaviors)
    /// </summary>
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] assemblies)
    {
        // Add Redis Cache
        services.AddRedisCache(configuration);

        // Add RabbitMQ
        services.AddRabbitMQ(configuration);

        // Add FluentValidation validators from assemblies
        foreach (var assembly in assemblies)
        {
            services.AddValidatorsFromAssembly(assembly);
        }

        return services;
    }

    /// <summary>
    /// Adds Mediator with behaviors
    /// </summary>
    public static IServiceCollection AddMediatorWithBehaviors(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        // Mediator.SourceGenerator handles registration automatically
        // Just register behaviors
        services.AddScoped(typeof(Mediator.IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(Mediator.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }

    /// <summary>
    /// Adds health checks for infrastructure dependencies
    /// </summary>
    public static IServiceCollection AddInfrastructureHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        
        services.AddHealthChecks()
            .AddRedis(redisConnection, name: "redis", tags: new[] { "infrastructure", "cache" })
            .AddRabbitMQ(name: "rabbitmq", tags: new[] { "infrastructure", "messaging" });

        return services;
    }

    /// <summary>
    /// Adds Global Exception Handler
    /// </summary>
    public static IServiceCollection AddGlobalExceptionHandler(this IServiceCollection services)
    {
        services.AddExceptionHandler<Middleware.GlobalExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }
}
