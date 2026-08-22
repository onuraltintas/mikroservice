using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using System.Reflection;
using Coaching.Application.Authorization;

namespace Coaching.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddScoped<ICoachingAccessPolicy, CoachingAccessPolicy>();
        services.AddScoped<ICoachingAdminScopeAuthorization, CoachingAdminScopeAuthorization>();


        return services;
    }
}
