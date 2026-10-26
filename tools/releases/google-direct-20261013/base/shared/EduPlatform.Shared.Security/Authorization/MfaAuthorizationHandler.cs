using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;

namespace EduPlatform.Shared.Security.Authorization;

public sealed class MfaPolicyRequirement : IAuthorizationRequirement
{
    public MfaPolicyRequirement(string? category = null)
    {
        Category = string.IsNullOrWhiteSpace(category)
            ? null
            : MfaOperationCategories.Normalize(category);
    }

    public string? Category { get; }
}

public interface IMfaPolicyStore
{
    Task<string> GetModeAsync(string category, CancellationToken cancellationToken = default);
}

public sealed class MfaAuthorizationHandler : AuthorizationHandler<MfaPolicyRequirement>
{
    private readonly IMfaPolicyStore _policyStore;

    public MfaAuthorizationHandler(IMfaPolicyStore policyStore)
    {
        _policyStore = policyStore;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MfaPolicyRequirement requirement)
    {
        var httpContext = ResolveHttpContext(context.Resource);
        var category = ResolveCategory(httpContext, context.Resource, requirement.Category);

        // Legacy MfaRequired attributes that predate category metadata are
        // governed by the system policy. This keeps the category controls
        // complete while the policy store still fails closed when its value is
        // missing or invalid.
        category ??= MfaOperationCategories.System;

        var mode = await _policyStore.GetModeAsync(category, httpContext?.RequestAborted ?? CancellationToken.None);
        if (mode.Equals(MfaPolicyModes.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return;
        }

        if (mode.Equals(MfaPolicyModes.MutationsOnly, StringComparison.OrdinalIgnoreCase)
            && httpContext is not null
            && IsSafeRequestMethod(httpContext.Request.Method))
        {
            context.Succeed(requirement);
            return;
        }

        RequireMfa(context, requirement);
    }

    private static void RequireMfa(
        AuthorizationHandlerContext context,
        MfaPolicyRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.Claims.Any(claim =>
                claim.Type == "amr"
                && claim.Value.Equals("mfa", StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }
    }

    private static bool IsSafeRequestMethod(string method) =>
        HttpMethods.IsGet(method)
        || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method);

    private static HttpContext? ResolveHttpContext(object? resource) =>
        resource switch
        {
            HttpContext httpContext => httpContext,
            AuthorizationFilterContext mvcContext => mvcContext.HttpContext,
            _ => null
        };

    private static string? ResolveCategory(
        HttpContext? httpContext,
        object? resource,
        string? requirementCategory)
    {
        var metadata = httpContext?.GetEndpoint()?.Metadata.GetMetadata<MfaCategoryAttribute>();
        if (metadata is not null)
        {
            return metadata.Category;
        }

        var genericMetadata = httpContext?.GetEndpoint()?.Metadata.GetMetadata<IMfaCategoryMetadata>();
        if (genericMetadata?.Category is not null)
        {
            return genericMetadata.Category;
        }

        if (resource is AuthorizationFilterContext mvcContext)
        {
            if (mvcContext.ActionDescriptor is ControllerActionDescriptor controllerAction)
            {
                var actionCategory = controllerAction.MethodInfo
                    .GetCustomAttribute<MfaCategoryAttribute>(inherit: true)
                    ?.Category;
                if (actionCategory is not null)
                {
                    return actionCategory;
                }

                var controllerCategory = controllerAction.ControllerTypeInfo
                    .GetCustomAttribute<MfaCategoryAttribute>(inherit: true)
                    ?.Category;
                if (controllerCategory is not null)
                {
                    return controllerCategory;
                }

                var permissionCategory = controllerAction.MethodInfo
                    .GetCustomAttributes<HasPermissionAttribute>(inherit: true)
                    .Select(attribute => attribute.Category)
                    .FirstOrDefault(category => category is not null);
                if (permissionCategory is not null)
                {
                    return permissionCategory;
                }
            }

            metadata = mvcContext.ActionDescriptor.EndpointMetadata
                .OfType<MfaCategoryAttribute>()
                .FirstOrDefault();
            if (metadata is not null)
            {
                return metadata.Category;
            }

            genericMetadata = mvcContext.ActionDescriptor.EndpointMetadata
                .OfType<IMfaCategoryMetadata>()
                .FirstOrDefault(item => item.Category is not null);
            if (genericMetadata?.Category is not null)
            {
                return genericMetadata.Category;
            }
        }

        return requirementCategory;
    }
}
