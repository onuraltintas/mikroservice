using EduPlatform.Shared.Infrastructure.Behaviors;
using EduPlatform.Shared.Infrastructure.Caching;
using EduPlatform.Shared.Infrastructure.Messaging;
using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        services.AddScoped(typeof(MediatR.IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }

    /// <summary>
    /// Adds health checks for infrastructure dependencies
    /// </summary>
    public static IServiceCollection AddInfrastructureHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Build Redis connection from environment variables for security
        var redisConnection = configuration.GetConnectionString("Redis");
        if (string.IsNullOrEmpty(redisConnection))
        {
            var host = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
            var port = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";
            var password = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
            
            redisConnection = string.IsNullOrEmpty(password) 
                ? $"{host}:{port}" 
                : $"{host}:{port},password={password}";
        }
        
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

    /// <summary>
    /// Applies the shared HTTP API error contract to MVC model validation.
    /// </summary>
    public static IMvcBuilder AddEduPlatformApiConventions(this IMvcBuilder builder)
    {
        builder.ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problemDetails = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation Error",
                    Type = "https://eduplatform.dev/problems/validation-error",
                    Instance = context.HttpContext.Request.Path
                };

                problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                return new BadRequestObjectResult(problemDetails)
                {
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

        return builder;
    }

    /// <summary>
    /// Adds the non-breaking v1 API version contract shared by HTTP services.
    /// Existing routes remain valid without a version; clients may opt into
    /// header or query-string version selection during migration.
    /// </summary>
    public static IServiceCollection AddEduPlatformApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new HeaderApiVersionReader("X-Api-Version"),
                new QueryStringApiVersionReader("api-version"));
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }
}
