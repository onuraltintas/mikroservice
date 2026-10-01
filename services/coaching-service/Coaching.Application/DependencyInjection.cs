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
        services.AddSingleton(TimeProvider.System);
        // Registration legal consent is enforced centrally by Identity. Specific
        // coaching agreement workflows remain available, but are not a global
        // prerequisite for every student read/write operation.


        return services;
    }
}
