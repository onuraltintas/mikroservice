using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.API;

/// <summary>
/// Tahsilat lists for the institution panel (19.11.2026): debts and payments with search, filters and pages. Owner only.
/// </summary>
public static class FinanceListEndpoints
{
    public static void MapFinanceListEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/workspaces/{workspaceId:guid}/finance/installments", async (Guid workspaceId, string? status, Guid? courseId,
            string? q, int? skip, int? take, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, user, ct)) return Results.NotFound();
            if (skip is < 0 || take is < 1 or > 100 || status is not (null or "open" or "overdue" or "upcoming" or "paid"))
                return Results.BadRequest(new { code = "Finance.InvalidFilter" });
            var today = CourseEmailCollector.TurkeyDate(DateTimeOffset.UtcNow);
            var reversed = db.Installments.Where(item => item.WorkspaceId == workspaceId && item.ReversalOfInstallmentId != null)
                .Select(item => item.ReversalOfInstallmentId!.Value);
            var rows = from installment in db.Installments.AsNoTracking()
                join student in db.Students.AsNoTracking() on installment.StudentId equals student.Id
                join agreement in db.FeeAgreements.AsNoTracking() on installment.FeeAgreementId equals agreement.Id
                join course in db.Courses.AsNoTracking() on agreement.CourseId equals course.Id
                where installment.WorkspaceId == workspaceId && installment.Amount > 0 && installment.ReversalOfInstallmentId == null
                    && !reversed.Contains(installment.Id)
                select new
                {
                    installment.Id, installment.StudentId, StudentName = student.DisplayName, CourseId = course.Id, CourseName = course.Name,
                    installment.Label, installment.Amount, installment.DueOn, IsLessonCharge = installment.SourceLessonId != null,
                    Paid = db.PaymentAllocations.Where(item => item.InstallmentId == installment.Id).Sum(item => (decimal?)item.Amount) ?? 0m
                };
            if (courseId is Guid chosenCourse) rows = rows.Where(item => item.CourseId == chosenCourse);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim().Replace("%", "").Replace("_", "")}%";
                rows = rows.Where(item => EF.Functions.ILike(item.StudentName, term) || EF.Functions.ILike(item.Label, term));
            }
            rows = (status ?? "open") switch
            {
                "paid" => rows.Where(item => item.Paid >= item.Amount),
                "overdue" => rows.Where(item => item.Paid < item.Amount && item.DueOn < today),
                "upcoming" => rows.Where(item => item.Paid < item.Amount && item.DueOn >= today),
                _ => rows.Where(item => item.Paid < item.Amount)
            };
            var total = await rows.CountAsync(ct);
            var outstanding = await rows.SumAsync(item => (decimal?)(item.Amount - item.Paid), ct) ?? 0m;
            var page = await (status == "paid" ? rows.OrderByDescending(item => item.DueOn) : rows.OrderBy(item => item.DueOn))
                .ThenBy(item => item.StudentName).ThenBy(item => item.Id)
                .Skip(skip ?? 0).Take(take ?? 25).ToListAsync(ct);
            return Results.Ok(new
            {
                total, outstanding, skip = skip ?? 0, take = take ?? 25,
                items = page.Select(item => new
                {
                    item.Id, item.StudentId, item.StudentName, item.CourseId, item.CourseName, item.Label, item.Amount,
                    remaining = Math.Max(0m, item.Amount - item.Paid), item.DueOn, item.IsLessonCharge,
                    overdueDays = item.Paid < item.Amount && item.DueOn < today ? today.DayNumber - item.DueOn.DayNumber : 0
                })
            });
        }).WithMetadata(new ReadOnlyWorkspaceOperationMetadata());

        api.MapGet("/workspaces/{workspaceId:guid}/finance/payments", async (Guid workspaceId, DateOnly? from, DateOnly? to, int? method,
            string? q, int? skip, int? take, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, user, ct)) return Results.NotFound();
            if (skip is < 0 || take is < 1 or > 100 || method is not (null or 1 or 2)) return Results.BadRequest(new { code = "Finance.InvalidFilter" });
            var rows = from payment in db.Payments.AsNoTracking()
                join student in db.Students.AsNoTracking() on payment.StudentId equals student.Id
                where payment.WorkspaceId == workspaceId
                select new { payment.Id, payment.StudentId, StudentName = student.DisplayName, payment.Amount, payment.ReceivedAt,
                    payment.Method, payment.Note, IsReversal = payment.ReversalOfPaymentId != null,
                    Reversed = db.Payments.Any(item => item.ReversalOfPaymentId == payment.Id) };
            if (from is DateOnly start) { var at = Start(start); rows = rows.Where(item => item.ReceivedAt >= at); }
            if (to is DateOnly end) { var at = Start(end.AddDays(1)); rows = rows.Where(item => item.ReceivedAt < at); }
            if (method is int chosen) rows = rows.Where(item => (int)item.Method == chosen);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim().Replace("%", "").Replace("_", "")}%";
                rows = rows.Where(item => EF.Functions.ILike(item.StudentName, term));
            }
            var total = await rows.CountAsync(ct);
            var sum = await rows.SumAsync(item => (decimal?)item.Amount, ct) ?? 0m;
            var page = await rows.OrderByDescending(item => item.ReceivedAt).ThenBy(item => item.Id).Skip(skip ?? 0).Take(take ?? 25).ToListAsync(ct);
            return Results.Ok(new
            {
                total, sum, skip = skip ?? 0, take = take ?? 25,
                items = page.Select(item => new { item.Id, item.StudentId, item.StudentName, item.Amount, item.ReceivedAt,
                    method = item.Method.ToString(), item.Note, item.IsReversal, item.Reversed })
            });
        }).WithMetadata(new ReadOnlyWorkspaceOperationMetadata());
    }

    private static DateTimeOffset Start(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, CourseManagementTime.Turkey.GetUtcOffset(local)).ToUniversalTime();
    }

    private static Task<bool> IsOwner(CourseManagementDbContext db, Guid workspaceId, ClaimsPrincipal user, CancellationToken ct)
    {
        var userId = AssignmentEndpoints.UserId(user);
        return db.Workspaces.AnyAsync(item => item.Id == workspaceId && item.OwnerUserId == userId, ct);
    }
}
