using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Coaching.Application.Interfaces;
using Coaching.Application.Attachments;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using Coaching.Infrastructure.Attachments;
using Coaching.Infrastructure.ExternalServices;
using Coaching.Infrastructure.Messaging;
using EduPlatform.Shared.Infrastructure.Middleware;

namespace Coaching.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database - Build connection string from environment variables
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
            var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
            var database = Environment.GetEnvironmentVariable("POSTGRES_DB_COACHING") ?? "coaching_db";
            var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "eduplatform";
            var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") 
                ?? throw new InvalidOperationException("POSTGRES_PASSWORD environment variable not found.");
            connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
        }

        services.AddDbContext<CoachingDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", "coaching");
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

            // Development logging
            if (configuration.GetValue<bool>("Logging:EnableSensitiveDataLogging"))
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        services.AddOptions<AssignmentAttachmentOptions>()
            .Bind(configuration.GetSection(AssignmentAttachmentOptions.SectionName))
            .Validate(options => options.Provider.Equals("Local", StringComparison.OrdinalIgnoreCase)
                || options.Provider.Equals("Minio", StringComparison.OrdinalIgnoreCase),
                "Attachment storage provider must be Local or Minio.")
            .Validate(options => options.UploadUrlLifetimeMinutes is >= 1 and <= 60,
                "Attachment upload URL lifetime must be between 1 and 60 minutes.")
            .Validate(options => options.Provider.Equals("Local", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(options.MinioEndpoint)
                    && !string.IsNullOrWhiteSpace(options.MinioAccessKey)
                    && !string.IsNullOrWhiteSpace(options.MinioSecretKey)
                    && !string.IsNullOrWhiteSpace(options.MinioBucket)),
                "Minio storage requires endpoint, access key, secret key and bucket.")
            .ValidateOnStart();

        var storageOptions = configuration
            .GetSection(AssignmentAttachmentOptions.SectionName)
            .Get<AssignmentAttachmentOptions>() ?? new AssignmentAttachmentOptions();

        var scanOptions = configuration
            .GetSection(AttachmentScanOptions.SectionName)
            .Get<AttachmentScanOptions>() ?? new AttachmentScanOptions();
        if (string.IsNullOrWhiteSpace(scanOptions.Provider))
            throw new InvalidOperationException("Attachment scanner provider is required.");

        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";
        if (environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase)
            && !scanOptions.Provider.Equals("ClamAv", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production requires Coaching:Attachments:Scanner:Provider=ClamAv.");
        }

        if (environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase)
            && !storageOptions.Provider.Equals("Minio", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production requires Coaching:Attachments:Provider=Minio.");
        }

        services.AddOptions<AttachmentScanOptions>()
            .Bind(configuration.GetSection(AttachmentScanOptions.SectionName))
            .Validate(options => options.Provider.Equals("Local", StringComparison.OrdinalIgnoreCase)
                || options.Provider.Equals("ClamAv", StringComparison.OrdinalIgnoreCase),
                "Attachment scanner provider must be Local or ClamAv.")
            .Validate(options => options.ClamAvPort is >= 1 and <= 65535,
                "ClamAV port must be between 1 and 65535.")
            .Validate(options => options.TimeoutSeconds is >= 1 and <= 60,
                "ClamAV timeout must be between 1 and 60 seconds.")
            .ValidateOnStart();

        // Repositories
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
        services.AddScoped<IExamRepository, ExamRepository>();
        services.AddScoped<ICoachingSessionRepository, CoachingSessionRepository>();
        services.AddScoped<IAcademicGoalRepository, AcademicGoalRepository>();
        services.AddScoped<ICoachingAdminRepository, CoachingAdminRepository>();
        if (storageOptions.Provider.Equals("Minio", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IAssignmentAttachmentStorage, MinioAssignmentAttachmentStorage>();
        else
            services.AddSingleton<IAssignmentAttachmentStorage, LocalAssignmentAttachmentStorage>();
        if (scanOptions.Provider.Equals("ClamAv", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IAssignmentAttachmentScanner, ClamAvAttachmentScanner>();
        else
            services.AddSingleton<IAssignmentAttachmentScanner, DevelopmentAttachmentScanner>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICoachingEventPublisher, MassTransitCoachingEventPublisher>();
        services.AddSingleton<IAdminAuditWriter, CoachingAdminAuditWriter>();
        services.AddHttpClient<ICoachingIdentityAuthorizationClient, IdentityAuthorizationClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddCorrelationIdPropagation();

        return services;
    }
}
