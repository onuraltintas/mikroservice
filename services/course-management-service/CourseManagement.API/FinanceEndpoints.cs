using System.Data;
using System.Security.Claims;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CourseManagement.API;

public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/workspaces/{workspaceId:guid}/finance/fee-agreements", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            return Results.Ok(await db.FeeAgreements.AsNoTracking().Where(item => item.WorkspaceId == workspaceId)
                .OrderByDescending(item => item.CreatedAt).Select(item => new FeeAgreementView(item.Id, item.StudentId, item.CourseId,
                    item.Model.ToString(), item.Amount, item.BillingUnit == null ? null : item.BillingUnit.ToString(),
                    item.PackageSessions, item.PackageExpiresAt, item.IsActive, item.CreatedAt, item.ClosedAt)).ToListAsync(ct));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/finance/fee-agreements", async (Guid workspaceId,
            CreateFeeAgreementRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            if (request.Id == Guid.Empty || !Enum.IsDefined(request.Model) || !IsValidMoney(request.Amount)
                || decimal.Round(request.Amount, 2) != request.Amount
                || request.Model == FeeModel.PerLesson && request.BillingUnit is not (LessonBillingUnit.Lesson or LessonBillingUnit.Hour)
                || request.Model != FeeModel.PerLesson && request.BillingUnit is not null
                || request.Model == FeeModel.LessonPackage && request.PackageSessions is not > 0
                || request.Model != FeeModel.LessonPackage && (request.PackageSessions is not null || request.PackageExpiresAt is not null))
                return Results.BadRequest(new { code = "FeeAgreement.InvalidInput" });
            var prior = await db.FeeAgreements.SingleOrDefaultAsync(item => item.Id == request.Id, ct);
            if (prior is not null)
                return Matches(prior, workspaceId, request)
                    ? Results.Ok(ToView(prior))
                    : Results.Conflict(new { code = "FeeAgreement.IdempotencyConflict" });
            if (!await db.Students.AnyAsync(item => item.Id == request.StudentId && item.WorkspaceId == workspaceId && item.IsActive, ct)
                || !await db.Courses.AnyAsync(item => item.Id == request.CourseId && item.WorkspaceId == workspaceId, ct))
                return Results.NotFound();

            var now = DateTimeOffset.UtcNow;
            FeeAgreement agreement;
            try
            {
                agreement = FeeAgreement.Create(request.Id, workspaceId, request.StudentId, request.CourseId, UserId(user),
                    request.Model, request.Amount, request.BillingUnit, request.PackageSessions, request.PackageExpiresAt, now);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { code = "FeeAgreement.InvalidInput" });
            }
            db.FeeAgreements.Add(agreement);
            if (agreement.Model == FeeModel.LessonPackage)
                db.PackageMovements.Add(PackageMovement.Grant(Guid.NewGuid(), agreement, now));
            AddAudit(db, workspaceId, user, agreement.Id, "Finance.AgreementCreated");
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                return Results.Conflict(new { code = "FeeAgreement.ActiveAgreementExists" });
            }
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/finance/fee-agreements/{agreement.Id}", ToView(agreement));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/finance/fee-agreements/{agreementId:guid}/installments",
            async (Guid workspaceId, Guid agreementId, CreateInstallmentRequest request, ClaimsPrincipal user,
                CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            if (request.Id == Guid.Empty || string.IsNullOrWhiteSpace(request.Label) || request.Label.Trim().Length > 120
                || !IsValidMoney(request.Amount) || request.DueOn == default)
                return Results.BadRequest(new { code = "Installment.InvalidInput" });
            var agreement = await db.FeeAgreements.SingleOrDefaultAsync(item => item.Id == agreementId
                && item.WorkspaceId == workspaceId, ct);
            if (agreement is null) return Results.NotFound();
            var prior = await db.Installments.SingleOrDefaultAsync(item => item.Id == request.Id, ct);
            if (prior is not null)
                return prior.FeeAgreementId == agreementId && prior.Amount == request.Amount && prior.DueOn == request.DueOn
                    && prior.Label == request.Label.Trim()
                    ? Results.Ok(ToView(prior, prior.Amount))
                    : Results.Conflict(new { code = "Installment.IdempotencyConflict" });
            if (!agreement.IsActive || agreement.Model == FeeModel.PerLesson)
                return Results.Conflict(new { code = "Installment.AgreementNotEditable" });
            if (agreement.Model == FeeModel.MonthlyFixed && request.Amount != agreement.Amount)
                return Results.BadRequest(new { code = "Installment.MonthlyAmountMustMatchAgreement" });
            var plannedAmount = await db.Installments.Where(item => item.FeeAgreementId == agreementId
                && item.ReversalOfInstallmentId == null).SumAsync(item => (decimal?)item.Amount, ct) ?? 0m;
            if (agreement.Model != FeeModel.MonthlyFixed && plannedAmount + request.Amount > agreement.Amount)
                return Results.Conflict(new { code = "Installment.ExceedsAgreementAmount" });
            var installment = Installment.Create(request.Id, workspaceId, agreementId, agreement.StudentId,
                request.Label, request.Amount, request.DueOn, DateTimeOffset.UtcNow);
            db.Installments.Add(installment);
            var studentUserId = await db.Students.AsNoTracking().Where(student => student.Id == agreement.StudentId
                && student.WorkspaceId == workspaceId).Select(student => student.UserId).SingleOrDefaultAsync(ct);
            await CourseManagementReminderScheduling.ScheduleInstallmentAsync(db, installment, studentUserId,
                DateTimeOffset.UtcNow, ct);
            AddAudit(db, workspaceId, user, installment.Id, "Finance.InstallmentCreated");
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/finance/installments/{installment.Id}", ToView(installment, installment.Amount));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/finance/fee-agreements/{agreementId:guid}/close",
            async (Guid workspaceId, Guid agreementId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            var agreement = await db.FeeAgreements.SingleOrDefaultAsync(item => item.Id == agreementId && item.WorkspaceId == workspaceId, ct);
            if (agreement is null) return Results.NotFound();
            if (agreement.IsActive)
            {
                var plannedAmount = await db.Installments.Where(item => item.FeeAgreementId == agreementId
                    && item.ReversalOfInstallmentId == null).SumAsync(item => (decimal?)item.Amount, ct) ?? 0m;
                if (agreement.Model is FeeModel.Term or FeeModel.LessonPackage && plannedAmount != agreement.Amount)
                    return Results.Conflict(new { code = "FeeAgreement.InstallmentsDoNotMatchAmount" });
                agreement.Close(DateTimeOffset.UtcNow);
                AddAudit(db, workspaceId, user, agreement.Id, "Finance.AgreementClosed");
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(ToView(agreement));
        });

        api.MapGet("/workspaces/{workspaceId:guid}/finance/students/{studentId:guid}/account",
            async (Guid workspaceId, Guid studentId, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            if (!await db.Students.AnyAsync(item => item.Id == studentId && item.WorkspaceId == workspaceId, ct)) return Results.NotFound();
            var agreementRows = await db.FeeAgreements.AsNoTracking().Where(item => item.WorkspaceId == workspaceId && item.StudentId == studentId)
                .OrderBy(item => item.CreatedAt).ToListAsync(ct);
            var agreements = agreementRows.Select(ToView).ToArray();
            var installments = await db.Installments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId && item.StudentId == studentId)
                .OrderBy(item => item.DueOn).ThenBy(item => item.CreatedAt).ToListAsync(ct);
            var payments = await db.Payments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId && item.StudentId == studentId)
                .OrderBy(item => item.ReceivedAt).ToListAsync(ct);
            var allocations = await (from allocation in db.PaymentAllocations.AsNoTracking()
                join payment in db.Payments.AsNoTracking() on allocation.PaymentId equals payment.Id
                where allocation.WorkspaceId == workspaceId && payment.StudentId == studentId
                select allocation).ToListAsync(ct);
            var reversedInstallmentIds = installments.Where(item => item.ReversalOfInstallmentId is not null)
                .Select(item => item.ReversalOfInstallmentId!.Value).ToHashSet();
            var installmentViews = installments.Select(item => ToView(item, item.ReversalOfInstallmentId is not null
                || reversedInstallmentIds.Contains(item.Id) ? 0m
                : item.Amount - allocations.Where(allocation => allocation.InstallmentId == item.Id).Sum(allocation => allocation.Amount))).ToArray();
            var packageAgreements = await db.FeeAgreements.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                && item.StudentId == studentId && item.Model == FeeModel.LessonPackage).ToListAsync(ct);
            var packageMovements = await db.PackageMovements.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                && item.StudentId == studentId).OrderBy(item => item.OccurredAt).ToListAsync(ct);
            var now = DateTimeOffset.UtcNow;
            var packageBalances = packageAgreements.Select(agreement => new PackageBalanceView(agreement.Id, agreement.PackageSessions ?? 0,
                agreement.PackageExpiresAt, agreement.PackageExpiresAt is { } expires && expires <= now ? 0
                    : Math.Max(0, packageMovements.Where(item => item.FeeAgreementId == agreement.Id).Sum(item => item.Quantity)))).ToArray();
            var chargeTotal = installments.Sum(item => item.Amount);
            var paymentTotal = payments.Sum(item => item.Amount);
            return Results.Ok(new StudentAccountView(studentId, chargeTotal - paymentTotal, chargeTotal, paymentTotal,
                paymentTotal - allocations.Sum(item => item.Amount), agreements, installmentViews,
                payments.Select(item => ToView(item, allocations.Where(allocation => allocation.PaymentId == item.Id).Sum(allocation => allocation.Amount))),
                packageBalances, packageMovements.Select(ToView).ToArray()));
        });

        api.MapGet("/me/finance", async (ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(user);
            var accounts = await (from student in db.Students.AsNoTracking()
                join membership in db.Memberships.AsNoTracking() on student.WorkspaceId equals membership.WorkspaceId
                join workspace in db.Workspaces.AsNoTracking() on student.WorkspaceId equals workspace.Id
                where student.UserId == userId && student.IsActive && membership.UserId == userId
                    && membership.IsActive && membership.Role == WorkspaceRole.Student
                orderby workspace.Name
                select new { student.Id, student.WorkspaceId, workspace.Name }).Take(100).ToListAsync(ct);
            if (accounts.Count == 0) return Results.Ok(Array.Empty<StudentFinanceSummaryView>());

            var studentIds = accounts.Select(item => item.Id).ToArray();
            var charges = await db.Installments.AsNoTracking().Where(item => Enumerable.Contains(studentIds, item.StudentId))
                .GroupBy(item => item.StudentId).Select(group => new { StudentId = group.Key, Amount = group.Sum(item => item.Amount) })
                .ToDictionaryAsync(item => item.StudentId, item => item.Amount, ct);
            var payments = await db.Payments.AsNoTracking().Where(item => Enumerable.Contains(studentIds, item.StudentId))
                .GroupBy(item => item.StudentId).Select(group => new { StudentId = group.Key, Amount = group.Sum(item => item.Amount) })
                .ToDictionaryAsync(item => item.StudentId, item => item.Amount, ct);
            var agreements = await db.FeeAgreements.AsNoTracking().Where(item => Enumerable.Contains(studentIds, item.StudentId)
                && item.Model == FeeModel.LessonPackage && item.IsActive).ToListAsync(ct);
            var agreementIds = agreements.Select(item => item.Id).ToArray();
            var packageMovements = agreementIds.Length == 0 ? new Dictionary<Guid, int>()
                : await db.PackageMovements.AsNoTracking().Where(item => Enumerable.Contains(agreementIds, item.FeeAgreementId))
                    .GroupBy(item => item.FeeAgreementId)
                    .Select(group => new { AgreementId = group.Key, Quantity = group.Sum(item => item.Quantity) })
                    .ToDictionaryAsync(item => item.AgreementId, item => item.Quantity, ct);
            var now = DateTimeOffset.UtcNow;
            return Results.Ok(accounts.Select(account => new StudentFinanceSummaryView(account.WorkspaceId, account.Name,
                charges.GetValueOrDefault(account.Id) - payments.GetValueOrDefault(account.Id),
                agreements.Where(agreement => agreement.StudentId == account.Id).Select(agreement =>
                {
                    var remaining = packageMovements.GetValueOrDefault(agreement.Id);
                    if (agreement.PackageExpiresAt is { } expiresAt && expiresAt <= now) remaining = 0;
                    return new StudentPackageEntitlementView(agreement.Id, agreement.PackageSessions ?? 0,
                        Math.Max(0, remaining), agreement.PackageExpiresAt);
                }).ToArray())));
        });

        api.MapGet("/workspaces/{workspaceId:guid}/finance/summary", async (Guid workspaceId, DateOnly? from, DateOnly? to,
            ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var start = from ?? new DateOnly(today.Year, today.Month, 1);
            var end = to ?? today;
            if (end < start) return Results.BadRequest(new { code = "Finance.InvalidDateRange" });

            var charges = await db.Installments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId)
                .SumAsync(item => (decimal?)item.Amount, ct) ?? 0m;
            var totalPayments = await db.Payments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId)
                .SumAsync(item => (decimal?)item.Amount, ct) ?? 0m;
            var paymentStart = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            var paymentEnd = new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            var periodCollections = await db.Payments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                && item.ReceivedAt >= paymentStart && item.ReceivedAt < paymentEnd).GroupBy(item => item.Method)
                .Select(group => new { Method = group.Key, Amount = group.Sum(item => item.Amount) }).ToListAsync(ct);
            var collections = periodCollections.Select(item => new CollectionByMethodView(item.Method.ToString(), item.Amount))
                .OrderBy(item => item.Method).ToArray();

            var openCharges = await db.Installments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                && item.Amount > 0 && item.ReversalOfInstallmentId == null).ToListAsync(ct);
            var reversedChargeIds = await db.Installments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                && item.ReversalOfInstallmentId != null).Select(item => item.ReversalOfInstallmentId!.Value).ToListAsync(ct);
            var openChargeIds = openCharges.Select(item => item.Id).ToArray();
            var allocations = await db.PaymentAllocations.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                && Enumerable.Contains(openChargeIds, item.InstallmentId)).ToListAsync(ct);
            decimal Remaining(Installment item) => Math.Max(0m,
                item.Amount - allocations.Where(allocation => allocation.InstallmentId == item.Id).Sum(allocation => allocation.Amount));
            var dueAmounts = openCharges.Where(item => !reversedChargeIds.Contains(item.Id)).Select(item => (item.DueOn, Amount: Remaining(item)));
            var overdue = dueAmounts.Where(item => item.DueOn < today).Sum(item => item.Amount);
            var upcoming = dueAmounts.Where(item => item.DueOn >= today && item.DueOn <= today.AddDays(30)).Sum(item => item.Amount);

            return Results.Ok(new FinanceSummaryView(start, end, charges, totalPayments, charges - totalPayments,
                overdue, upcoming, collections));
        });

        api.MapGet("/workspaces/{workspaceId:guid}/finance/report", async (Guid workspaceId, ClaimsPrincipal user,
            CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var now = DateTimeOffset.UtcNow;
            var chargeRows = await (from installment in db.Installments.AsNoTracking()
                join student in db.Students.AsNoTracking() on installment.StudentId equals student.Id
                join agreement in db.FeeAgreements.AsNoTracking() on installment.FeeAgreementId equals agreement.Id
                join course in db.Courses.AsNoTracking() on agreement.CourseId equals course.Id
                where installment.WorkspaceId == workspaceId && installment.Amount > 0 && installment.ReversalOfInstallmentId == null
                    && student.WorkspaceId == workspaceId && agreement.WorkspaceId == workspaceId && course.WorkspaceId == workspaceId
                orderby installment.DueOn, installment.Id
                select new
                {
                    installment.Id, installment.FeeAgreementId, installment.StudentId, StudentName = student.DisplayName,
                    CourseId = course.Id, CourseName = course.Name, installment.Label, installment.Amount, installment.DueOn
                }).ToListAsync(ct);
            var chargeIds = chargeRows.Select(item => item.Id).ToArray();
            var reversedChargeIds = await db.Installments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                    && item.ReversalOfInstallmentId != null)
                .Select(item => item.ReversalOfInstallmentId!.Value).ToListAsync(ct);
            var allocations = chargeIds.Length == 0 ? new Dictionary<Guid, decimal>()
                : await db.PaymentAllocations.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                        && Enumerable.Contains(chargeIds, item.InstallmentId))
                    .GroupBy(item => item.InstallmentId)
                    .Select(group => new { InstallmentId = group.Key, Amount = group.Sum(item => item.Amount) })
                    .ToDictionaryAsync(item => item.InstallmentId, item => item.Amount, ct);
            var receivables = chargeRows.Where(item => !reversedChargeIds.Contains(item.Id)).Select(item =>
                new FinanceInstallmentReportView(item.Id, item.FeeAgreementId, item.StudentId, item.StudentName,
                    item.CourseId, item.CourseName, item.Label, item.Amount,
                    Math.Max(0m, item.Amount - allocations.GetValueOrDefault(item.Id)), item.DueOn))
                .Where(item => item.RemainingAmount > 0m).ToArray();
            var overdueInstallments = receivables.Where(item => item.DueOn < today).ToArray();
            var upcomingInstallments = receivables.Where(item => item.DueOn >= today && item.DueOn <= today.AddDays(30)).ToArray();

            var packageRows = await (from agreement in db.FeeAgreements.AsNoTracking()
                join student in db.Students.AsNoTracking() on agreement.StudentId equals student.Id
                join course in db.Courses.AsNoTracking() on agreement.CourseId equals course.Id
                where agreement.WorkspaceId == workspaceId && agreement.Model == FeeModel.LessonPackage && agreement.IsActive
                    && student.WorkspaceId == workspaceId && course.WorkspaceId == workspaceId
                orderby student.DisplayName, agreement.Id
                select new
                {
                    agreement.Id, agreement.StudentId, StudentName = student.DisplayName, CourseId = course.Id, CourseName = course.Name,
                    agreement.Amount, GrantedSessions = agreement.PackageSessions ?? 0, agreement.PackageExpiresAt
                }).ToListAsync(ct);
            var packageIds = packageRows.Select(item => item.Id).ToArray();
            var packageMovements = packageIds.Length == 0 ? new Dictionary<Guid, int>()
                : await db.PackageMovements.AsNoTracking().Where(item => item.WorkspaceId == workspaceId
                        && Enumerable.Contains(packageIds, item.FeeAgreementId))
                    .GroupBy(item => item.FeeAgreementId)
                    .Select(group => new { AgreementId = group.Key, Quantity = group.Sum(item => item.Quantity) })
                    .ToDictionaryAsync(item => item.AgreementId, item => item.Quantity, ct);
            var packageBalances = packageRows.Select(item => new FinancePackageReportView(item.Id, item.StudentId,
                    item.StudentName, item.CourseId, item.CourseName, item.Amount, item.GrantedSessions,
                    item.PackageExpiresAt, item.PackageExpiresAt is { } expires && expires <= now ? 0
                        : Math.Max(0, packageMovements.GetValueOrDefault(item.Id))))
                .Where(item => item.RemainingSessions > 0)
                .OrderBy(item => item.ExpiresAt ?? DateTimeOffset.MaxValue).ThenBy(item => item.StudentName).ThenBy(item => item.AgreementId)
                .ToArray();

            return Results.Ok(new FinanceWorkspaceReportView(today, today.AddDays(30), overdueInstallments,
                upcomingInstallments, packageBalances));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/finance/payments", async (Guid workspaceId, CreatePaymentRequest request,
            ClaimsPrincipal user, CourseManagementDbContext db, IPublishEndpoint publishEndpoint, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (!await IsOwner(db, workspaceId, userId, ct)) return Results.NotFound();
            var allocationRequests = request.Allocations ?? [];
            if (request.Id == Guid.Empty || request.StudentId == Guid.Empty || !Enum.IsDefined(request.Method)
                || !IsValidMoney(request.Amount) || request.ReceivedAt == default
                || allocationRequests.Any(item => item.InstallmentId == Guid.Empty || item.Amount <= 0 || decimal.Round(item.Amount, 2) != item.Amount)
                || allocationRequests.Select(item => item.InstallmentId).Distinct().Count() != allocationRequests.Length
                || allocationRequests.Sum(item => item.Amount) > request.Amount)
                return Results.BadRequest(new { code = "Payment.InvalidInput" });
            var existing = await db.Payments.SingleOrDefaultAsync(item => item.Id == request.Id, ct);
            if (existing is not null)
            {
                var existingAllocations = await db.PaymentAllocations.Where(item => item.PaymentId == existing.Id).ToListAsync(ct);
                var same = existing.WorkspaceId == workspaceId && existing.StudentId == request.StudentId
                    && existing.Method == request.Method && existing.Amount == request.Amount
                    && existing.ReceivedAt == request.ReceivedAt.ToUniversalTime() && existing.Note == Normalize(request.Note)
                    && existingAllocations.OrderBy(item => item.InstallmentId).Select(item => (item.InstallmentId, item.Amount))
                        .SequenceEqual(allocationRequests.OrderBy(item => item.InstallmentId).Select(item => (item.InstallmentId, item.Amount)));
                return same ? Results.Ok(ToView(existing, existingAllocations.Sum(item => item.Amount)))
                    : Results.Conflict(new { code = "Payment.IdempotencyConflict" });
            }
            if (!await db.Students.AnyAsync(item => item.Id == request.StudentId && item.WorkspaceId == workspaceId && item.IsActive, ct))
                return Results.NotFound();

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var installmentIds = allocationRequests.Select(item => item.InstallmentId).ToArray();
            var targets = await db.Installments.Where(item => item.WorkspaceId == workspaceId && item.StudentId == request.StudentId
                && Enumerable.Contains(installmentIds, item.Id) && item.Amount > 0 && item.ReversalOfInstallmentId == null).ToListAsync(ct);
            if (targets.Count != installmentIds.Length) return Results.BadRequest(new { code = "Payment.InvalidAllocation" });
            var priorAllocations = await db.PaymentAllocations.Where(item => Enumerable.Contains(installmentIds, item.InstallmentId)).ToListAsync(ct);
            var alreadyReversed = await db.Installments.Where(item => item.WorkspaceId == workspaceId && item.ReversalOfInstallmentId != null)
                .Select(item => item.ReversalOfInstallmentId!.Value).ToListAsync(ct);
            foreach (var allocation in allocationRequests)
            {
                var target = targets.Single(item => item.Id == allocation.InstallmentId);
                if (alreadyReversed.Contains(target.Id)) return Results.Conflict(new { code = "Payment.InstallmentReversed" });
                var remaining = target.Amount - priorAllocations.Where(item => item.InstallmentId == target.Id).Sum(item => item.Amount);
                if (allocation.Amount > remaining) return Results.Conflict(new { code = "Payment.AllocationExceedsBalance" });
            }
            var now = DateTimeOffset.UtcNow;
            var payment = Payment.Create(request.Id, workspaceId, request.StudentId, userId, request.Method,
                request.Amount, request.ReceivedAt, request.Note);
            var studentUserId = await db.Students.AsNoTracking().Where(item => item.Id == request.StudentId
                    && item.WorkspaceId == workspaceId && item.IsActive).Select(item => item.UserId).SingleOrDefaultAsync(ct);
            if (studentUserId.HasValue)
                await publishEndpoint.Publish(new CourseManagementPaymentRecordedV1(payment.Id, workspaceId,
                    studentUserId.Value), ct);
            db.Payments.Add(payment);
            foreach (var allocation in allocationRequests)
                db.PaymentAllocations.Add(PaymentAllocation.Create(Guid.NewGuid(), workspaceId, payment.Id,
                    allocation.InstallmentId, allocation.Amount, now));
            foreach (var allocation in allocationRequests)
            {
                var installment = targets.Single(item => item.Id == allocation.InstallmentId);
                var allocatedBefore = priorAllocations.Where(item => item.InstallmentId == installment.Id).Sum(item => item.Amount);
                if (allocatedBefore + allocation.Amount >= installment.Amount)
                    await CourseManagementReminderScheduling.CancelEntityAsync(db, workspaceId, installment.Id, ct);
            }
            AddAudit(db, workspaceId, user, payment.Id, "Finance.PaymentRecorded");
            try
            {
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                return Results.Conflict(new { code = "Payment.IdempotencyConflict" });
            }
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/finance/payments/{payment.Id}",
                ToView(payment, allocationRequests.Sum(item => item.Amount)));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/finance/payments/{paymentId:guid}/reverse", async (Guid workspaceId,
            Guid paymentId, ReversePaymentRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            if (request.Id == Guid.Empty) return Results.BadRequest(new { code = "Payment.InvalidReversal" });
            var payment = await db.Payments.SingleOrDefaultAsync(item => item.Id == paymentId && item.WorkspaceId == workspaceId, ct);
            if (payment is null) return Results.NotFound();
            var existing = await db.Payments.SingleOrDefaultAsync(item => item.ReversalOfPaymentId == paymentId, ct);
            if (existing is not null)
                return existing.Id == request.Id && existing.Note == Normalize(request.Reason)
                    && (request.ReceivedAt == default || existing.ReceivedAt == request.ReceivedAt.ToUniversalTime())
                    ? Results.Ok(ToView(existing, -payment.Amount))
                    : Results.Conflict(new { code = "Payment.AlreadyReversed" });
            var now = request.ReceivedAt == default ? DateTimeOffset.UtcNow : request.ReceivedAt.ToUniversalTime();
            var reversal = Payment.CreateReversal(request.Id, payment, UserId(user), now, request.Reason);
            var originalAllocations = await db.PaymentAllocations.Where(item => item.PaymentId == payment.Id).ToListAsync(ct);
            db.Payments.Add(reversal);
            foreach (var allocation in originalAllocations)
                db.PaymentAllocations.Add(PaymentAllocation.CreateReversal(Guid.NewGuid(), allocation, reversal.Id, now));
            var affectedInstallmentIds = originalAllocations.Select(item => item.InstallmentId).Distinct().ToArray();
            if (affectedInstallmentIds.Length > 0)
            {
                var installments = await db.Installments.Where(item => item.WorkspaceId == workspaceId
                    && Enumerable.Contains(affectedInstallmentIds, item.Id) && item.ReversalOfInstallmentId == null).ToListAsync(ct);
                var allAllocations = await db.PaymentAllocations.Where(item => item.WorkspaceId == workspaceId
                    && Enumerable.Contains(affectedInstallmentIds, item.InstallmentId)).ToListAsync(ct);
                var studentUserId = await db.Students.AsNoTracking().Where(student => student.WorkspaceId == workspaceId
                    && student.Id == payment.StudentId).Select(student => student.UserId).SingleOrDefaultAsync(ct);
                foreach (var installment in installments)
                {
                    var reversalAmount = originalAllocations.Where(item => item.InstallmentId == installment.Id).Sum(item => item.Amount);
                    var paidAfterReversal = allAllocations.Where(item => item.InstallmentId == installment.Id).Sum(item => item.Amount) - reversalAmount;
                    if (installment.Amount - paidAfterReversal > 0)
                        await CourseManagementReminderScheduling.ScheduleInstallmentAsync(db, installment, studentUserId, now, ct);
                }
            }
            AddAudit(db, workspaceId, user, reversal.Id, "Finance.PaymentReversed");
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                return Results.Conflict(new { code = "Payment.AlreadyReversed" });
            }
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/finance/payments/{reversal.Id}",
                ToView(reversal, -originalAllocations.Sum(item => item.Amount)));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/finance/installments/{installmentId:guid}/reverse", async (Guid workspaceId,
            Guid installmentId, ReverseInstallmentRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            if (request.Id == Guid.Empty) return Results.BadRequest(new { code = "Installment.InvalidReversal" });
            var installment = await db.Installments.SingleOrDefaultAsync(item => item.Id == installmentId
                && item.WorkspaceId == workspaceId && item.ReversalOfInstallmentId == null, ct);
            if (installment is null) return Results.NotFound();
            var existing = await db.Installments.SingleOrDefaultAsync(item => item.ReversalOfInstallmentId == installmentId, ct);
            if (existing is not null)
                return existing.Id == request.Id ? Results.Ok(ToView(existing, 0))
                    : Results.Conflict(new { code = "Installment.AlreadyReversed" });
            var reversal = Installment.CreateReversal(request.Id, installment, DateTimeOffset.UtcNow);
            db.Installments.Add(reversal);
            await CourseManagementReminderScheduling.CancelEntityAsync(db, workspaceId, installment.Id, ct);
            AddAudit(db, workspaceId, user, reversal.Id, "Finance.InstallmentReversed");
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            { return Results.Conflict(new { code = "Installment.AlreadyReversed" }); }
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/finance/installments/{reversal.Id}", ToView(reversal, 0));
        });

        api.MapPost("/workspaces/{workspaceId:guid}/finance/package-movements/{movementId:guid}/reverse", async (Guid workspaceId,
            Guid movementId, ReversePackageMovementRequest request, ClaimsPrincipal user, CourseManagementDbContext db, CancellationToken ct) =>
        {
            if (!await IsOwner(db, workspaceId, UserId(user), ct)) return Results.NotFound();
            if (request.Id == Guid.Empty) return Results.BadRequest(new { code = "PackageMovement.InvalidReversal" });
            var movement = await db.PackageMovements.SingleOrDefaultAsync(item => item.Id == movementId
                && item.WorkspaceId == workspaceId && item.Type == PackageMovementType.LessonConsumed, ct);
            if (movement is null) return Results.NotFound();
            var existing = await db.PackageMovements.SingleOrDefaultAsync(item => item.ReversalOfMovementId == movementId, ct);
            if (existing is not null)
                return existing.Id == request.Id ? Results.Ok(ToView(existing))
                    : Results.Conflict(new { code = "PackageMovement.AlreadyReversed" });
            var reversal = PackageMovement.Restore(request.Id, movement, DateTimeOffset.UtcNow);
            db.PackageMovements.Add(reversal);
            AddAudit(db, workspaceId, user, reversal.Id, "Finance.PackageMovementReversed");
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            { return Results.Conflict(new { code = "PackageMovement.AlreadyReversed" }); }
            return Results.Created($"/api/course-management/v1/workspaces/{workspaceId}/finance/package-movements/{reversal.Id}", ToView(reversal));
        });
    }

    private static async Task<bool> IsOwner(CourseManagementDbContext db, Guid workspaceId, Guid userId, CancellationToken ct) =>
        await db.Workspaces.AnyAsync(item => item.Id == workspaceId && item.OwnerUserId == userId, ct);

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);

    private static void AddAudit(CourseManagementDbContext db, Guid workspaceId, ClaimsPrincipal user, Guid targetId, string action) =>
        db.AuditEntries.Add(WorkspaceAuditEntry.Create(workspaceId, UserId(user), targetId, action));

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static bool Matches(FeeAgreement agreement, Guid workspaceId, CreateFeeAgreementRequest request) =>
        agreement.WorkspaceId == workspaceId && agreement.StudentId == request.StudentId && agreement.CourseId == request.CourseId
        && agreement.Model == request.Model && agreement.Amount == request.Amount && agreement.BillingUnit == request.BillingUnit
        && agreement.PackageSessions == request.PackageSessions
        && agreement.PackageExpiresAt == request.PackageExpiresAt?.ToUniversalTime();

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidMoney(decimal value) => value is > 0 and <= 9_999_999_999.99m
        && decimal.Round(value, 2) == value;

    private static FeeAgreementView ToView(FeeAgreement item) => new(item.Id, item.StudentId, item.CourseId, item.Model.ToString(),
        item.Amount, item.BillingUnit?.ToString(), item.PackageSessions, item.PackageExpiresAt, item.IsActive, item.CreatedAt, item.ClosedAt);

    private static InstallmentView ToView(Installment item, decimal remainingAmount) => new(item.Id, item.FeeAgreementId, item.StudentId,
        item.SourceLessonId, item.ReversalOfInstallmentId, item.Label, item.Amount, remainingAmount, item.DueOn, item.CreatedAt);

    private static PaymentView ToView(Payment item, decimal allocatedAmount) => new(item.Id, item.StudentId, item.ReversalOfPaymentId,
        item.Method.ToString(), item.Amount, item.ReceivedAt, item.Note, allocatedAmount);

    private static PackageMovementView ToView(PackageMovement item) => new(item.Id, item.FeeAgreementId, item.LessonSessionId,
        item.ReversalOfMovementId, item.Type.ToString(), item.Quantity, item.OccurredAt);
}

