using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddScoped<Authorization.InstitutionManagementAuthorization>();
        services.AddScoped<Services.IAuthenticationSessionIssuer, Services.AuthenticationSessionIssuer>();
        services.AddScoped<Services.MfaAuthenticationCoordinator>();
        
        return services;
    }
}
