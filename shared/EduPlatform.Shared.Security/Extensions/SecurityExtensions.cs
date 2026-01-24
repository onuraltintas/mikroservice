using EduPlatform.Shared.Security.Interfaces;
using EduPlatform.Shared.Security.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace EduPlatform.Shared.Security.Extensions;

public static class SecurityExtensions
{
    public static IServiceCollection AddCustomAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            var keycloakUrl = configuration["Keycloak:BaseUrl"];
            var realm = configuration["Keycloak:Realm"];
            
            // Internal Docker URL for Authority (back-channel)
            options.Authority = $"{keycloakUrl}/realms/{realm}";
            
            // Ensure that the token was issued by Keycloak
            options.RequireHttpsMetadata = false; // For dev environment
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false, // Keycloak tokens might have 'account' as audience
                ValidateIssuer = true,
                ValidIssuer = $"{keycloakUrl}/realms/{realm}",
                ValidateLifetime = true
            };
        });

        return services;
    }
}
