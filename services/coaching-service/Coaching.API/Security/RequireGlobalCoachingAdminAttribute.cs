using Coaching.Application.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Coaching.API.Security;

/// <summary>
/// Platform-wide coaching administration (CMS, newsletter subscribers, subscription plans, bank settings and
/// transfer approvals) is limited to the global administrator. A permission alone is not enough: role
/// permissions can be edited at runtime, and granting one to institution administrators must not turn every
/// institution administrator into a platform administrator.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireGlobalCoachingAdminAttribute : TypeFilterAttribute
{
    public RequireGlobalCoachingAdminAttribute() : base(typeof(Filter)) { }

    private sealed class Filter(ICoachingAdminScopeAuthorization scopes) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            bool global;
            try { global = (await scopes.RequireReadScopeAsync(context.HttpContext.RequestAborted)).IsGlobal; }
            catch (BusinessRuleException exception) when (exception.Code.StartsWith("Authorization.", StringComparison.Ordinal))
            { global = false; }

            if (!global)
            {
                context.Result = new ObjectResult(new { success = false, message = "Bu işlem yalnız platform yöneticisine açıktır." })
                { StatusCode = StatusCodes.Status403Forbidden };
                return;
            }
            await next();
        }
    }
}
