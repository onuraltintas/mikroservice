using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

/// <summary>
/// Opens the workspace of a maxkampus.com registration (Identity's <see cref="CourseManagementWorkspaceRequestedV1"/>).
/// One registration opens one workspace: a repeated message, or an owner who already has one, opens nothing.
/// The panel stays closed until a plan is approved (billing), as for any workspace.
/// </summary>
public sealed class CourseManagementWorkspaceRequestedConsumer(CourseManagementDbContext db, ILogger<CourseManagementWorkspaceRequestedConsumer> logger)
    : IConsumer<CourseManagementWorkspaceRequestedV1>
{
    public async Task Consume(ConsumeContext<CourseManagementWorkspaceRequestedV1> context)
    {
        var message = context.Message;
        var type = message.Kind switch
        {
            "institution" => WorkspaceType.Institution,
            "teacher" => WorkspaceType.IndependentTeacher,
            _ => (WorkspaceType?)null
        };
        var name = message.WorkspaceName?.Trim() ?? string.Empty;
        if (type is null || message.UserId == Guid.Empty || message.WorkspaceId == Guid.Empty || name.Length is 0 or > 200)
        {
            logger.LogWarning("A workspace request with invalid data was ignored.");
            return;
        }
        var ct = context.CancellationToken;
        if (await db.Workspaces.AnyAsync(item => item.Id == message.WorkspaceId, ct)) return;
        if (await db.Memberships.AnyAsync(item => item.UserId == message.UserId && item.Role == WorkspaceRole.Owner, ct)) return;

        var workspace = Workspace.Create(message.WorkspaceId, message.UserId, name, type.Value);
        db.Workspaces.Add(workspace);
        db.Memberships.Add(WorkspaceMembership.Create(workspace.Id, message.UserId, WorkspaceRole.Owner));
        var displayName = message.DisplayName?.Trim();
        if (!string.IsNullOrWhiteSpace(displayName) && await db.UserProfiles.FindAsync([message.UserId], ct) is null)
            db.UserProfiles.Add(CourseUserProfile.Create(message.UserId, displayName.Length > 200 ? displayName[..200] : displayName));
        db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspace.Id, message.UserId, workspace.Id, "Workspace.Registered"));
        await db.SaveChangesAsync(ct);
    }
}
