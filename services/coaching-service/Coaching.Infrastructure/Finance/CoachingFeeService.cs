using System.Data;
using Coaching.Application.Authorization;
using Coaching.Application.Finance;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.Infrastructure.Finance;

public sealed class CoachingFeeService(CoachingDbContext db, ICurrentUserService actor,
    ICoachingIdentityAuthorizationClient identity, ICoachingAdminScopeAuthorization institutions, TimeProvider clock)
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Istanbul).DateTime);

    public async Task<CoachingFeeView> CreateAsync(Guid student, CoachingFeeCreate request, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(student, true, true, ct);
        return await WriteAsync(student, async () => {
            var candidate = CoachingFeeAgreement.Create(request.RequestId, student, scope.InstitutionId, scope.CoachId,
                actor.UserId!.Value, request.Title, request.TotalAmount, request.Installments, clock.GetUtcNow().UtcDateTime);
            var existing = await Fees(scope, student).Include(x => x.Installments).SingleOrDefaultAsync(x => x.Id == request.RequestId, ct);
            if (existing is not null) {
                if (existing.Title != candidate.Title || existing.TotalAmount != candidate.TotalAmount || existing.CreatedByUserId != candidate.CreatedByUserId
                    || !existing.Installments.OrderBy(x => x.Sequence).Select(x => (x.DueOn, x.Amount))
                        .SequenceEqual(candidate.Installments.OrderBy(x => x.Sequence).Select(x => (x.DueOn, x.Amount)))) throw Conflict();
                return View(existing);
            }
            db.CoachingFeeAgreements.Add(candidate); await db.SaveChangesAsync(ct); return View(candidate);
        }, ct);
    }

    public async Task<CoachingFeeView> PayAsync(Guid student, Guid feeId, CoachingFeePaymentInput request, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(student, true, false, ct); ValidateDate(request.ReceivedOn);
        return await WriteAsync(student, async () => {
            var fee = await Fees(scope, student).Include(x => x.Installments).Include(x => x.Payments.Where(p => p.Id == request.RequestId))
                .SingleOrDefaultAsync(x => x.Id == feeId, ct) ?? throw NotFound();
            var replay = fee.Payments.Any(x => x.Id == request.RequestId);
            if (!replay) RequireVersion(fee, request.ExpectedVersion);
            Apply(() => fee.AddPayment(request.RequestId, actor.UserId!.Value, request.Amount, request.Method, request.ReceivedOn, clock.GetUtcNow().UtcDateTime, request.Reference));
            if (!replay) db.CoachingFeePayments.Add(fee.Payments.Single(x => x.Id == request.RequestId));
            await db.SaveChangesAsync(ct); return View(fee);
        }, ct);
    }

    public async Task<CoachingFeeView> ReverseAsync(Guid student, Guid feeId, Guid paymentId, CoachingFeeReversalInput request, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(student, true, false, ct); ValidateDate(request.ReceivedOn);
        return await WriteAsync(student, async () => {
            var fee = await Fees(scope, student).Include(x => x.Installments)
                .Include(x => x.Payments.Where(p => p.Id == request.RequestId || p.Id == paymentId || p.ReversalOfPaymentId == paymentId))
                .SingleOrDefaultAsync(x => x.Id == feeId, ct) ?? throw NotFound();
            var replay = fee.Payments.Any(x => x.Id == request.RequestId);
            if (!replay) RequireVersion(fee, request.ExpectedVersion);
            Apply(() => fee.ReversePayment(request.RequestId, paymentId, actor.UserId!.Value, request.ReceivedOn, request.Reason, clock.GetUtcNow().UtcDateTime));
            if (!replay) db.CoachingFeePayments.Add(fee.Payments.Single(x => x.Id == request.RequestId));
            await db.SaveChangesAsync(ct); return View(fee);
        }, ct);
    }

    public async Task<CoachingFeeView> CancelAsync(Guid student, Guid feeId, CoachingFeeCancelInput request, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(student, true, false, ct);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 300) throw new ArgumentException("A cancellation reason of at most 300 characters is required.");
        var reason = request.Reason.Trim();
        return await WriteAsync(student, async () => {
            var fee = await Fees(scope, student).Include(x => x.Installments).SingleOrDefaultAsync(x => x.Id == feeId, ct) ?? throw NotFound();
            var sameCancellation = fee.IsCancelled && fee.CancelledByUserId == actor.UserId && fee.CancellationReason == reason;
            if (!sameCancellation) RequireVersion(fee, request.ExpectedVersion);
            Apply(() => fee.Cancel(actor.UserId!.Value, reason, clock.GetUtcNow().UtcDateTime)); await db.SaveChangesAsync(ct); return View(fee);
        }, ct);
    }

    public async Task<CoachingFeeAccountView> GetStudentAsync(Guid student, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default, bool studentView = false)
    {
        Page(pageNumber, pageSize); var scope = await ScopeAsync(student, false, false, ct, studentView);
        return await ReadAsync(async () => {
            var query = Fees(scope, student).AsNoTracking(); var today = Today;
            var count = await query.CountAsync(ct);
            var total = await query.Where(x => !x.IsCancelled).SumAsync(x => x.TotalAmount, ct);
            var paid = await query.SumAsync(x => x.PaidAmount, ct);
            var overdue = await query.Where(x => !x.IsCancelled)
                .SumAsync(x => Math.Max(0m, (x.Installments.Where(i => i.DueOn < today).Sum(i => (decimal?)i.Amount) ?? 0m) - x.PaidAmount), ct);
            var rows = await query.Include(x => x.Installments).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return new CoachingFeeAccountView(total, paid, total - paid, overdue, scope.CanManage, scope.CanCreate, count, pageNumber, pageSize, rows.Select(View).ToArray());
        }, ct);
    }

    public async Task<CoachingFeePaymentPage> GetPaymentsAsync(Guid student, Guid feeId, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default, bool studentView = false)
    {
        Page(pageNumber, pageSize); var scope = await ScopeAsync(student, false, false, ct, studentView);
        return await ReadAsync(async () => {
            if (!await Fees(scope, student).AnyAsync(x => x.Id == feeId, ct)) throw NotFound();
            var query = Payments(scope, student).Where(x => x.AgreementId == feeId);
            var count = await query.CountAsync(ct);
            var rows = await PaymentViews(query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
            return new CoachingFeePaymentPage(rows, count, pageNumber, pageSize);
        }, ct);
    }

    public async Task<CoachingFeeView> GetAsync(Guid student, Guid feeId, CancellationToken ct = default, bool studentView = false)
    {
        var scope = await ScopeAsync(student, false, false, ct, studentView);
        return await ReadAsync(async () => View(await Fees(scope, student).AsNoTracking().Include(x => x.Installments)
            .SingleOrDefaultAsync(x => x.Id == feeId, ct) ?? throw NotFound()), ct);
    }

    public async Task<CoachingCashStatement> GetCashAsync(DateOnly fromDate, DateOnly toDate, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
    {
        Page(pageNumber, pageSize);
        if (fromDate == default || toDate < fromDate || toDate.DayNumber - fromDate.DayNumber > 366) throw new ArgumentException("Choose a valid cash statement range of at most 367 days.");
        var scope = await ScopeAsync(null, false, false, ct);
        return await ReadAsync(async () => {
            var query = Payments(scope, null).Where(x => x.ReceivedOn >= fromDate && x.ReceivedOn <= toDate);
            var count = await query.CountAsync(ct);
            var cash = await query.Where(x => x.Method == CoachingFeePaymentMethod.Cash).SumAsync(x => x.Amount, ct);
            var transfers = await query.Where(x => x.Method == CoachingFeePaymentMethod.BankTransfer).SumAsync(x => x.Amount, ct);
            var rows = await PaymentViews(query.OrderByDescending(x => x.ReceivedOn).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
            return new CoachingCashStatement(cash, transfers, rows, count, pageNumber, pageSize);
        }, ct);
    }

    public async Task<CoachingReceivablesView> GetReceivablesAsync(int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
    {
        Page(pageNumber, pageSize); var scope = await ScopeAsync(null, false, false, ct);
        return await ReadAsync(async () => {
            var query = Fees(scope, null).AsNoTracking().Where(x => !x.IsCancelled && x.PaidAmount < x.TotalAmount);
            var today = Today; var count = await query.CountAsync(ct);
            var balance = await query.SumAsync(x => x.TotalAmount - x.PaidAmount, ct);
            var overdue = await query.SumAsync(x => Math.Max(0m,
                (x.Installments.Where(i => i.DueOn < today).Sum(i => (decimal?)i.Amount) ?? 0m) - x.PaidAmount), ct);
            var rows = await query.Include(x => x.Installments).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return new CoachingReceivablesView(balance, overdue, rows.Select(View).ToArray(), count, pageNumber, pageSize);
        }, ct);
    }

    private async Task<FeeScope> ScopeAsync(Guid? student, bool write, bool create, CancellationToken ct, bool studentView = false)
    {
        if (!actor.IsAuthenticated || actor.UserId is not { } user || user == Guid.Empty || student == Guid.Empty) throw Forbidden();
        bool Role(string role) => actor.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        if (studentView) {
            if (write || !Role("Student") || student != user) throw Forbidden();
            return new(null, null, true, false, false);
        }
        if (Role("SystemAdmin")) { if (write) throw Forbidden(); return new(null, null, true, false, false); }
        if (Role("InstitutionAdmin") || Role("InstitutionOwner")) {
            var scope = await institutions.RequireReadScopeAsync(ct);
            if (scope.IsGlobal || scope.InstitutionId is null || scope.InstitutionId == Guid.Empty
                || create && scope.StudentIds?.Contains(student!.Value) != true) throw Forbidden();
            return new(scope.InstitutionId, null, false, true, student.HasValue && scope.StudentIds?.Contains(student.Value) == true);
        }
        if (Role("Teacher")) {
            var canCreate = false;
            if (student.HasValue && (!write || create)) {
                try { canCreate = await identity.AuthorizeTeacherTargetsAsync(user, [student.Value], null, false, ct) is null; }
                catch (BusinessRuleException error) when (!create && error.Code.StartsWith("Authorization.", StringComparison.Ordinal)) { canCreate = false; }
            }
            if (create && !canCreate) throw Forbidden();
            return new(null, user, false, true, canCreate);
        }
        if (!write && Role("Student") && student == user) return new(null, null, true, false, false);
        throw Forbidden();
    }

    private IQueryable<CoachingFeeAgreement> Fees(FeeScope scope, Guid? student)
    {
        var query = db.CoachingFeeAgreements.AsQueryable();
        if (student.HasValue) query = query.Where(x => x.StudentId == student);
        if (!scope.All) query = scope.InstitutionId.HasValue ? query.Where(x => x.InstitutionId == scope.InstitutionId) : query.Where(x => x.CoachId == scope.CoachId);
        return query;
    }
    private IQueryable<CoachingFeePayment> Payments(FeeScope scope, Guid? student) => db.CoachingFeePayments.AsNoTracking()
        .Where(payment => Fees(scope, student).Any(fee => fee.Id == payment.AgreementId));
    private IQueryable<CoachingFeePaymentView> PaymentViews(IQueryable<CoachingFeePayment> query) =>
        from payment in query
        join fee in db.CoachingFeeAgreements.AsNoTracking() on payment.AgreementId equals fee.Id
        select new CoachingFeePaymentView(payment.Id, fee.Id, fee.StudentId, payment.RecordedByUserId, payment.ReversalOfPaymentId,
            payment.Method, payment.Amount, payment.ReceivedOn, payment.Reference, payment.CreatedAt);

    private CoachingFeeView View(CoachingFeeAgreement fee)
    {
        var remainingPaid = fee.PaidAmount; var today = Today;
        var installments = fee.Installments.OrderBy(x => x.Sequence).Select(x => {
            var allocated = Math.Min(remainingPaid, x.Amount); remainingPaid -= allocated;
            var balance = fee.IsCancelled ? 0m : x.Amount - allocated;
            return new CoachingFeeInstallmentView(x.Id, x.Sequence, x.DueOn, x.Amount, balance, balance > 0m && x.DueOn < today);
        }).ToArray();
        return new(fee.Id, fee.StudentId, fee.InstitutionId, fee.CoachId, fee.Version, fee.Title, "TRY", fee.TotalAmount,
            fee.PaidAmount, fee.Balance, installments.Where(x => x.IsOverdue).Sum(x => x.Balance), fee.IsCancelled, fee.CancellationReason, fee.CancelledAt, installments);
    }

    private async Task<T> WriteAsync<T>(Guid student, Func<Task<T>> action, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try {
                var key = "coaching-finance:" + student;
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
                var result = await action(); await transaction.CommitAsync(ct); return result;
            }
            catch (DbUpdateException error) when (error.GetBaseException() is PostgresException { SqlState: "23505" }) { throw Conflict(); }
            finally {
                foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is CoachingFeeAgreement or CoachingFeeInstallment or CoachingFeePayment).ToArray()) entry.State = EntityState.Detached;
            }
        });
    }
    private Task<T> ReadAsync<T>(Func<Task<T>> action, CancellationToken ct) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct); return await action();
    });
    private void ValidateDate(DateOnly date) { if (date == default || date > Today) throw new ArgumentException("A receipt date cannot be in the future."); }
    private static void Page(int number, int size) { if (number is < 1 or > 10000 || size is < 1 or > 100) throw new ArgumentException("Choose a valid page and a page size of 1–100."); }
    private static void RequireVersion(CoachingFeeAgreement fee, int version) { if (version < 0 || fee.Version != version) throw Conflict(); }
    private static void Apply(Action change) {
        try { change(); }
        catch (InvalidOperationException) { throw new BusinessRuleException("Finance.Conflict", "İşlem uygulanamadı. Kalan borcu, önceki tahsilatı ve kayıt sürümünü kontrol edin."); }
    }
    private static BusinessRuleException Conflict() => new("Finance.Conflict", "Ücret kaydı değişti veya işlem kimliği daha önce kullanıldı. Güncel kaydı yükleyin.");
    private static BusinessRuleException NotFound() => new("Finance.NotFound", "Bu kapsamda ücret kaydı bulunamadı.");
    private static BusinessRuleException Forbidden() => new("Authorization.Forbidden", "Koçluk ücret kaydı için yetkiniz yok.");
    private sealed record FeeScope(Guid? InstitutionId, Guid? CoachId, bool All, bool CanManage, bool CanCreate);
}
