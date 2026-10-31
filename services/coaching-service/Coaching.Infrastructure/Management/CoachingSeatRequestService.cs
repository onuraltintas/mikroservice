using System.Data;
using Coaching.Application.Authorization;
using Coaching.Application.Subscriptions;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.Infrastructure.Management;

/// <summary>
/// Roadmap O10b: an institution asks for extra student seats on a running license. The price is the plan's per-seat
/// price for the days left of the license (prorated); the institution pays by bank transfer and the admin approves,
/// which raises the license's capacity and records the payment. Extra seats are not carried into a renewal.
/// </summary>
public sealed class CoachingSeatRequestService(CoachingDbContext db, ICoachingAdminScopeAuthorization scopes, ICurrentUserService actor, TimeProvider clock)
{
    public const int MaxSeatsPerRequest = 1000;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---- Institution -------------------------------------------------------------------------------------------------

    public async Task<CoachingSeatQuote> QuoteAsync(Guid subscriptionId, int seats, CancellationToken ct)
    {
        var institutionId = await RequireInstitutionAsync(ct);
        var (subscription, plan) = await LicenseAsync(institutionId, subscriptionId, ct);
        var settings = await db.CoachingSubscriptionSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct) ?? new CoachingSubscriptionSettings();
        var price = Price(subscription, plan, Seats(seats));
        var bank = settings.BankTransferEnabled;
        return new(subscription.Id, price.Seats, price.UnitPrice, price.RemainingDays, price.DurationDays, price.Amount, settings.Currency,
            bank ? settings.AccountHolder : null, bank ? settings.BankName : null, bank ? settings.Iban : null, bank ? settings.PaymentInstructions : null);
    }

    public async Task<CoachingSeatRequestSummary> CreateAsync(CoachingSeatRequestCreate request, CancellationToken ct)
    {
        var institutionId = await RequireInstitutionAsync(ct);
        var seats = Seats(request.Seats);
        var reference = request.PaymentReference?.Trim() ?? string.Empty;
        if (reference.Length is < 1 or > 120) throw new ArgumentException("Banka işlem referansı 1–120 karakter olmalıdır.");
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note?.Length > 1000) throw new ArgumentException("Not en fazla 1000 karakter olabilir.");
        var (subscription, plan) = await LicenseAsync(institutionId, request.SubscriptionId, ct);
        if (await db.Set<CoachingSeatRequest>().AnyAsync(x => x.SubscriptionId == subscription.Id && x.Status == "Pending", ct))
            throw PendingExists();
        var price = Price(subscription, plan, seats);
        var currency = await db.CoachingSubscriptionSettings.AsNoTracking().Where(x => x.Id == 1).Select(x => x.Currency).SingleOrDefaultAsync(ct) ?? "TRY";
        var entity = new CoachingSeatRequest
        {
            SubscriptionId = subscription.Id, InstitutionId = institutionId, RequestedBy = actor.UserId!.Value, Seats = seats,
            UnitPrice = price.UnitPrice, RemainingDays = price.RemainingDays, DurationDays = price.DurationDays, Amount = price.Amount,
            Currency = currency, PaymentReference = reference, Note = note, CreatedAt = Now
        };
        db.Add(entity);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.GetBaseException() is PostgresException { SqlState: "23505" }) { throw PendingExists(); }
        return ToSummary(entity, plan.Name, subscription.EndDate);
    }

    public async Task<IReadOnlyList<CoachingSeatRequestSummary>> ListMineAsync(CancellationToken ct)
    {
        var institutionId = await RequireInstitutionAsync(ct);
        return await ListAsync(db.Set<CoachingSeatRequest>().Where(x => x.InstitutionId == institutionId), ct);
    }

    public async Task<CoachingSeatRequestSummary> WithdrawAsync(Guid id, CancellationToken ct)
    {
        var institutionId = await RequireInstitutionAsync(ct);
        var request = await db.Set<CoachingSeatRequest>().SingleOrDefaultAsync(x => x.Id == id && x.InstitutionId == institutionId, ct)
            ?? throw new KeyNotFoundException();
        if (request.Status != "Pending") throw new BusinessRuleException("Sales.SeatRequestClosed", "Yalnız incelenmeyi bekleyen talep geri çekilebilir.");
        request.Status = "Withdrawn";
        await db.SaveChangesAsync(ct);
        return (await ListAsync(db.Set<CoachingSeatRequest>().Where(x => x.Id == id), ct)).Single();
    }

    // ---- Admin -------------------------------------------------------------------------------------------------------

    public Task<IReadOnlyList<CoachingSeatRequestSummary>> ListAsync(string? status, CancellationToken ct)
    {
        var query = db.Set<CoachingSeatRequest>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
        return ListAsync(query, ct);
    }

    /// <summary>Approving adds the seats to the license and records the payment; rejecting needs a reason the institution sees.</summary>
    public async Task ReviewAsync(Guid actorId, Guid id, bool approve, string? note, CancellationToken ct)
    {
        var reviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (!approve && reviewNote is null) throw new ArgumentException("Talebi reddetmek için gerekçe yazın.");
        if (reviewNote?.Length > 1000) throw new ArgumentException("Not en fazla 1000 karakter olabilir.");
        var attempts = 0;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempts++ > 0) db.ChangeTracker.Clear();
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var request = await db.Set<CoachingSeatRequest>().SingleOrDefaultAsync(x => x.Id == id && x.Status == "Pending", ct)
                ?? throw new KeyNotFoundException();
            request.Status = approve ? "Approved" : "Rejected";
            request.ReviewedBy = actorId; request.ReviewedAt = Now; request.ReviewNote = reviewNote;
            if (approve)
            {
                var subscription = await db.CoachingSubscriptions.SingleAsync(x => x.Id == request.SubscriptionId, ct);
                if (subscription.Status != "Active" || subscription.EndDate <= Now)
                    throw new BusinessRuleException("Sales.SeatRequestLicenseEnded", "Lisans sona erdiği için ek koltuk eklenemez; talebi gerekçeyle reddedin.");
                var plan = await db.CoachingSubscriptionPlans.AsNoTracking().SingleAsync(x => x.Id == subscription.PlanId, ct);
                subscription.ExtraSeats += request.Seats;
                subscription.UpdatedBy = actorId; subscription.UpdatedAt = Now;
                var payment = new CoachingPaymentRecord
                {
                    InstitutionId = request.InstitutionId, UserName = "Kurum ek koltuk", PlanId = plan.Id, SubscriptionId = subscription.Id,
                    PlanName = $"{plan.Name} · {request.Seats} ek koltuk", Amount = request.Amount, Currency = request.Currency,
                    Provider = "BankTransfer", Reference = request.PaymentReference, CreatedAt = Now
                };
                db.CoachingPaymentRecords.Add(payment);
                request.PaymentRecordId = payment.Id;
            }
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        });
    }

    // ---- Helpers -----------------------------------------------------------------------------------------------------

    private sealed record SeatPrice(int Seats, decimal UnitPrice, int RemainingDays, int DurationDays, decimal Amount);

    /// <summary>Per-seat yearly price × seats × the days left of the license (counted from its start if it has not begun, at most the plan length) ÷ the plan length.</summary>
    private SeatPrice Price(CoachingSubscription subscription, CoachingSubscriptionPlan plan, int seats)
    {
        if (plan.ExtraSeatPrice is not { } unit || unit <= 0 || plan.DurationDays <= 0)
            throw new BusinessRuleException("Sales.ExtraSeatsUnavailable", "Bu lisans için ek koltuk satışı açık değil; Koçluk yönetimiyle iletişime geçin.");
        var from = subscription.StartDate > Now ? subscription.StartDate : Now;
        // Coaching audit K1: a license lengthened by an admin can have more days left than the plan has; a seat never costs
        // more than its full yearly price.
        var remaining = Math.Clamp((int)Math.Ceiling((subscription.EndDate - from).TotalDays), 1, plan.DurationDays);
        var amount = Math.Round(unit * seats * remaining / plan.DurationDays, 2, MidpointRounding.AwayFromZero);
        return new(seats, unit, remaining, plan.DurationDays, amount);
    }

    private static int Seats(int seats) => seats is < 1 or > MaxSeatsPerRequest
        ? throw new ArgumentException($"Ek koltuk sayısı 1–{MaxSeatsPerRequest} arasında olmalıdır.") : seats;

    private async Task<(CoachingSubscription Subscription, CoachingSubscriptionPlan Plan)> LicenseAsync(Guid institutionId, Guid subscriptionId, CancellationToken ct)
    {
        var now = Now;
        var row = await (from subscription in db.CoachingSubscriptions.AsNoTracking()
                         join plan in db.CoachingSubscriptionPlans.AsNoTracking() on subscription.PlanId equals plan.Id
                         where subscription.Id == subscriptionId && subscription.InstitutionId == institutionId && subscription.Status == "Active"
                               && subscription.EndDate > now && plan.Audience == "Institution"
                         select new { subscription, plan }).SingleOrDefaultAsync(ct);
        return row is null ? throw new KeyNotFoundException() : (row.subscription, row.plan);
    }

    private async Task<IReadOnlyList<CoachingSeatRequestSummary>> ListAsync(IQueryable<CoachingSeatRequest> requests, CancellationToken ct)
    {
        var rows = await (from request in requests.AsNoTracking()
                          join subscription in db.CoachingSubscriptions.AsNoTracking() on request.SubscriptionId equals subscription.Id
                          join plan in db.CoachingSubscriptionPlans.AsNoTracking() on subscription.PlanId equals plan.Id
                          orderby request.CreatedAt descending
                          select new { request, plan.Name, subscription.EndDate }).Take(200).ToListAsync(ct);
        return rows.Select(x => ToSummary(x.request, x.Name, x.EndDate)).ToArray();
    }

    private static CoachingSeatRequestSummary ToSummary(CoachingSeatRequest x, string planName, DateTime licenseEndsAt) => new(
        x.Id, x.SubscriptionId, x.InstitutionId, planName, x.Seats, x.UnitPrice, x.RemainingDays, x.Amount, x.Currency, x.PaymentReference, x.Note,
        x.Status, x.CreatedAt, x.ReviewedAt, x.ReviewNote, x.PaymentRecordId, licenseEndsAt, x.DurationDays);

    private async Task<Guid> RequireInstitutionAsync(CancellationToken ct)
    {
        var scope = await scopes.RequireReadScopeAsync(ct);
        if (actor.UserId is null || scope.IsGlobal || scope.InstitutionId is null)
            throw new BusinessRuleException("Authorization.Forbidden", "Aktif kurum kapsamı doğrulanamadı.");
        return scope.InstitutionId.Value;
    }

    private static BusinessRuleException PendingExists() =>
        new("Sales.SeatRequestPending", "Bu lisans için incelenmeyi bekleyen bir ek koltuk talebiniz var; önce onu sonuçlandırın ya da geri çekin.");
}
