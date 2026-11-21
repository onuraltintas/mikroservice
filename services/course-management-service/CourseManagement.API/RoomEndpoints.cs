using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

public sealed record UpdateBranchRequest(string? Name);
public sealed record UpdateRoomRequest(string? Name, int? Capacity, bool Force);

/// <summary>
/// Rooms and branches can be corrected, archived and deleted (user decision 20.11.2026; a branch stays only a group of
/// rooms). Deleting never breaks history: a room used by a lesson that has started is archived instead (Room.InUse);
/// a room used only by planned lessons is deleted after the owner confirms, and those lessons keep going without a room.
/// All of it is the owner's and goes to the change record.
/// </summary>
public static class RoomEndpoints
{
    public static void MapRoomEndpoints(this RouteGroupBuilder api)
    {
        api.MapPut("/workspaces/{workspaceId:guid}/branches/{branchId:guid}", async (Guid workspaceId, Guid branchId,
            UpdateBranchRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await SetupEndpoints.OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var branch = await db.Branches.SingleOrDefaultAsync(item => item.Id == branchId && item.WorkspaceId == workspaceId, ct);
            if (branch is null) return Results.NotFound();
            var name = request.Name?.Trim() ?? string.Empty;
            if (name.Length is < 1 or > 120) return Results.BadRequest(new { code = "Branch.InvalidInput" });
            if (await db.Branches.AnyAsync(item => item.WorkspaceId == workspaceId && item.Id != branchId && item.Name == name, ct))
                return Results.Conflict(new { code = "Branch.DuplicateName" });
            branch.Rename(name);
            Audit(db, workspaceId, user, branchId, "CourseBranch.Renamed");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { branch.Id, branch.Name, branch.ArchivedAt });
        });

