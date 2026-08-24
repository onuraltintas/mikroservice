using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SpeedReading.Application.Content;
using SpeedReading.Application.Progress;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSpeedReadingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SpeedReading")
            ?? configuration["SPEED_READING_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("SPEED_READING_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:SpeedReading or SPEED_READING_CONNECTION_STRING must be configured.");
        }

        services.AddDbContext<SpeedReadingDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.EnableRetryOnFailure()));

        services.AddScoped<ILegacySpeedReadingCatalog, LegacySpeedReadingCatalog>();
        services.AddScoped<ILegacySpeedReadingProgress, LegacySpeedReadingProgress>();
        services.AddScoped<ISpeedReadingProgressWriter, LegacySpeedReadingProgressWriter>();
        services.AddScoped<ILegacySpeedReadingPrograms, LegacySpeedReadingPrograms>();
        services.AddScoped<ILegacySpeedReadingLearningPaths, LegacySpeedReadingLearningPaths>();

        return services;
    }
}