public sealed record CreateFeeAgreementRequest(Guid Id, Guid StudentId, Guid CourseId, FeeModel Model, decimal Amount,
    LessonBillingUnit? BillingUnit = null, int? PackageSessions = null, DateTimeOffset? PackageExpiresAt = null);
public sealed record CreateInstallmentRequest(Guid Id, string Label, decimal Amount, DateOnly DueOn);
public sealed record PaymentAllocationRequest(Guid InstallmentId, decimal Amount);
public sealed record CreatePaymentRequest(Guid Id, Guid StudentId, PaymentMethod Method, decimal Amount,
    DateTimeOffset ReceivedAt, string? Note, PaymentAllocationRequest[]? Allocations);
public sealed record ReversePaymentRequest(Guid Id, DateTimeOffset ReceivedAt, string? Reason);
public sealed record ReverseInstallmentRequest(Guid Id);
public sealed record ReversePackageMovementRequest(Guid Id);
public sealed record FeeAgreementView(Guid Id, Guid StudentId, Guid CourseId, string Model, decimal Amount,
    string? BillingUnit, int? PackageSessions, DateTimeOffset? PackageExpiresAt, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? ClosedAt);
public sealed record InstallmentView(Guid Id, Guid FeeAgreementId, Guid StudentId, Guid? SourceLessonId,
    Guid? ReversalOfInstallmentId, string Label, decimal Amount, decimal RemainingAmount, DateOnly DueOn, DateTimeOffset CreatedAt);
