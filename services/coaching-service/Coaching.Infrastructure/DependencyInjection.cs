using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Coaching.Application.Interfaces;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;

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

        // Repositories
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<IExamRepository, ExamRepository>();
        services.AddScoped<ICoachingSessionRepository, CoachingSessionRepository>();
        services.AddScoped<IAcademicGoalRepository, AcademicGoalRepository>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
