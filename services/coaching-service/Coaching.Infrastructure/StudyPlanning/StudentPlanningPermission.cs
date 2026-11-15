using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

/// <summary>
/// User decision 15.11.2026. A student with an active teacher makes or changes her own study plan and weekly schedule only
/// when the teacher allows it (off by default). A student without a teacher, including one who is only a member of an
/// institution, plans freely. Reporting what was studied is never restricted.
/// </summary>
/// <param name="HasTeacher">An active teacher assignment exists.</param>
/// <param name="AllowedByTeacher">The switch on the student's profile; it matters only with a teacher.</param>
/// <param name="PartialPlanEnd">The plan in force covers only part of the way to the exam and ends on this day; else null.</param>
public sealed record StudentPlanningState(bool HasTeacher, bool AllowedByTeacher, DateOnly? PartialPlanEnd = null)
{
    public bool StudentMayPlan => !HasTeacher || AllowedByTeacher;
}

public static class StudentPlanningPermission
{
    public const string Code = "Authorization.TeacherPlans";
    public const string Message = "Çalışma planını öğretmenin hazırlıyor. Kendi planını yapabilmen için öğretmeninin izin vermesi gerekir.";

    public static async Task<StudentPlanningState> StateAsync(CoachingDbContext db, Guid studentId, CancellationToken ct)
    {
        var hasTeacher = await db.CoachingTeacherStudentAssignments.AsNoTracking()
            .AnyAsync(x => x.StudentUserId == studentId && x.IsActive, ct);
        var allowed = await db.CoachingStudentProfiles.AsNoTracking().Where(x => x.UserId == studentId)
            .Select(x => x.CanPlanOwnStudy).SingleOrDefaultAsync(ct);
        // The weekly source is one JSON value: read for the plan in force only.
        var source = await db.StudyPlanRevisions.AsNoTracking().Where(x => x.StudentId == studentId && x.IsActive)
            .Select(x => x.WeeklySource).SingleOrDefaultAsync(ct);
        return new(hasTeacher, allowed, source is { Partial: true } ? source.EndDate : null);
    }

    /// <summary>For the student's own plan-changing requests.</summary>
    public static async Task RequireStudentMayPlanAsync(CoachingDbContext db, Guid studentId, CancellationToken ct)
    {
        if (!(await StateAsync(db, studentId, ct)).StudentMayPlan) throw new BusinessRuleException(Code, Message);
    }
}

/// <summary>The teacher's switch and what the student is told about it.</summary>
public sealed class StudentPlanningPermissionService(CoachingDbContext db, ICoachingAccessPolicy access,
    ICoachingEventPublisher events, CoachingStudyPlanAccess staffAccess)
{
    public const string TeacherPlansNotice = "Çalışma planını artık öğretmenin hazırlıyor; planı sen değiştiremezsin, çalışmalarını işaretlemeye devam edebilirsin.";
    public const string StudentPlansNotice = "Öğretmenin kendi çalışma planını yapmana izin verdi.";
    public const string WeeklyScheduleNotice = "Öğretmenin haftalık çalışma düzenini güncelledi.";

    public async Task<StudentPlanningState> GetOwnAsync(CancellationToken ct)
    {
        if (access.CurrentUserId is not { } student || !access.IsCurrentStudent(student))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu işlem yalnız öğrencinin kendi hesabıyla kullanılabilir.");
        return await StudentPlanningPermission.StateAsync(db, student, ct);
    }

    public async Task<StudentPlanningState> GetForStudentAsync(Guid studentId, CancellationToken ct)
    {
        await staffAccess.RequireAsync(studentId, ct);
        return await StudentPlanningPermission.StateAsync(db, studentId, ct);
    }

    public async Task<StudentPlanningState> SetForStudentAsync(Guid studentId, bool allowed, CancellationToken ct)
    {
        await staffAccess.RequireTeacherWriteAsync(studentId, ct);
        var profile = await db.CoachingStudentProfiles.SingleOrDefaultAsync(x => x.UserId == studentId, ct)
            ?? throw new BusinessRuleException("StudyPlanning.NotFound", "Öğrenci profili bulunamadı.");
        if (profile.CanPlanOwnStudy != allowed)
        {
            profile.SetCanPlanOwnStudy(allowed, access.CurrentUserId!.Value);
            await NotifyAsync(studentId, allowed ? StudentPlansNotice : TeacherPlansNotice, ct);
            await db.SaveChangesAsync(ct);
        }
        return await StudentPlanningPermission.StateAsync(db, studentId, ct);
    }

    /// <summary>After a teacher saved the student's weekly schedule: the student hears about it.</summary>
    public async Task WeeklyScheduleSavedByTeacherAsync(Guid studentId, CancellationToken ct)
    {
        await NotifyAsync(studentId, WeeklyScheduleNotice, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task NotifyAsync(Guid studentId, string text, CancellationToken ct)
    {
        // The plan notice the student already knows; it points at the active plan when there is one.
        var active = await db.StudyPlanRevisions.AsNoTracking().Where(x => x.StudentId == studentId && x.IsActive)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        await events.PublishAsync(new StudyPlanUpdatedEvent(active ?? studentId, studentId, text), ct);
    }
}