public sealed record PaymentView(Guid Id, Guid StudentId, Guid? ReversalOfPaymentId, string Method, decimal Amount,
    DateTimeOffset ReceivedAt, string? Note, decimal AllocatedAmount);
public sealed record PackageBalanceView(Guid AgreementId, int GrantedSessions, DateTimeOffset? ExpiresAt, int RemainingSessions);
public sealed record StudentFinanceSummaryView(Guid WorkspaceId, string WorkspaceName, decimal Balance,
    IReadOnlyList<StudentPackageEntitlementView> PackageBalances);
public sealed record StudentPackageEntitlementView(Guid AgreementId, int GrantedSessions, int RemainingSessions,
    DateTimeOffset? ExpiresAt);
public sealed record PackageMovementView(Guid Id, Guid FeeAgreementId, Guid? LessonSessionId, Guid? ReversalOfMovementId,
    string Type, int Quantity, DateTimeOffset OccurredAt);
public sealed record CollectionByMethodView(string Method, decimal Amount);
public sealed record FinanceSummaryView(DateOnly From, DateOnly To, decimal TotalCharges, decimal TotalPayments, decimal Balance,
    decimal OverdueAmount, decimal DueInNext30Days, IReadOnlyList<CollectionByMethodView> CollectionsByMethod);
