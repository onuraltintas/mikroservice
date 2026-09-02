using EduPlatform.Shared.Security.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Notification.API;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class InternalServiceKeyAttribute : Attribute, IAsyncResourceFilter
{
    public Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        if (!InternalServiceAuthentication.IsValid(context.HttpContext.Request, configuration))
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        return next();
    }
}