        api.MapPost("/workspaces/{workspaceId:guid}/branches/{branchId:guid}/archive", async (Guid workspaceId, Guid branchId,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await SetupEndpoints.OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var branch = await db.Branches.SingleOrDefaultAsync(item => item.Id == branchId && item.WorkspaceId == workspaceId, ct);
            if (branch is null) return Results.NotFound();
            if (await db.Rooms.AnyAsync(room => room.BranchId == branchId && room.ArchivedAt == null, ct))
                return Results.Conflict(new { code = "Branch.HasActiveRooms" });
            branch.Archive(DateTimeOffset.UtcNow);
            Audit(db, workspaceId, user, branchId, "CourseBranch.Archived");
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        api.MapPost("/workspaces/{workspaceId:guid}/branches/{branchId:guid}/restore", async (Guid workspaceId, Guid branchId,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await SetupEndpoints.OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var branch = await db.Branches.SingleOrDefaultAsync(item => item.Id == branchId && item.WorkspaceId == workspaceId, ct);
            if (branch is null) return Results.NotFound();
            branch.Restore();
            Audit(db, workspaceId, user, branchId, "CourseBranch.Restored");
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        api.MapDelete("/workspaces/{workspaceId:guid}/branches/{branchId:guid}", async (Guid workspaceId, Guid branchId,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await SetupEndpoints.OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var branch = await db.Branches.SingleOrDefaultAsync(item => item.Id == branchId && item.WorkspaceId == workspaceId, ct);
            if (branch is null) return Results.NotFound();
            if (await db.Rooms.AnyAsync(room => room.BranchId == branchId, ct)) return Results.Conflict(new { code = "Branch.HasRooms" });
            db.Branches.Remove(branch);
            Audit(db, workspaceId, user, branchId, "CourseBranch.Deleted");
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        api.MapPut("/workspaces/{workspaceId:guid}/rooms/{roomId:guid}", async (Guid workspaceId, Guid roomId,
            UpdateRoomRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await SetupEndpoints.OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var room = await db.Rooms.SingleOrDefaultAsync(item => item.Id == roomId && item.WorkspaceId == workspaceId, ct);
            if (room is null) return Results.NotFound();
            var name = request.Name?.Trim() ?? string.Empty;
            if (name.Length is < 1 or > 120 || request.Capacity is < 1) return Results.BadRequest(new { code = "Room.InvalidInput" });
            if (await db.Rooms.AnyAsync(item => item.BranchId == room.BranchId && item.Id != roomId && item.Name == name, ct))
                return Results.Conflict(new { code = "Room.DuplicateName" });
            // A smaller room: the planned lessons that no longer fit are counted and the owner confirms; they stay as they are.
            if (request.Capacity is int capacity && (room.Capacity is null || capacity < room.Capacity) && !request.Force)
            {
                var now = DateTimeOffset.UtcNow;
                var crowded = await db.LessonSessions.AsNoTracking()
                    .Where(lesson => lesson.WorkspaceId == workspaceId && lesson.RoomId == roomId
                        && lesson.Status == LessonSessionStatus.Scheduled && lesson.StartsAt > now)
                    .CountAsync(lesson => db.LessonParticipants.Count(participant => participant.LessonSessionId == lesson.Id && participant.IsActive) > capacity, ct);
                if (crowded > 0) return Results.Conflict(new { code = "Room.CapacityBelowLessons", lessons = crowded });
            }
            var changes = new List<string>();
            if (room.Name != name) changes.Add("CourseRoom.Renamed");
            if (room.Capacity != request.Capacity) changes.Add("CourseRoom.CapacityChanged");
            room.Update(name, request.Capacity);
            foreach (var change in changes) Audit(db, workspaceId, user, roomId, change);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { room.Id, room.BranchId, room.Name, room.Capacity, room.ArchivedAt });
        });

        api.MapPost("/workspaces/{workspaceId:guid}/rooms/{roomId:guid}/archive", async (Guid workspaceId, Guid roomId, bool? force,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await SetupEndpoints.OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var room = await db.Rooms.SingleOrDefaultAsync(item => item.Id == roomId && item.WorkspaceId == workspaceId, ct);
            if (room is null) return Results.NotFound();
            var planned = await PlannedLessons(db, workspaceId, roomId, ct);
            if (planned > 0 && force != true) return Results.Conflict(new { code = "Room.HasPlannedLessons", lessons = planned });
            room.Archive(DateTimeOffset.UtcNow);
            Audit(db, workspaceId, user, roomId, "CourseRoom.Archived");
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        api.MapPost("/workspaces/{workspaceId:guid}/rooms/{roomId:guid}/restore", async (Guid workspaceId, Guid roomId,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await SetupEndpoints.OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var room = await db.Rooms.SingleOrDefaultAsync(item => item.Id == roomId && item.WorkspaceId == workspaceId, ct);
            if (room is null) return Results.NotFound();
            var branch = await db.Branches.SingleAsync(item => item.Id == room.BranchId, ct);
            branch.Restore();
            room.Restore();
            Audit(db, workspaceId, user, roomId, "CourseRoom.Restored");
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        api.MapDelete("/workspaces/{workspaceId:guid}/rooms/{roomId:guid}", async (Guid workspaceId, Guid roomId, bool? force,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (await SetupEndpoints.OwnedAsync(db, workspaceId, user, ct) is null) return Results.NotFound();
            var room = await db.Rooms.SingleOrDefaultAsync(item => item.Id == roomId && item.WorkspaceId == workspaceId, ct);
            if (room is null) return Results.NotFound();
            var now = DateTimeOffset.UtcNow;
            // A lesson that has started keeps its room in the record: archive instead.
            if (await db.LessonSessions.AnyAsync(lesson => lesson.WorkspaceId == workspaceId && lesson.RoomId == roomId && lesson.StartsAt <= now, ct))
                return Results.Conflict(new { code = "Room.InUse" });
            var planned = await PlannedLessons(db, workspaceId, roomId, ct);
            if (planned > 0 && force != true) return Results.Conflict(new { code = "Room.HasPlannedLessons", lessons = planned });
            foreach (var lesson in await db.LessonSessions.Where(item => item.WorkspaceId == workspaceId && item.RoomId == roomId).ToListAsync(ct))
                lesson.ClearRoom();
            foreach (var series in await db.LessonSeries.Where(item => item.WorkspaceId == workspaceId && item.RoomId == roomId).ToListAsync(ct))
                series.ClearRoom();
            // One save: EF clears the lessons' room before it deletes the room.
            db.Rooms.Remove(room);
            Audit(db, workspaceId, user, roomId, "CourseRoom.Deleted");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { freedLessons = planned });
        });
    }

    private static Task<int> PlannedLessons(CourseManagementDbContext db, Guid workspaceId, Guid roomId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return db.LessonSessions.CountAsync(lesson => lesson.WorkspaceId == workspaceId && lesson.RoomId == roomId
            && lesson.Status == LessonSessionStatus.Scheduled && lesson.StartsAt > now, ct);
    }

    private static void Audit(CourseManagementDbContext db, Guid workspaceId, ClaimsPrincipal user, Guid entityId, string action) =>
        db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspaceId, SetupEndpoints.UserId(user), entityId, action));
}
