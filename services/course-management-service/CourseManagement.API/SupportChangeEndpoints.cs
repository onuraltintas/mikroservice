using System.Data;
using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

/// <summary>
/// Corrections under a support grant the owner opened with "düzeltme de yapılabilir" (19.11.2026). A short, fixed list:
/// a student's or guardian's contact details, a student's attendance, a single lesson's time, switching an e-mail kind
/// off. None of them tells students or guardians anything (no e-mail, no notice), money records are never touched, and
/// each one is written to the support log with what it was and what it became (codes and field names, no personal data).
/// </summary>
public static class SupportChangeEndpoints
{
    public static void MapSupportChangeEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/course-management/v1/admin/workspaces/{workspaceId:guid}/support")
            .RequireAuthorization("CourseManagementAdmin");

        admin.MapPut("/students/{studentId:guid}", async (Guid workspaceId, Guid studentId, SupportContactRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var grant = await SupportAccessEndpoints.ChangeGrantAsync(db, workspaceId, user, SupportAccessSections.Students, ct);
            if (grant is null) return Results.NotFound();
            if (!ValidContact(request)) return Results.BadRequest(new { code = "Student.InvalidInput" });
            var student = await db.Students.SingleOrDefaultAsync(item => item.Id == studentId && item.WorkspaceId == workspaceId, ct);
            if (student is null) return Results.NotFound();
            var changed = ChangedFields(student.DisplayName, student.Email, student.PhoneNumber, request);
            if (changed.Length == 0) return Results.Ok(new { student.Id, student.DisplayName, student.Email, student.PhoneNumber });
            try { student.Update(request.DisplayName!, request.Email, request.PhoneNumber); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "Student.InvalidInput" }); }
            var now = DateTimeOffset.UtcNow;
            db.SupportAccessLogs.Add(SupportAccessLog.Change(grant, SupportAccessSections.Students, "Öğrenci iletişim bilgisi düzeltildi",
                student.Id, null, string.Join(", ", changed), now));
            AssignmentEndpoints.AddAudit(db, workspaceId, grant.AdminUserId, student.Id, "Support.Student.Updated");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { student.Id, student.DisplayName, student.Email, student.PhoneNumber });
        });

        admin.MapPut("/guardians/{guardianId:guid}", async (Guid workspaceId, Guid guardianId, SupportContactRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var grant = await SupportAccessEndpoints.ChangeGrantAsync(db, workspaceId, user, SupportAccessSections.Students, ct);
            if (grant is null) return Results.NotFound();
            if (!ValidContact(request)) return Results.BadRequest(new { code = "Guardian.InvalidInput" });
            var guardian = await db.Guardians.SingleOrDefaultAsync(item => item.Id == guardianId && item.WorkspaceId == workspaceId, ct);
            if (guardian is null) return Results.NotFound();
            var changed = ChangedFields(guardian.DisplayName, guardian.Email, guardian.PhoneNumber, request);
            if (changed.Length == 0) return Results.Ok(new { guardian.Id, guardian.DisplayName, guardian.Email, guardian.PhoneNumber });
            try { guardian.Update(request.DisplayName!, request.Email, request.PhoneNumber); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "Guardian.InvalidInput" }); }
            db.SupportAccessLogs.Add(SupportAccessLog.Change(grant, SupportAccessSections.Students, "Veli iletişim bilgisi düzeltildi",
                guardian.Id, null, string.Join(", ", changed), DateTimeOffset.UtcNow));
            AssignmentEndpoints.AddAudit(db, workspaceId, grant.AdminUserId, guardian.Id, "Support.Guardian.Updated");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { guardian.Id, guardian.DisplayName, guardian.Email, guardian.PhoneNumber });
        });

        // Attendance of a lesson that has begun, also after it was completed, unless the lesson already charged a fee.
        admin.MapPut("/lessons/{lessonId:guid}/students/{studentId:guid}/attendance", async (Guid workspaceId, Guid lessonId,
            Guid studentId, UpdateLessonAttendanceRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var grant = await SupportAccessEndpoints.ChangeGrantAsync(db, workspaceId, user, SupportAccessSections.Schedule, ct);
            if (grant is null) return Results.NotFound();
            if (!Enum.IsDefined(typeof(LessonAttendanceStatus), request.Status)) return Results.BadRequest(new { code = "Attendance.InvalidStatus" });
            var status = (LessonAttendanceStatus)request.Status;
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var lesson = await db.LessonSessions.SingleOrDefaultAsync(item => item.Id == lessonId && item.WorkspaceId == workspaceId, ct);
                if (lesson is null) return Results.NotFound();
                var now = DateTimeOffset.UtcNow;
                if (lesson.Status == LessonSessionStatus.Cancelled) return Results.Conflict(new { code = "Attendance.LessonClosed" });
                if (lesson.StartsAt > now) return Results.Conflict(new { code = "Attendance.LessonNotStarted" });
                if (!await db.LessonParticipants.AnyAsync(item => item.WorkspaceId == workspaceId && item.LessonSessionId == lessonId
                        && item.StudentId == studentId, ct))
                    return Results.NotFound();
                if (await db.Installments.AnyAsync(item => item.WorkspaceId == workspaceId && item.SourceLessonId == lessonId
                        && item.StudentId == studentId, ct))
                    return Results.Conflict(new { code = "SupportAccess.LessonCharged" });
                var attendance = await db.AttendanceRecords.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId
                    && item.LessonSessionId == lessonId && item.StudentId == studentId, ct);
                var before = attendance?.Status.ToString();
                if (attendance?.Status == status) { await transaction.CommitAsync(ct); return Results.Ok(new { lessonId, studentId, status = status.ToString() }); }
                // Marked by the administrator, who is not a member: the absence e-mail is not sent for it.
                if (attendance is null)
                {
                    attendance = AttendanceRecord.Create(Guid.NewGuid(), workspaceId, lessonId, studentId, status, grant.AdminUserId, now);
                    db.AttendanceRecords.Add(attendance);
                }
                else attendance.UpdateStatus(status, grant.AdminUserId, now);
                db.SupportAccessLogs.Add(SupportAccessLog.Change(grant, SupportAccessSections.Schedule, "Yoklama düzeltildi", studentId,
                    before ?? "Yok", status.ToString(), now));
                AssignmentEndpoints.AddAudit(db, workspaceId, grant.AdminUserId, attendance.Id, "Support.Attendance.Updated");
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.Ok(new { lessonId, studentId, status = status.ToString() });
            }
            catch (Exception exception) when (LearningEndpoints.IsSerializationFailure(exception))
            {
                return Results.Conflict(new { code = "Attendance.ConcurrentUpdate" });
            }
        });

        // A single future lesson's time, with the same clash checks the institution's own move has.
        admin.MapPut("/lessons/{lessonId:guid}/schedule", async (Guid workspaceId, Guid lessonId, RescheduleLessonRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var grant = await SupportAccessEndpoints.ChangeGrantAsync(db, workspaceId, user, SupportAccessSections.Schedule, ct);
            if (grant is null) return Results.NotFound();
            var startsAt = request.StartsAt.ToUniversalTime();
            var endsAt = request.EndsAt.ToUniversalTime();
            var now = DateTimeOffset.UtcNow;
            if (request.RoomId == Guid.Empty || startsAt <= now || endsAt <= startsAt || endsAt - startsAt > TimeSpan.FromHours(8))
                return Results.BadRequest(new { code = "Lesson.InvalidTime" });
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var lesson = await db.LessonSessions.SingleOrDefaultAsync(item => item.Id == lessonId && item.WorkspaceId == workspaceId, ct);
                if (lesson is null) return Results.NotFound();
                if (lesson.Status != LessonSessionStatus.Scheduled) return Results.Conflict(new { code = "Lesson.NotScheduled" });
                if (lesson.SeriesId is not null) return Results.Conflict(new { code = "Lesson.SeriesOccurrenceMustUseSeriesEditor" });
                if (lesson.StartsAt <= now || await db.AttendanceRecords.AnyAsync(item => item.LessonSessionId == lessonId, ct))
                    return Results.Conflict(new { code = "Lesson.AttendanceStarted" });
                var makeup = await db.MakeupCredits.AsNoTracking().SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId
                    && item.MakeupLessonId == lessonId, ct);
                if (makeup is not null && endsAt > makeup.ExpiresAt) return Results.Conflict(new { code = "MakeupCredit.Expired" });
                var studentIds = await db.LessonParticipants.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                    && item.LessonSessionId == lessonId && item.IsActive).Select(item => item.StudentId).ToListAsync(ct);
                var roomId = request.RoomId ?? lesson.RoomId;
                if (roomId is Guid chosenRoom)
                {
                    var room = await db.Rooms.AsNoTracking().SingleOrDefaultAsync(item => item.Id == chosenRoom && item.WorkspaceId == workspaceId, ct);
                    if (room is null) return Results.NotFound();
                    if (room.Capacity is int capacity && studentIds.Count > capacity) return Results.Conflict(new { code = "Room.CapacityExceeded" });
                    if (await db.LessonSessions.AnyAsync(item => item.Id != lessonId && item.WorkspaceId == workspaceId && item.RoomId == chosenRoom
                            && item.Status == LessonSessionStatus.Scheduled && item.StartsAt < endsAt && startsAt < item.EndsAt, ct))
                        return Results.Conflict(new { code = "Room.Conflict" });
                }
                if (await db.TeacherTimeOffs.AnyAsync(item => item.WorkspaceId == workspaceId && item.TeacherUserId == lesson.TeacherUserId
                        && item.StartsAt < endsAt && startsAt < item.EndsAt, ct))
                    return Results.Conflict(new { code = "Availability.TimeOffConflict" });
                var windows = await db.TeacherAvailabilityWindows.AsNoTracking()
                    .Where(item => item.WorkspaceId == workspaceId && item.TeacherUserId == lesson.TeacherUserId).ToListAsync(ct);
                if (!LearningEndpoints.FitsTeacherAvailability((startsAt, endsAt), windows))
                    return Results.Conflict(new { code = "Availability.OutsideWorkingHours" });
                if (await db.LessonSessions.AnyAsync(item => item.Id != lessonId && item.WorkspaceId == workspaceId
                        && item.Status == LessonSessionStatus.Scheduled && item.TeacherUserId == lesson.TeacherUserId
                        && item.StartsAt < endsAt && startsAt < item.EndsAt, ct))
                    return Results.Conflict(new { code = "Lesson.TeacherConflict" });
                if (await (from participant in db.LessonParticipants.AsNoTracking()
                        join other in db.LessonSessions.AsNoTracking() on participant.LessonSessionId equals other.Id
                        where participant.WorkspaceId == workspaceId && participant.IsActive && participant.LessonSessionId != lessonId
                            && studentIds.Contains(participant.StudentId) && other.Status == LessonSessionStatus.Scheduled
                            && other.StartsAt < endsAt && startsAt < other.EndsAt
                        select participant.Id).AnyAsync(ct))
                    return Results.Conflict(new { code = "Lesson.StudentConflict" });

                var before = CourseEmailContent.When(lesson.StartsAt);
                lesson.Reschedule(startsAt, endsAt, roomId);
                // Audit under its own name: the lesson-change e-mail is not sent for a support correction.
                AssignmentEndpoints.AddAudit(db, workspaceId, grant.AdminUserId, lesson.Id, "Support.LessonSession.Rescheduled");
                db.SupportAccessLogs.Add(SupportAccessLog.Change(grant, SupportAccessSections.Schedule, "Ders saati düzeltildi", lesson.Id,
                    before, CourseEmailContent.When(startsAt), now));
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.Ok(new { lesson.Id, lesson.StartsAt, lesson.EndsAt, lesson.RoomId });
            }
            catch (Exception exception) when (LearningEndpoints.IsSerializationFailure(exception))
            {
                return Results.Conflict(new { code = "Lesson.ConcurrentUpdate" });
            }
        });

        // Switching an e-mail kind off (e.g. it goes to the wrong people). Switching on needs the owner's consent.
        admin.MapPost("/settings/email/{kind}/off", async (Guid workspaceId, CourseEmailKind kind, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var grant = await SupportAccessEndpoints.ChangeGrantAsync(db, workspaceId, user, SupportAccessSections.Settings, ct);
            if (grant is null) return Results.NotFound();
            if (!Enum.IsDefined(kind)) return Results.BadRequest(new { code = "EmailSettings.UnknownKind" });
            var settings = await db.EmailSettings.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId, ct);
            var now = DateTimeOffset.UtcNow;
            if (settings is null || !settings.SwitchOff(kind, now)) return Results.Ok(new { kind = kind.ToString(), enabled = false });
            db.SupportAccessLogs.Add(SupportAccessLog.Change(grant, SupportAccessSections.Settings, "E-posta bildirimi kapatıldı",
                workspaceId, kind.ToString(), "Kapalı", now));
            AssignmentEndpoints.AddAudit(db, workspaceId, grant.AdminUserId, workspaceId, "Support.EmailSettings.SwitchedOff");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { kind = kind.ToString(), enabled = false });
        });
    }

    private static bool ValidContact(SupportContactRequest request) =>
        !string.IsNullOrWhiteSpace(request.DisplayName) && request.DisplayName.Trim().Length <= 200
        && (request.Email?.Trim().Length ?? 0) <= 320 && (request.PhoneNumber?.Trim().Length ?? 0) <= 40;

    private static string[] ChangedFields(string name, string? email, string? phone, SupportContactRequest request)
    {
        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var fields = new List<string>();
        if (!string.Equals(name, request.DisplayName?.Trim(), StringComparison.Ordinal)) fields.Add("ad");
        if (!string.Equals(Clean(email), Clean(request.Email)?.ToLowerInvariant(), StringComparison.Ordinal)) fields.Add("e-posta");
        if (!string.Equals(Clean(phone), Clean(request.PhoneNumber), StringComparison.Ordinal)) fields.Add("telefon");
        return fields.ToArray();
    }
}

public sealed record SupportContactRequest(string? DisplayName, string? Email, string? PhoneNumber);
