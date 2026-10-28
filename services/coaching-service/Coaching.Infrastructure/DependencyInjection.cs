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
using Coaching.Application.Privacy;
using Coaching.Infrastructure.Privacy;
using Coaching.Application.Content;
using Coaching.Application.Subscriptions;
using Coaching.Application.Newsletters;
using Coaching.Infrastructure.Management;

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
            .Validate(options => options.Provider.Equals("Local", StringComparison.OrdinalIgnoreCase),
                "Attachment storage provider must be Local.")
            .Validate(options => options.UploadUrlLifetimeMinutes is >= 1 and <= 60,
                "Attachment upload URL lifetime must be between 1 and 60 minutes.")
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
            && storageOptions.Provider.Equals("Local", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(configuration[$"{AssignmentAttachmentOptions.SectionName}:RootPath"])
                || !Path.IsPathFullyQualified(storageOptions.RootPath)))
        {
            throw new InvalidOperationException(
                "Production local attachment storage requires an explicit absolute Coaching:Attachments:RootPath.");
        }

        if (!storageOptions.Provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Coaching:Attachments:Provider must be Local.");

        var coachingCmsMediaRootPath = configuration["Coaching:CmsMedia:RootPath"]
            ?? Environment.GetEnvironmentVariable("COACHING_CMS_MEDIA_ROOT");
        if (environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(coachingCmsMediaRootPath) || !Path.IsPathFullyQualified(coachingCmsMediaRootPath)))
        {
            throw new InvalidOperationException(
                "Production Coaching CMS media storage requires an explicit absolute Coaching:CmsMedia:RootPath.");
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
        services.AddScoped<ICoachingCalendarRepository, CoachingCalendarRepository>();
        services.AddScoped<IAcademicGoalRepository, AcademicGoalRepository>();
        services.AddScoped<ICoachingStudentProgressRepository, CoachingStudentProgressRepository>();
        services.AddScoped<ICoachingComparativeReportRepository, CoachingComparativeReportRepository>();
        services.AddScoped<ICoachingEarlyWarningRepository, CoachingEarlyWarningRepository>();
        services.AddScoped<ICoachingAdminRepository, CoachingAdminRepository>();
        services.AddScoped<ICoachingStudentHistoryRepository>(provider =>
            provider.GetRequiredService<ICoachingAdminRepository>());
        services.AddScoped<ICoachingAgreementRepository, CoachingAgreementRepository>();
        services.AddScoped<ICoachingDataExportRepository, CoachingDataExportRepository>();
        services.AddScoped<ICoachingErasureAssessmentService, CoachingErasureAssessmentService>();
        services.AddScoped<ICoachingErasureExecutionService, CoachingErasureExecutionService>();
        services.AddScoped<ICoachingCms, CoachingCmsService>();
        services.AddScoped<Coaching.Application.StudyPlanning.IStudyAvailabilityService, Coaching.Infrastructure.StudyPlanning.CoachingStudyAvailabilityService>();
        services.AddScoped<Coaching.Application.StudyPlanning.IWeeklyStudyDraftService, Coaching.Infrastructure.StudyPlanning.CoachingWeeklyStudyDraftService>();
        services.AddScoped<Coaching.Application.StudyPlanning.IWeeklyOutcomePlanService, Coaching.Infrastructure.StudyPlanning.CoachingWeeklyOutcomePlanService>();
        services.AddScoped<Coaching.Application.StudyPlanning.ILearningSetupService, Coaching.Infrastructure.StudyPlanning.CoachingLearningSetupService>();
        services.AddScoped<Coaching.Infrastructure.Catalogs.CoachingCurriculumService>();
        services.AddScoped<Coaching.Infrastructure.Catalogs.CoachingOutcomeCatalogService>();
        services.AddScoped<Coaching.Infrastructure.Catalogs.CoachingCurriculumManagementService>();
        services.AddScoped<Coaching.Infrastructure.StudyPlanning.CoachingGoalTargetService>();
        services.AddScoped<Coaching.Infrastructure.StudyPlanning.CoachingPracticeProgressService>();
        services.AddScoped<Coaching.Infrastructure.StudyPlanning.CoachingPracticeAdaptationService>();
        services.AddScoped<Coaching.Infrastructure.StudyPlanning.CoachingWeeklyReviewService>();
        services.AddScoped<Coaching.Application.StudyPlanning.IGoalTargetService>(sp => sp.GetRequiredService<Coaching.Infrastructure.StudyPlanning.CoachingGoalTargetService>());
        services.AddScoped<Coaching.Application.StudyPlanning.IAutomaticStudyPlanPreviewService, Coaching.Infrastructure.StudyPlanning.CoachingAutomaticStudyPlanPreviewService>();
        services.AddScoped<Coaching.Application.StudyPlanning.IStudyTopicSearchService, Coaching.Infrastructure.StudyPlanning.CoachingTopicSearchService>();
        services.AddScoped<Coaching.Application.StudyPlanning.IStudentExamService, Coaching.Infrastructure.StudyPlanning.CoachingStudentExamService>();
        services.AddScoped<Coaching.Application.StudyPlanning.IStudentStudyReportService, Coaching.Infrastructure.StudyPlanning.CoachingStudentStudyReportService>();
        services.AddScoped<Coaching.Infrastructure.StudyPlanning.CoachingAdminStudyReader>();
        services.AddScoped<Coaching.Infrastructure.StudyPlanning.CoachingAdminStudyCorrectionService>();
        services.AddScoped<Coaching.Application.StudyPlanning.IAutomaticStudyPlanDraftService>(sp =>
            (Coaching.Infrastructure.StudyPlanning.CoachingManualStudyPlanService)sp.GetRequiredService<Coaching.Application.StudyPlanning.IManualStudyPlanService>());
        services.AddScoped<Coaching.Application.Authorization.CoachingStudyPlanAccess>();
        services.AddScoped<Coaching.Infrastructure.StudyPlanning.CoachingManualStudyPlanService>();
        services.AddScoped<Coaching.Application.StudyPlanning.IManualStudyPlanService>(sp => sp.GetRequiredService<Coaching.Infrastructure.StudyPlanning.CoachingManualStudyPlanService>());
        services.AddScoped<Coaching.Infrastructure.StudyPlanning.CoachingStudentStudyReportService>();
        services.AddScoped<ICoachingSubscription, CoachingSubscriptionService>();
        services.AddScoped<CoachingInstitutionSubscriptionService>();
        services.AddScoped<CoachingInstitutionExamService>();
        services.AddScoped<CoachingMessagingService>();
        services.AddScoped<CoachingScoreEstimateService>();
        services.AddScoped<CoachingSalesService>();
        services.AddScoped<CoachingSeatRequestService>();
        services.AddScoped<CoachingCalendarSubscriptionService>();
        services.AddScoped<Coaching.Infrastructure.Finance.CoachingFeeService>();
        services.AddScoped<ICoachingNewsletter>(provider => new CoachingNewsletterService(
            provider.GetRequiredService<CoachingDbContext>(),
            provider.GetRequiredService<ICoachingNewsletterEmailDelivery>(),
            provider.GetRequiredService<ICoachingSharedLegalPageVersionProvider>(),
            configuration["CoachingNewsletter:PublicBaseUrl"] ?? "https://onuraltintas.net"));
        services.AddSingleton<ICoachingCmsMediaStorage, LocalCoachingCmsMediaStorage>();
        services.AddSingleton<IAssignmentAttachmentStorage, LocalAssignmentAttachmentStorage>();
        if (scanOptions.Provider.Equals("ClamAv", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IAssignmentAttachmentScanner, ClamAvAttachmentScanner>();
        else
            services.AddSingleton<IAssignmentAttachmentScanner, DevelopmentAttachmentScanner>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICoachingEventPublisher, MassTransitCoachingEventPublisher>();
        services.AddScoped<Coaching.Infrastructure.Reminders.CoachingReminderProcessor>();
        services.AddScoped<CoachingSubscriptionLifecycleProcessor>();
        if (configuration.GetValue("Coaching:Subscriptions:LifecycleEnabled", true))
            services.AddHostedService<CoachingSubscriptionLifecycleWorker>();
        services.AddScoped<CoachingParentWeeklySummaryProcessor>();
        if (configuration.GetValue("Coaching:Parents:WeeklySummaryEnabled", true))
            services.AddHostedService<CoachingParentWeeklySummaryWorker>();
        services.AddHttpClient<ICoachingParentDirectory, IdentityParentDirectoryClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        }).AddCorrelationIdPropagation();
        if (configuration.GetValue("Coaching:Reminders:Enabled", true))
            services.AddHostedService<Coaching.Infrastructure.Reminders.CoachingReminderWorker>();
        services.AddScoped<Coaching.Infrastructure.Attachments.CoachingAttachmentCleanupProcessor>();
        if (configuration.GetValue("Coaching:Attachments:CleanupEnabled", true))
            services.AddHostedService<Coaching.Infrastructure.Attachments.CoachingAttachmentCleanupWorker>();
        if (configuration.GetValue("Coaching:Messages:RetentionEnabled", true))
            services.AddHostedService<CoachingMessagingRetentionWorker>();
        services.AddScoped<Coaching.Infrastructure.Privacy.CoachingFinancialRetentionProcessor>();
        if (configuration.GetValue("Coaching:Privacy:FinancialRetentionEnabled", true))
            services.AddHostedService<Coaching.Infrastructure.Privacy.CoachingFinancialRetentionWorker>();
        services.AddSingleton<IAdminAuditWriter, CoachingAdminAuditWriter>();
        services.AddAdminAuditClientDataRetention<CoachingAdminAuditClientDataPurger>(configuration);
        services.AddScoped<Coaching.Application.CatalogAdministration.ICoachingAdminCatalogReader, Coaching.Infrastructure.Catalogs.CoachingAdminCatalogReader>();
        services.AddScoped<Coaching.Application.CatalogAdministration.ICoachingCatalogDeletionService, Coaching.Infrastructure.Catalogs.CoachingCatalogDeletionService>();
        services.AddScoped<Coaching.Application.CatalogAdministration.ICoachingCatalogManagementService, Coaching.Infrastructure.Catalogs.CoachingCatalogManagementService>();
        services.AddHttpClient<Coaching.Application.CatalogAdministration.ICoachingLocationDirectory, IdentityLocationDirectoryClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddCorrelationIdPropagation();
        services.AddScoped<Profiles.CoachingProfileStore>();
        services.AddScoped<Profiles.CoachingInvitationStore>();
        services.AddScoped<Coaching.Application.Profiles.ICoachingProfileProvisioner>(sp => sp.GetRequiredService<Profiles.CoachingProfileStore>());
        services.AddScoped<Profiles.OwnedCoachingDirectory>();
        services.AddScoped<ICoachingIdentityAuthorizationClient>(sp => sp.GetRequiredService<Profiles.OwnedCoachingDirectory>());
        services.AddScoped<ICoachingIdentityReportClient>(sp => sp.GetRequiredService<Profiles.OwnedCoachingDirectory>());
        // Roadmap O13: an admin of several branches picks one per request.
        services.AddHttpContextAccessor();
        services.AddScoped<ICoachingInstitutionSelection, Coaching.Infrastructure.ExternalServices.HttpCoachingInstitutionSelection>();
        services.AddScoped<Coaching.Infrastructure.Management.CoachingInstitutionWorkloadService>();
        services.AddScoped<Coaching.Infrastructure.Management.CoachingInstitutionBulkService>();
        services.AddHttpClient<Coaching.Application.Profiles.ICoachingAccountDirectory, CoachingAccountDirectoryClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddCorrelationIdPropagation();
        services.AddHttpClient<ICoachingAgreementRepresentativeAuthorizationClient, IdentityAuthorizationClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddCorrelationIdPropagation();
        services.AddHttpClient<ICoachingNewsletterEmailDelivery, CoachingNewsletterEmailClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        }).AddCorrelationIdPropagation();
        services.AddHttpClient<ICoachingSharedLegalPageVersionProvider, IdentitySharedLegalPageVersionClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        }).AddCorrelationIdPropagation();

        return services;
    }
}
