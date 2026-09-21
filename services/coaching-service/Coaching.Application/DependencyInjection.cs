using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using System.Reflection;
using Coaching.Application.Authorization;
using Coaching.Application.CoachingAgreements;
using MediatR;

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
        services.AddScoped(
            typeof(IPipelineBehavior<,>),
            typeof(CoachingAgreementRequirementBehavior<,>));


        return services;
    }
}
