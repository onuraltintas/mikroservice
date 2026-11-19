using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

/// <summary>
/// The institution panel's student list (19.11.2026): search, filters, sorting and pages. A teacher sees only the
/// students in their own groups and no money; the owner also sees overdue debt.
/// </summary>
public static class StudentSearchEndpoints
{
    public static void MapStudentSearchEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/workspaces/{workspaceId:guid}/students/search", async (Guid workspaceId, string? q, Guid? courseId, Guid? groupId,
            Guid? teacherId, string? status, bool? overdue, string? sort, int? skip, int? take, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = AssignmentEndpoints.UserId(user);
            var owner = await db.Workspaces.AnyAsync(item => item.Id == workspaceId && item.OwnerUserId == userId, ct);
            var teacher = !owner && await db.Memberships.AnyAsync(item => item.WorkspaceId == workspaceId && item.UserId == userId
                && item.IsActive && item.Role == WorkspaceRole.Teacher, ct);
            if (!owner && !teacher) return Results.NotFound();
            if (skip is < 0 || take is < 1 or > 100 || status is not (null or "active" or "archived" or "all")
                || sort is not (null or "name" or "-name" or "created" or "-created"))
                return Results.BadRequest(new { code = "Student.InvalidSearch" });

            var students = db.Students.AsNoTracking().Where(item => item.WorkspaceId == workspaceId);
            students = (status ?? "active") switch
            {
                "active" => students.Where(item => item.IsActive),
                "archived" => students.Where(item => !item.IsActive),
                _ => students
            };
            if (teacher)
                students = students.Where(item => item.IsActive && db.Enrollments.Any(enrollment => enrollment.StudentId == item.Id
                    && enrollment.Status == EnrollmentStatus.Active
                    && db.Groups.Any(courseGroup => courseGroup.Id == enrollment.GroupId && courseGroup.TeacherUserId == userId)));
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim().Replace("%", "").Replace("_", "")}%";
                students = students.Where(item => EF.Functions.ILike(item.DisplayName, term)
                    || item.Email != null && EF.Functions.ILike(item.Email, term) || item.PhoneNumber != null && EF.Functions.ILike(item.PhoneNumber, term)
                    || db.StudentGuardianLinks.Any(link => link.StudentId == item.Id && link.IsActive
                        && db.Guardians.Any(guardian => guardian.Id == link.GuardianContactId && EF.Functions.ILike(guardian.DisplayName, term))));
            }
            if (groupId is Guid group)
                students = students.Where(item => db.Enrollments.Any(enrollment => enrollment.StudentId == item.Id
                    && enrollment.GroupId == group && enrollment.Status == EnrollmentStatus.Active));
            if (courseId is Guid course)
                students = students.Where(item => db.Enrollments.Any(enrollment => enrollment.StudentId == item.Id
                    && enrollment.Status == EnrollmentStatus.Active
                    && db.Groups.Any(courseGroup => courseGroup.Id == enrollment.GroupId && courseGroup.CourseId == course)));
            if (teacherId is Guid groupTeacher)
                students = students.Where(item => db.Enrollments.Any(enrollment => enrollment.StudentId == item.Id
                    && enrollment.Status == EnrollmentStatus.Active
                    && db.Groups.Any(courseGroup => courseGroup.Id == enrollment.GroupId && courseGroup.TeacherUserId == groupTeacher)));

            Dictionary<Guid, decimal> debts = [];
            if (owner)
            {
                var today = CourseEmailCollector.TurkeyDate(DateTimeOffset.UtcNow);
                debts = (await CourseEmailContent.OpenInstallmentsAsync(db, workspaceId, query => query.Where(item => item.DueOn < today), ct))
                    .GroupBy(item => item.StudentId).ToDictionary(group => group.Key, group => group.Sum(item => item.Remaining));
                if (overdue is bool wantsOverdue)
                {
                    var ids = debts.Keys.ToArray();
                    students = wantsOverdue ? students.Where(item => ids.Contains(item.Id)) : students.Where(item => !ids.Contains(item.Id));
                }
            }

            var total = await students.CountAsync(ct);
            students = sort switch
            {
                "-name" => students.OrderByDescending(item => item.DisplayName).ThenBy(item => item.Id),
                "created" => students.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id),
                "-created" => students.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id),
                _ => students.OrderBy(item => item.DisplayName).ThenBy(item => item.Id)
            };
            var rows = await students.Skip(skip ?? 0).Take(take ?? 25).Select(item => new
            {
                item.Id, item.DisplayName, item.Email, item.PhoneNumber, item.IsActive, item.EmailsMuted, item.CreatedAt,
                groups = (from enrollment in db.Enrollments
                    join courseGroup in db.Groups on enrollment.GroupId equals courseGroup.Id
                    join courseItem in db.Courses on courseGroup.CourseId equals courseItem.Id
                    where enrollment.StudentId == item.Id && enrollment.Status == EnrollmentStatus.Active
                    select new { courseGroup.Id, courseGroup.Name, courseName = courseItem.Name }).ToList(),
                guardianCount = db.StudentGuardianLinks.Count(link => link.StudentId == item.Id && link.IsActive),
                guardianWithoutEmail = db.StudentGuardianLinks.Any(link => link.StudentId == item.Id && link.IsActive
                    && db.Guardians.Any(guardian => guardian.Id == link.GuardianContactId && guardian.Email == null))
            }).ToListAsync(ct);
            return Results.Ok(new
            {
                total, skip = skip ?? 0, take = take ?? 25,
                items = rows.Select(row => new
                {
                    row.Id, row.DisplayName, email = teacher ? null : row.Email, phoneNumber = teacher ? null : row.PhoneNumber,
                    row.IsActive, row.EmailsMuted, row.CreatedAt, row.groups, row.guardianCount, row.guardianWithoutEmail,
                    overdueAmount = owner ? debts.GetValueOrDefault(row.Id) : (decimal?)null
                })
            });
        }).WithMetadata(new ReadOnlyWorkspaceOperationMetadata());
    }
}
