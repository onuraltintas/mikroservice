using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;

namespace Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        
        // Manual scanning for validators
        var validators = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.BaseType != null && t.BaseType.IsGenericType &&
                        t.BaseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>));

        foreach (var validator in validators)
        {
            var interfaceType = validator.BaseType!.GetGenericArguments()[0];
            var serviceType = typeof(IValidator<>).MakeGenericType(interfaceType);
            services.AddScoped(serviceType, validator);
        }

        return services;
    }
}
