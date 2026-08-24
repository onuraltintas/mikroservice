using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EduPlatform.Shared.Infrastructure.Middleware;
using SpeedReading.Application.Content;
using SpeedReading.Application.Progress;
using SpeedReading.Application.Gamification;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Reports;
using SpeedReading.Infrastructure.ExternalServices;
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
        services.AddMemoryCache(options => options.SizeLimit = 4_096);

        services.AddScoped<ILegacySpeedReadingCatalog, LegacySpeedReadingCatalog>();
        services.AddScoped<ISpeedReadingContentAdminWriter, LegacySpeedReadingContentAdminWriter>();
        services.AddScoped<ILegacySpeedReadingProgress, LegacySpeedReadingProgress>();
        services.AddScoped<ISpeedReadingProgressWriter, LegacySpeedReadingProgressWriter>();
        services.AddScoped<ILegacySpeedReadingPrograms, LegacySpeedReadingPrograms>();
        services.AddScoped<ILegacySpeedReadingLearningPaths, LegacySpeedReadingLearningPaths>();
        services.AddScoped<ILegacySpeedReadingGamification, LegacySpeedReadingGamification>();
        services.AddScoped<ISpeedReadingGamificationAdminWriter, LegacySpeedReadingGamificationAdminWriter>();
        services.AddScoped<ILegacySpeedReadingAnalytics, LegacySpeedReadingAnalytics>();
        services.AddHttpClient<ISpeedReadingTeacherAccess, IdentityTeacherAccessClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddCorrelationIdPropagation();
        services.AddScoped<ILegacySpeedReadingReports, LegacySpeedReadingReports>();
        services.AddScoped<ISpeedReadingReportsAdminWriter, LegacySpeedReadingReportsAdminWriter>();
        services.AddScoped<ISpeedReadingReportsScheduleWriter, LegacySpeedReadingReportsScheduleWriter>();
        services.AddSingleton<IAdminAuditWriter, SpeedReadingAdminAuditWriter>();

        return services;
    }
}