public sealed record FinanceInstallmentReportView(Guid InstallmentId, Guid FeeAgreementId, Guid StudentId, string StudentName,
    Guid CourseId, string CourseName, string Label, decimal Amount, decimal RemainingAmount, DateOnly DueOn);
public sealed record FinancePackageReportView(Guid AgreementId, Guid StudentId, string StudentName, Guid CourseId, string CourseName,
    decimal Amount, int GrantedSessions, DateTimeOffset? ExpiresAt, int RemainingSessions);
public sealed record FinanceWorkspaceReportView(DateOnly GeneratedOn, DateOnly UpcomingThrough,
    IReadOnlyList<FinanceInstallmentReportView> OverdueInstallments,
    IReadOnlyList<FinanceInstallmentReportView> UpcomingInstallments,
    IReadOnlyList<FinancePackageReportView> PackageBalances);
public sealed record StudentAccountView(Guid StudentId, decimal Balance, decimal TotalCharges, decimal TotalPayments,
    decimal UnallocatedPaymentAmount, IReadOnlyList<FeeAgreementView> Agreements, IReadOnlyList<InstallmentView> Installments,
    IEnumerable<PaymentView> Payments, IReadOnlyList<PackageBalanceView> PackageBalances, IReadOnlyList<PackageMovementView> PackageMovements);
