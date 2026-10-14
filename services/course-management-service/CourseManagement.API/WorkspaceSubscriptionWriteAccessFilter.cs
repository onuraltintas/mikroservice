using System.Security.Claims;
using CourseManagement.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

internal sealed class ReadOnlyWorkspaceOperationMetadata { }

public sealed class WorkspaceSubscriptionWriteAccessFilter(CourseManagementDbContext db) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        if (HttpMethods.IsGet(httpContext.Request.Method) || HttpMethods.IsHead(httpContext.Request.Method)
            || HttpMethods.IsOptions(httpContext.Request.Method))
            return await next(context);

        var userId = Guid.TryParse(httpContext.User.FindFirstValue("sub"), out var parsedUserId)
            ? parsedUserId : Guid.Empty;
        var target = await ResolveTargetAsync(httpContext.Request.RouteValues, userId, httpContext.RequestAborted);
        if (target is null) return await next(context);

        var workspace = await db.Workspaces.AsNoTracking()
            .Where(item => item.Id == target.Value.WorkspaceId)
            .Select(item => new { item.IsErasureReadOnly, item.OwnerUserId })
            .SingleOrDefaultAsync(httpContext.RequestAborted);
        if (workspace is null) return await next(context);

        if (!target.Value.IsInvitationRecipient
            && workspace.OwnerUserId != userId
            && !await db.Memberships.AsNoTracking().AnyAsync(member => member.WorkspaceId == target.Value.WorkspaceId
                && member.UserId == userId && member.IsActive, httpContext.RequestAborted))
            return Results.NotFound();

        var allowReadOnlyOperation = httpContext.GetEndpoint()?.Metadata
            .GetMetadata<ReadOnlyWorkspaceOperationMetadata>() is not null;
        if (!allowReadOnlyOperation && workspace.IsErasureReadOnly)
            return Results.Conflict(new { code = "Workspace.ErasureReadOnly" });

        var subscription = await db.WorkspaceSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.WorkspaceId == target.Value.WorkspaceId, httpContext.RequestAborted);
        if (subscription is not null && !allowReadOnlyOperation && !subscription.CanWriteAt(DateTimeOffset.UtcNow))
            return Results.Conflict(new { code = "Subscription.ReadOnly" });

        return await next(context);
    }

    private async Task<WriteTarget?> ResolveTargetAsync(RouteValueDictionary routeValues, Guid userId,
        CancellationToken cancellationToken)
    {
        if (TryGetGuid(routeValues, "workspaceId", out var workspaceId))
            return new WriteTarget(workspaceId, false);

        if (TryGetGuid(routeValues, "invitationId", out var invitationId))
        {
            var invitationWorkspaceId = await db.Invitations.AsNoTracking()
                .Where(item => item.Id == invitationId && item.TargetUserId == userId)
                .Select(item => (Guid?)item.WorkspaceId).SingleOrDefaultAsync(cancellationToken);
            if (invitationWorkspaceId is { } invitedWorkspaceId)
                return new WriteTarget(invitedWorkspaceId, true);
        }

        if (TryGetGuid(routeValues, "assignmentId", out var assignmentId))
        {
            var assignmentWorkspaceId = await db.Assignments.AsNoTracking()
                .Where(item => item.Id == assignmentId).Select(item => (Guid?)item.WorkspaceId)
                .SingleOrDefaultAsync(cancellationToken);
            if (assignmentWorkspaceId is { } assignedWorkspaceId)
                return new WriteTarget(assignedWorkspaceId, false);
        }

        if (TryGetGuid(routeValues, "lessonId", out var lessonId))
        {
            var lessonWorkspaceId = await db.LessonSessions.AsNoTracking()
                .Where(item => item.Id == lessonId).Select(item => (Guid?)item.WorkspaceId)
                .SingleOrDefaultAsync(cancellationToken);
            if (lessonWorkspaceId is { } lessonWorkspace)
                return new WriteTarget(lessonWorkspace, false);
        }

        return null;
    }

    private static bool TryGetGuid(RouteValueDictionary routeValues, string name, out Guid id)
    {
        if (routeValues.TryGetValue(name, out var value) && Guid.TryParse(value?.ToString(), out id)) return true;
        id = Guid.Empty;
        return false;
    }

    private readonly record struct WriteTarget(Guid WorkspaceId, bool IsInvitationRecipient);
}
