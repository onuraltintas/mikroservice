using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Coaching.API.Security;

public sealed class CoachingProfileExceptionFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is BusinessRuleException { Code: "Profiles.Conflict" } error)
            context.Result = new ConflictObjectResult(new { code = error.Code, message = error.Message });
        else if (context.Exception is KeyNotFoundException)
            context.Result = new NotFoundObjectResult(new { code = "Profiles.NotFound", message = "Koçluk kaydı bulunamadı." });
        else return;
        context.ExceptionHandled = true;
    }
}
