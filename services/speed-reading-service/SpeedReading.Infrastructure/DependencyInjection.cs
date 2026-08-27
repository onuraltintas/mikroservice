using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EduPlatform.Shared.Infrastructure.Middleware;
using SpeedReading.Application.Content;
using SpeedReading.Application.Progress;
using SpeedReading.Application.Gamification;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Reports;
using SpeedReading.Application.Subscription;
using SpeedReading.Application.AdaptiveLearning;
using SpeedReading.Application.AdaptiveText;
using SpeedReading.Application.ContentFeedback;
using SpeedReading.Application.Visualization;
using SpeedReading.Application.Vocabulary;
using SpeedReading.Application.QuestionBank;
using SpeedReading.Application.StudentProgram;
using SpeedReading.Application.Rsvp;
using SpeedReading.Application.Notifications;
using SpeedReading.Application.AgeGroups;
using SpeedReading.Application.Assessment;
using SpeedReading.Application.Review;
using SpeedReading.Application.SeriesAccess;
using SpeedReading.Application.StudentReading;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.Assignments;
using SpeedReading.Infrastructure.ExternalServices;
using SpeedReading.Infrastructure.Exports;
using SpeedReading.Infrastructure.Legacy;
using SpeedReading.Infrastructure.Payments;
using SpeedReading.Infrastructure.Persistence;

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

        var ownedConnectionString = configuration.GetConnectionString("SpeedReadingOwned")
            ?? configuration["SPEED_READING_OWNED_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("SPEED_READING_OWNED_CONNECTION_STRING");
        var ownedDataEnabled = configuration.GetValue<bool>("SpeedReading:OwnedDataEnabled");
        if (ownedDataEnabled && string.IsNullOrWhiteSpace(ownedConnectionString))
        {
            throw new InvalidOperationException(
                "SpeedReading:OwnedDataEnabled requires ConnectionStrings:SpeedReadingOwned or SPEED_READING_OWNED_CONNECTION_STRING.");
        }
        if (!string.IsNullOrWhiteSpace(ownedConnectionString))
        {
            services.AddDbContext<OwnedSpeedReadingDbContext>(options =>
                options.UseNpgsql(ownedConnectionString, npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", "speed_reading");
                    npgsql.EnableRetryOnFailure();
                }));
            services.AddScoped<OwnedSpeedReadingCatalogBackfill>();
        }

        services.AddMemoryCache(options => options.SizeLimit = 4_096);

        var iyzicoOptions = configuration
            .GetSection(IyzicoOptions.SectionName)
            .Get<IyzicoOptions>()
            ?? new IyzicoOptions();
        iyzicoOptions.ApplyEnvironmentOverrides(configuration);
        services.AddSingleton(iyzicoOptions);
        services.AddHttpClient<ISpeedReadingPaymentProvider, IyzicoPaymentProvider>(client =>
        {
            client.BaseAddress = new Uri(iyzicoOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        services.AddScoped<ILegacySpeedReadingCatalog, LegacySpeedReadingCatalog>();
        services.AddSingleton<ISpeedReadingReadingTextExporter, ReadingTextExportService>();
        services.AddScoped<ISpeedReadingCms, LegacySpeedReadingCms>();
        services.AddScoped<ISpeedReadingSubscription, LegacySpeedReadingSubscription>();
        services.AddScoped<ISpeedReadingAdaptiveLearning, LegacySpeedReadingAdaptiveLearning>();
        services.AddScoped<ISpeedReadingAdaptiveText, LegacySpeedReadingAdaptiveText>();
        services.AddScoped<ISpeedReadingContentFeedback, LegacySpeedReadingContentFeedback>();
        services.AddScoped<ISpeedReadingVisualization, LegacySpeedReadingVisualization>();
        services.AddScoped<ISpeedReadingVocabulary, LegacySpeedReadingVocabulary>();
        services.AddScoped<ISpeedReadingQuestionBank, LegacySpeedReadingQuestionBank>();
        services.AddScoped<ISpeedReadingStudentProgram, LegacySpeedReadingStudentProgram>();
        services.AddScoped<ISpeedReadingRsvp, LegacySpeedReadingRsvp>();
        services.AddScoped<ISpeedReadingNotifications, LegacySpeedReadingNotifications>();
        services.AddScoped<ISpeedReadingAnnouncements, LegacySpeedReadingAnnouncements>();
        services.AddScoped<ISpeedReadingEmailTemplates, LegacySpeedReadingEmailTemplates>();
        services.AddScoped<ISpeedReadingEmailCampaigns, LegacySpeedReadingEmailCampaigns>();
        services.AddScoped<ISpeedReadingAgeGroups, LegacySpeedReadingAgeGroups>();
        services.AddScoped<ISpeedReadingAssessment, LegacySpeedReadingAssessment>();
        services.AddScoped<ISpeedReadingReview, LegacySpeedReadingReview>();
        services.AddScoped<ISpeedReadingSeriesAccess, LegacySpeedReadingSeriesAccess>();
        services.AddScoped<ISpeedReadingStudentReading, LegacySpeedReadingStudentReading>();
        services.AddScoped<ISpeedReadingContentAdminWriter, LegacySpeedReadingContentAdminWriter>();
        services.AddScoped<ILegacySpeedReadingProgress, LegacySpeedReadingProgress>();
        services.AddScoped<ISpeedReadingProgressWriter, LegacySpeedReadingProgressWriter>();
        services.AddScoped<ILegacySpeedReadingPrograms, LegacySpeedReadingPrograms>();
        services.AddScoped<ILegacySpeedReadingLearningPaths, LegacySpeedReadingLearningPaths>();
        services.AddScoped<ISpeedReadingDailyProgress, LegacySpeedReadingDailyProgress>();
        if (ownedDataEnabled)
            services.AddScoped<ISpeedReadingExerciseSessions, OwnedSpeedReadingExerciseSessions>();
        else
            services.AddScoped<ISpeedReadingExerciseSessions, LegacySpeedReadingExerciseSessions>();
        services.AddScoped<ISpeedReadingAssignments, LegacySpeedReadingAssignments>();
        services.AddScoped<ILegacySpeedReadingGamification, LegacySpeedReadingGamification>();
        services.AddScoped<ISpeedReadingGamificationAdminWriter, LegacySpeedReadingGamificationAdminWriter>();
        services.AddScoped<ILegacySpeedReadingAnalytics, LegacySpeedReadingAnalytics>();
        services.AddScoped<ILegacySpeedReadingAdminAnalytics, LegacySpeedReadingAdminAnalytics>();
        services.AddHttpClient<ISpeedReadingTeacherAccess, IdentityTeacherAccessClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddCorrelationIdPropagation();
        services.AddHttpClient<ISpeedReadingInstitutionDirectory, IdentityInstitutionDirectoryClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddCorrelationIdPropagation();
        services.AddScoped<ILegacySpeedReadingReports, LegacySpeedReadingReports>();
        services.AddScoped<ILegacySpeedReadingTeacherReports, LegacySpeedReadingTeacherReports>();
        services.AddScoped<ISpeedReadingReportsAdminWriter, LegacySpeedReadingReportsAdminWriter>();
        services.AddScoped<ISpeedReadingReportsScheduleWriter, LegacySpeedReadingReportsScheduleWriter>();
        services.AddScoped<ISpeedReadingReportsSnapshotWriter, LegacySpeedReadingReportsSnapshotWriter>();
        services.AddSingleton<ISpeedReadingReportExporter, ReportExportService>();
        services.AddSingleton<IAdminAuditWriter, SpeedReadingAdminAuditWriter>();

        return services;
    }
}
