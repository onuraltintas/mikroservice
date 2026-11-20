using CourseManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Infrastructure;

/// <summary>
/// A teacher invited by e-mail can be given groups, lessons, working hours, leave and one-to-one students before
/// accepting (setup guide, 20.11.2026): the open invitation's id stands for the teacher. On acceptance it all moves to
/// the teacher's account, in the same save as the acceptance.
/// </summary>
public static class TeacherInvitationHandOver
{
    /// <summary>The invited teacher accepted: everything given to the invitation (its id stood for the teacher) becomes theirs.</summary>
    public static async Task<int> HandOverAsync(CourseManagementDbContext db, Guid workspaceId, Guid invitationId, Guid teacherUserId, CancellationToken ct)
    {
        var count = 0;
        foreach (var group in await db.Groups.Where(item => item.WorkspaceId == workspaceId && item.TeacherUserId == invitationId).ToListAsync(ct)) { group.ChangeTeacher(teacherUserId); count++; }
        foreach (var lesson in await db.LessonSessions.Where(item => item.WorkspaceId == workspaceId && item.TeacherUserId == invitationId).ToListAsync(ct)) { lesson.ChangeTeacher(teacherUserId); count++; }
        foreach (var series in await db.LessonSeries.Where(item => item.WorkspaceId == workspaceId && item.TeacherUserId == invitationId).ToListAsync(ct)) { series.ChangeTeacher(teacherUserId); count++; }
        foreach (var window in await db.TeacherAvailabilityWindows.Where(item => item.WorkspaceId == workspaceId && item.TeacherUserId == invitationId).ToListAsync(ct)) { window.ChangeTeacher(teacherUserId); count++; }
        foreach (var block in await db.TeacherTimeOffs.Where(item => item.WorkspaceId == workspaceId && item.TeacherUserId == invitationId).ToListAsync(ct)) { block.ChangeTeacher(teacherUserId); count++; }
        foreach (var student in await db.Students.Where(item => item.WorkspaceId == workspaceId && item.OneToOneTeacherUserId == invitationId).ToListAsync(ct)) { student.ReplaceOneToOneTeacher(invitationId, teacherUserId); count++; }
        return count;
    }

}
