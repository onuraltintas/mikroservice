using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Shared.Security.Authorization;

/// <summary>
/// Gives a forbidden authorization result a Turkish ProblemDetails body so clients can tell a
/// missing MFA step-up (<c>Auth.MfaRequired</c>) from a missing role or permission
/// (<c>Auth.Forbidden</c>). Challenges and successful results keep the default behaviour.
/// </summary>
public sealed class AuthorizationProblemResultHandler : IAuthorizationMiddlewareResultHandler
{
    public const string MfaRequiredCode = "Auth.MfaRequired";
    public const string ForbiddenCode = "Auth.Forbidden";
    public const string MfaRequiredMessage =
        "Bu işlem için MFA doğrulaması gerekiyor. Profil ayarlarından MFA doğrulamasını tamamlayın.";
    public const string ForbiddenMessage = "Bu işlem için yetkiniz yok.";

    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Forbidden || context.Response.HasStarted)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var failed = authorizeResult.AuthorizationFailure?.FailedRequirements.ToArray() ?? [];
        // A missing role or permission wins: completing MFA would not help the caller.
        var mfaOnly = failed.Length > 0 && failed.All(requirement => requirement is MfaPolicyRequirement);

        foreach (var scheme in policy.AuthenticationSchemes)
        {
            await context.ForbidAsync(scheme);
        }

        if (policy.AuthenticationSchemes.Count == 0)
        {
            await context.ForbidAsync();
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Type = "https://eduplatform.dev/problems/forbidden",
            Detail = mfaOnly ? MfaRequiredMessage : ForbiddenMessage,
            Instance = context.Request.Path,
        };
        problem.Extensions["code"] = mfaOnly ? MfaRequiredCode : ForbiddenCode;
        await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }
}
