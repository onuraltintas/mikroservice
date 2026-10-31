using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Coaching.API.Security;

/// <summary>
/// Coaching audit K1: payment rules (contract not accepted, a notice already under review, a used-up discount code, an
/// erased account) reach the payment screens as <c>{ success, code, message }</c>, the shape they show to the user.
/// Authorization failures keep the global 403.
/// </summary>
public sealed class CoachingPaymentExceptionFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case BusinessRuleException error when !error.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase):
                var body = new { success = false, code = error.Code, message = error.Message };
                context.Result = error.Code is "Subscription.TransferPending" or "Sales.CouponLimit" or "Subscription.UserErased"
                    ? new ConflictObjectResult(body) : new BadRequestObjectResult(body);
                break;
            case ArgumentException error:
                context.Result = new BadRequestObjectResult(new { success = false, message = error.Message });
                break;
            default:
                return;
        }
        context.ExceptionHandled = true;
    }
}
