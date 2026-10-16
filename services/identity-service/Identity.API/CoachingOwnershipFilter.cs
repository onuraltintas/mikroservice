using Identity.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Identity.API;

/// <summary>Archived profile routes cannot read or mutate Coaching relationships after the ownership cutover.</summary>
public sealed class CoachingOwnershipFilter : IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var controller = (context.ActionDescriptor as ControllerActionDescriptor)?.ControllerTypeInfo.AsType();
        if (controller == typeof(TeachersController) || controller == typeof(StudentsController)
            || controller == typeof(InstitutionController) || controller == typeof(CoachingInstitutionMembersController)
            || controller == typeof(InvitationsController))
            context.Result = new ObjectResult(new { code = "Profiles.OwnedByCoaching",
                message = "Bu işlem Koçluk servisine taşındı. Güncel Koçluk panelini kullanın." }) { StatusCode = 410 };
    }
    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
