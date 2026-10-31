using Identity.Infrastructure.Persistence;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Identity.Application.Interfaces;
using EduPlatform.Shared.Infrastructure.Middleware;
using Identity.Infrastructure.Security;
using Identity.Application.DataSubjectRequests;
using Identity.Infrastructure.Messaging;

namespace Identity.Infrastructure;

/// <summary>
/// Infrastructure layer dependency injection
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add DbContext - Build connection string from environment variables
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
            var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
            var database = Environment.GetEnvironmentVariable("POSTGRES_DB_IDENTITY") ?? "identity_db";
            var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "eduplatform";
            var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")
                ?? configuration["POSTGRES_PASSWORD"]
                ?? throw new InvalidOperationException("POSTGRES_PASSWORD is not configured.");
            connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();
        services.AddSingleton<IAuthenticationLock>(_ => new PostgreSqlAuthenticationLock(
            new NpgsqlDataSourceBuilder(new NpgsqlConnectionStringBuilder(connectionString)
            {
                Pooling = false,
                ApplicationName = "Identity.AuthenticationLock"
            }.ConnectionString).Build()));
        
        // Register dataSource as Singleton to be disposed properly (Optional but recommended, or let DI handle it if registered)
        // For simplicity within AddDbContext usage:
        
        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(dataSource, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            });

            // Enable sensitive data logging only in development
            #if DEBUG
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
            #endif
        });

        // Add repositories here when created
        // services.AddScoped<IUserRepository, UserRepository>();

        // Services
        services.AddScoped<IIdentityService, Services.LocalIdentityService>();
        services.AddScoped<ICoachingAccountReader, Services.CoachingAccountReader>();
        services.AddScoped<Services.CoachingLegacyProfileReader>();
        services.AddScoped<ITokenService, Services.TokenService>();
        services.AddScoped<IGoogleAuthService, Services.GoogleAuthService>();
        services.AddScoped<ISystemLogService, Services.Logs.SystemLogService>();
        services.AddScoped<IConfigurationService, Services.ConfigurationService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IMultiFactorService, MultiFactorService>();
        services.AddOptions<Security.LoginLockoutOptions>()
            .Bind(configuration.GetSection(Security.LoginLockoutOptions.SectionName))
            .Validate(options =>
            {
                options.Validate();
                return true;
            })
            .ValidateOnStart();
        // PublicationOnly: a failed first connection is retried on the next use instead of being cached.
        services.AddSingleton(provider => new Lazy<StackExchange.Redis.IConnectionMultiplexer>(
            () => provider.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>(),
            LazyThreadSafetyMode.PublicationOnly));
        services.AddSingleton<ILoginAttemptLimiter, Security.RedisLoginAttemptLimiter>();

        var sessionLifetime = configuration.GetSection(SessionLifetimeOptions.SectionName).Get<SessionLifetimeOptions>()
            ?? new SessionLifetimeOptions();
        sessionLifetime.Validate();
        services.AddSingleton(sessionLifetime);
        services.AddOptions<Security.RefreshTokenRetentionOptions>()
            .Bind(configuration.GetSection(Security.RefreshTokenRetentionOptions.SectionName))
            .Validate(options =>
            {
                options.Validate();
                return true;
            })
            .ValidateOnStart();
        services.AddHostedService<Security.RefreshTokenRetentionService>();
        services.AddScoped<IUserAccessManagementService, Services.UserAccessManagementService>();
        services.AddSingleton<IAdminAuditWriter, IdentityAdminAuditWriter>();
        services.AddAdminAuditClientDataRetention<IdentityAdminAuditClientDataPurger>(configuration);

        // Repositories
        services.AddScoped<IUserRepository,Repositories.UserRepository>();
        services.AddScoped<IInstitutionRepository, Repositories.InstitutionRepository>();
        services.AddScoped<ILocationRepository, Repositories.LocationRepository>();
        services.AddScoped<ITeacherRepository, Repositories.TeacherRepository>();
        services.AddScoped<IStudentRepository, Repositories.StudentRepository>();
        services.AddScoped<IParentRepository, Repositories.ParentRepository>();
        services.AddScoped<IParentStudentRelationshipRepository, Repositories.ParentStudentRelationshipRepository>();
        services.AddScoped<Identity.Application.ParentLinks.IParentLinkService, Services.ParentLinkService>();
        services.AddScoped<Identity.Application.ParentLinks.IParentLinkEventPublisher, Messaging.ParentLinkEventPublisher>();
        services.AddScoped<Identity.Application.Notifications.INotificationRecipientDirectory, Services.NotificationRecipientDirectory>();
        services.AddScoped<IInvitationRepository, Repositories.InvitationRepository>();
        services.AddScoped<IIdempotencyRepository, Repositories.IdempotencyRepository>();
        services.AddScoped<IRoleRepository, Repositories.RoleRepository>();
        services.AddScoped<IPermissionRepository, Repositories.PermissionRepository>();
        services.AddScoped<IDataSubjectRequestRepository, Repositories.DataSubjectRequestRepository>();
        services.AddScoped<IDataSubjectRequestEventPublisher, DataSubjectRequestEventPublisher>();
        services.AddScoped<IDataSubjectRequestErasureExecutionPublisher, DataSubjectRequestEventPublisher>();
        services.AddScoped<IDataSubjectRequestAssessmentRepository, Repositories.DataSubjectRequestAssessmentRepository>();
        services.AddScoped<IDataSubjectRequestExecutionRepository, Repositories.DataSubjectRequestExecutionRepository>();
        services.AddScoped<IIdentityAccountErasureService, Services.IdentityAccountErasureService>();
        services.AddScoped<Identity.Application.LegalPages.IPlatformLegalPages, Services.PlatformLegalPagesService>();
        services.AddScoped<Identity.Application.LegalPages.IRegistrationLegalConsentService, Services.RegistrationLegalConsentService>();
        services.AddScoped<Services.RegistrationSourceReport>();
        services.AddScoped<IUnitOfWork, Repositories.UnitOfWork>();

        return services;
    }
}
