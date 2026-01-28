using Identity.Infrastructure.Persistence;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Identity.Application.Interfaces;

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
        // Add DbContext
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=identity_db;Username=eduplatform;Password=eduplatform_secret_2024";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();
        
        // Register dataSource as Singleton to be disposed properly (Optional but recommended, or let DI handle it if registered)
        // For simplicity within AddDbContext usage:
        
        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(dataSource, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
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
        services.AddScoped<ITokenService, Services.TokenService>();
        services.AddScoped<IGoogleAuthService, Services.GoogleAuthService>();

        // Repositories
        services.AddScoped<IUserRepository,Repositories.UserRepository>();
        services.AddScoped<IInstitutionRepository, Repositories.InstitutionRepository>();
        services.AddScoped<ITeacherRepository, Repositories.TeacherRepository>();
        services.AddScoped<IStudentRepository, Repositories.StudentRepository>();
        services.AddScoped<IInvitationRepository, Repositories.InvitationRepository>();
        services.AddScoped<IRoleRepository, Repositories.RoleRepository>();
        services.AddScoped<IPermissionRepository, Repositories.PermissionRepository>();
        services.AddScoped<IUnitOfWork, Repositories.UnitOfWork>();

        return services;
    }
}
