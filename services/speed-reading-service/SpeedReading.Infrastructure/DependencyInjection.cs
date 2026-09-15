using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EduPlatform.Shared.Infrastructure.Middleware;
using SpeedReading.Application.AdaptiveLearning;
using SpeedReading.Application.AdaptiveText;
using SpeedReading.Application.AgeGroups;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Assessment;
using SpeedReading.Application.Content;
using SpeedReading.Application.ContentFeedback;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.Gamification;
using SpeedReading.Application.Notifications;
using SpeedReading.Application.Progress;
using SpeedReading.Application.QuestionBank;
using SpeedReading.Application.Reports;
using SpeedReading.Application.Review;
using SpeedReading.Application.Rsvp;
using SpeedReading.Application.SeriesAccess;
using SpeedReading.Application.StudentProgram;
using SpeedReading.Application.StudentReading;
using SpeedReading.Application.Subscription;
using SpeedReading.Application.Visualization;
using SpeedReading.Application.Vocabulary;
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
        var ownedConnectionString = configuration.GetConnectionString("SpeedReadingOwned")
            ?? configuration["SPEED_READING_OWNED_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("SPEED_READING_OWNED_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(ownedConnectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:SpeedReadingOwned or SPEED_READING_OWNED_CONNECTION_STRING must be configured.");
        }

        services.AddDbContext<OwnedSpeedReadingDbContext>(options =>
            options.UseNpgsql(ownedConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "speed_reading");
                npgsql.EnableRetryOnFailure();
            }));
        services.AddScoped<OwnedSpeedReadingGamificationRecalculation>();
        services.AddScoped<OwnedExerciseTaxonomyBootstrap>();
        services.AddScoped<OwnedSpeedReadingReadingTextWordCountBackfill>();
        services.AddScoped<OwnedSpeedReadingContentAudit>();

        services.AddMemoryCache(options => options.SizeLimit = 4_096);
        services.AddSingleton<ISpeedReadingCmsMediaStorage, LocalCmsMediaStorage>();

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

        services.AddSingleton<ISpeedReadingReadingTextExporter, ReadingTextExportService>();
        services.AddScoped<ISpeedReadingDataContext>(serviceProvider =>
            serviceProvider.GetRequiredService<OwnedSpeedReadingDbContext>());
        services.AddScoped<ISpeedReadingCms>(serviceProvider =>
            new LegacySpeedReadingCms(
                serviceProvider.GetRequiredService<ISpeedReadingDataContext>(),
                serviceProvider.GetRequiredService<IMemoryCache>(),
                serviceProvider.GetRequiredService<ISpeedReadingEmailDelivery>(),
                serviceProvider.GetRequiredService<ISpeedReadingCmsMediaStorage>()));
        services.AddScoped<ISpeedReadingSubscription>(serviceProvider =>
            new LegacySpeedReadingSubscription(
                serviceProvider.GetRequiredService<ISpeedReadingDataContext>(),
                serviceProvider.GetRequiredService<OwnedSpeedReadingDbContext>(),
                serviceProvider.GetRequiredService<ISpeedReadingPaymentProvider>(),
                serviceProvider.GetRequiredService<IyzicoOptions>(),
                serviceProvider.GetRequiredService<ISpeedReadingUserDirectory>()));
        services.AddScoped<ISpeedReadingNotifications, LegacySpeedReadingNotifications>();
        services.AddScoped<ISpeedReadingAnnouncements, LegacySpeedReadingAnnouncements>();
        services.AddScoped<ISpeedReadingEmailTemplates, LegacySpeedReadingEmailTemplates>();
        services.AddScoped<ISpeedReadingEmailCampaigns, LegacySpeedReadingEmailCampaigns>();
        services.AddScoped<ISpeedReadingRsvp, LegacySpeedReadingRsvp>();

        services.AddScoped<ILegacySpeedReadingCatalog, OwnedSpeedReadingCatalog>();
        services.AddScoped<ILegacySpeedReadingProgress, OwnedSpeedReadingProgress>();
        services.AddScoped<ISpeedReadingProgressWriter, OwnedSpeedReadingProgressWriter>();
        services.AddScoped<ISpeedReadingExerciseSessions, OwnedSpeedReadingExerciseSessions>();
        services.AddScoped<ISpeedReadingAssignments, OwnedSpeedReadingAssignments>();
        services.AddScoped<ILegacySpeedReadingPrograms, OwnedSpeedReadingPrograms>();
        services.AddScoped<ISpeedReadingProgramAdminWriter, OwnedSpeedReadingProgramAdminWriter>();
        services.AddScoped<ISpeedReadingDailyProgress, OwnedSpeedReadingDailyProgress>();
        services.AddScoped<ISpeedReadingAgeGroups, OwnedSpeedReadingAgeGroups>();
        services.AddScoped<ISpeedReadingStudentProgram, OwnedSpeedReadingStudentProgram>();
        services.AddScoped<ISpeedReadingAssessment, OwnedSpeedReadingAssessment>();
        services.AddScoped<ISpeedReadingLevelCatalog, OwnedSpeedReadingLevelCatalog>();
        services.AddScoped<ISpeedReadingCalibrationAnalytics, OwnedSpeedReadingCalibrationAnalytics>();
        services.AddScoped<ISpeedReadingStudyEnrollments, OwnedSpeedReadingStudyEnrollments>();
        services.AddScoped<ISpeedReadingStudyCatalog, OwnedSpeedReadingStudyCatalog>();
        services.AddScoped<ISpeedReadingCatalogAdminWriter, OwnedSpeedReadingCatalogAdminWriter>();
        services.AddScoped<ISpeedReadingContentAdminWriter, OwnedSpeedReadingContentAdminWriter>();
        services.AddScoped<ILegacySpeedReadingLearningPaths, OwnedSpeedReadingLearningPaths>();
        services.AddScoped<ISpeedReadingLearningPathAdminWriter, OwnedSpeedReadingLearningPathAdminWriter>();
        services.AddScoped<ISpeedReadingQuestionBank, OwnedSpeedReadingQuestionBank>();
        services.AddScoped<ISpeedReadingVisualization, OwnedSpeedReadingVisualization>();
        services.AddScoped<ISpeedReadingVocabulary, OwnedSpeedReadingVocabulary>();
        services.AddScoped<ISpeedReadingIdempotencyCleaner, OwnedSpeedReadingIdempotencyCleaner>();
        services.AddScoped<ISpeedReadingSeriesAccess, OwnedSpeedReadingSeriesAccess>();
        services.AddScoped<ISpeedReadingReview, OwnedSpeedReadingReview>();
        services.AddScoped<ISpeedReadingContentFeedback, OwnedSpeedReadingContentFeedback>();
        services.AddScoped<ISpeedReadingStudentReading, OwnedSpeedReadingStudentReading>();
        services.AddScoped<ISpeedReadingAdaptiveLearning, OwnedSpeedReadingAdaptiveLearning>();
        services.AddScoped<ISpeedReadingAdaptiveText, OwnedSpeedReadingAdaptiveText>();

        services.AddHttpClient<ISpeedReadingTeacherAccess, IdentityTeacherAccessClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddCorrelationIdPropagation();
        services.AddHttpClient<ISpeedReadingProgressAccess, IdentityProgressAccessClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddCorrelationIdPropagation();
        services.AddHttpClient<ISpeedReadingUserDirectory, IdentityUserDirectoryClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddCorrelationIdPropagation();
        services.AddHttpClient<ISpeedReadingInstitutionDirectory, IdentityInstitutionDirectoryClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddCorrelationIdPropagation();
        services.AddHttpClient<ISpeedReadingEmailDelivery, NotificationEmailClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        }).AddCorrelationIdPropagation();

        services.AddSingleton<ISpeedReadingReportExporter, ReportExportService>();
        services.AddScoped<ILegacySpeedReadingGamification, OwnedSpeedReadingGamification>();
        services.AddScoped<ISpeedReadingGamificationAdminWriter, OwnedSpeedReadingGamificationAdminWriter>();
        services.AddScoped<IAdminAuditWriter, OwnedSpeedReadingAdminAuditWriter>();
        services.AddScoped<ILegacySpeedReadingAnalytics, OwnedSpeedReadingAnalytics>();
        services.AddScoped<ILegacySpeedReadingAdminAnalytics, OwnedSpeedReadingAdminAnalytics>();
        services.AddScoped<ILegacySpeedReadingTeacherReports, OwnedSpeedReadingTeacherReports>();
        services.AddScoped<ILegacySpeedReadingReports, OwnedSpeedReadingReports>();
        services.AddScoped<ISpeedReadingReportsAdminWriter, OwnedSpeedReadingReports>();
        services.AddScoped<ISpeedReadingReportsScheduleWriter, OwnedSpeedReadingReports>();
        services.AddScoped<ISpeedReadingReportsSnapshotWriter, OwnedSpeedReadingReports>();

        return services;
    }
}
