using EduPlatform.Shared.Security.Interfaces;
using EduPlatform.Shared.Security.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace EduPlatform.Shared.Security.Extensions;

public static class SecurityExtensions
{
    public static IServiceCollection AddCustomAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<JwtBearerOptions>? configureOptions = null)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        var secretKey = configuration["JWT_SECRET"] ?? configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT_SECRET is not configured.");
        var issuer = configuration["JWT_ISSUER"] ?? configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT_ISSUER is not configured.");
        var audience = configuration["JWT_AUDIENCE"] ?? configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT_AUDIENCE is not configured.");
        if (System.Text.Encoding.UTF8.GetByteCount(secretKey) < 32)
        {
            throw new InvalidOperationException("JWT_SECRET must be at least 32 bytes long.");
        }
        var key = System.Text.Encoding.UTF8.GetBytes(secretKey);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = configuration.GetValue("Jwt:RequireHttpsMetadata", true);
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = System.Security.Claims.ClaimTypes.Role, // Ensure roles are mapped correctly
            };

            configureOptions?.Invoke(options);
        });

        return services;
    }
    public static IServiceCollection AddGlobalAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    public static IServiceCollection AddCustomAuthorization(this IServiceCollection services)
    {
        services.AddGlobalAuthorization();
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, Authorization.PermissionPolicyProvider>();
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Authorization.PermissionAuthorizationHandler>();
        return services;
    }
}
