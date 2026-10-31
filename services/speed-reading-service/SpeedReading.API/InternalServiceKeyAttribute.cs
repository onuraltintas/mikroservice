using Microsoft.AspNetCore.Mvc.Filters;

namespace SpeedReading.API;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class InternalServiceKeyAttribute : Attribute, IAsyncResourceFilter
{
    public Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next) => next();
}
