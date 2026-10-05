using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Linq.Expressions;
using System.Data;
using Coaching.Application.Exceptions;
using Coaching.Application.Queries;
using Coaching.Application.Interfaces;
using Coaching.Application.Subscriptions;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Management;

public sealed class CoachingSubscriptionService(
    CoachingDbContext db,
    ICoachingIdentityAuthorizationClient identityAuthorizationClient) : ICoachingSubscription
{
    private const string SettingsIdempotencyScope = "coaching.subscription.settings.update";
    private const string TransferCreateIdempotencyScope = "coaching.bank-transfer.create";
    private const string TransferReviewIdempotencyScope = "coaching.bank-transfer.review";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SubscriptionStatuses = new(StringComparer.Ordinal)
    {
        "Active",
        "Cancelled",
        "Expired"
    };

    public async Task<IReadOnlyList<CoachingSubscriptionPlanSummary>> GetPlansAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = db.CoachingSubscriptionPlans.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(plan => plan.IsActive && plan.IsPublic);
        }

        var plans = await query.OrderBy(plan => plan.SortOrder).ThenBy(plan => plan.Name)
            .ToListAsync(cancellationToken);
        return plans.Select(ToSummary).ToArray();
    }

    public async Task<CoachingSubscriptionPlanSummary?> GetPlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await db.CoachingSubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return plan is null ? null : ToSummary(plan);
    }

    public async Task<Guid?> CreatePlanAsync(
        CoachingSubscriptionPlanRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePlan(request);
        if (!CoachingManagementRules.IsValidPlan(
            normalized.Audience, normalized.Price, normalized.IsContactOnly, normalized.BillingPeriod,
            normalized.DurationDays, normalized.IncludedStudentSeats)
            || await db.CoachingSubscriptionPlans.AnyAsync(plan => plan.Slug == normalized.Slug, cancellationToken))
        {
            return null;
        }

        var plan = new CoachingSubscriptionPlan
        {
            Slug = normalized.Slug,
            Name = normalized.Name,
            Description = normalized.Description,
            Audience = normalized.Audience,
            Price = normalized.Price,
            IsContactOnly = normalized.IsContactOnly,
            BillingPeriod = normalized.BillingPeriod,
            DurationDays = normalized.DurationDays,
            IncludedStudentSeats = normalized.IncludedStudentSeats,
            FeaturesJson = JsonSerializer.Serialize(normalized.Features, JsonOptions),
            IsActive = normalized.IsActive,
            IsPublic = normalized.IsPublic,
            SortOrder = normalized.SortOrder,
            CreatedBy = actorId
        };
        db.CoachingSubscriptionPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        return plan.Id;
    }

    public async Task<CoachingSubscriptionPlanSummary?> UpdatePlanAsync(
        Guid id,
        CoachingSubscriptionPlanRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePlan(request);
        if (!CoachingManagementRules.IsValidPlan(
            normalized.Audience, normalized.Price, normalized.IsContactOnly, normalized.BillingPeriod,
            normalized.DurationDays, normalized.IncludedStudentSeats))
        {
            return null;
        }

        var plan = await db.CoachingSubscriptionPlans.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (plan is null
            || await db.CoachingSubscriptionPlans.AnyAsync(
                item => item.Id != id && item.Slug == normalized.Slug,
                cancellationToken))
        {
            return null;
        }

        plan.Slug = normalized.Slug;
        plan.Name = normalized.Name;
        plan.Description = normalized.Description;
        plan.Audience = normalized.Audience;
        plan.Price = normalized.Price;
        plan.IsContactOnly = normalized.IsContactOnly;
        plan.BillingPeriod = normalized.BillingPeriod;
        plan.DurationDays = normalized.DurationDays;
        plan.IncludedStudentSeats = normalized.IncludedStudentSeats;
        plan.FeaturesJson = JsonSerializer.Serialize(normalized.Features, JsonOptions);
        plan.IsActive = normalized.IsActive;
        plan.IsPublic = normalized.IsPublic;
        plan.SortOrder = normalized.SortOrder;
        plan.UpdatedBy = actorId;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(plan);
    }

    public async Task<bool> DeactivatePlanAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var plan = await db.CoachingSubscriptionPlans.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (plan is null)
        {
            return false;
        }

        plan.IsActive = false;
        plan.IsPublic = false;
        plan.UpdatedBy = actorId;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CoachingSubscriptionSettingsSummary> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await db.CoachingSubscriptionSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
        return ToSettingsSummary(settings ?? new CoachingSubscriptionSettings());
    }

    public async Task<CoachingSubscriptionSettingsSummary?> GetPublicSettingsAsync(CancellationToken cancellationToken = default)
    {
        var summary = await GetSettingsAsync(cancellationToken);
        return summary.IsPubliclyAvailable ? summary : null;
    }

    public async Task<CoachingSubscriptionSettingsSummary> UpdateSettingsAsync(
        CoachingSubscriptionSettingsRequest request,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var key = ValidateIdempotencyKey(actorId, idempotencyKey);
        var currency = request.Currency.Trim().ToUpperInvariant();
        var holder = NormalizeOptional(request.AccountHolder, 200);
        var bank = NormalizeOptional(request.BankName, 200);
        var iban = CoachingManagementRules.NormalizeIban(request.Iban);
        var instructions = NormalizeOptional(request.PaymentInstructions, 2000);
        if (currency is not ("TRY" or "EUR" or "USD")
            || (request.BankTransferEnabled && (holder is null || bank is null || !CoachingManagementRules.IsValidIban(iban))))
        {
            throw new ArgumentException("Para birimi veya yayınlanacak banka bilgileri geçersiz.");
        }

        var requestHash = CreateRequestHash(
            actorId.ToString("D"), SettingsIdempotencyScope, request.RequireActiveSubscription.ToString(), currency,
            holder, bank, iban, instructions, request.BankTransferEnabled.ToString());
        var replay = await GetIdempotencyRecordAsync(SettingsIdempotencyScope, key, cancellationToken);
        if (replay is not null)
        {
            EnsureIdempotencyMatches(replay, requestHash);
            return await GetSettingsAsync(cancellationToken);
        }

        var settings = await db.CoachingSubscriptionSettings.SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
        if (settings is null)
        {
            settings = new CoachingSubscriptionSettings();
            db.CoachingSubscriptionSettings.Add(settings);
        }

        settings.RequireActiveSubscription = request.RequireActiveSubscription;
        settings.Currency = currency;
        settings.AccountHolder = holder;
        settings.BankName = bank;
        settings.Iban = iban;
        settings.PaymentInstructions = instructions;
        settings.BankTransferEnabled = request.BankTransferEnabled;
        settings.UpdatedBy = actorId;
        settings.UpdatedAt = DateTime.UtcNow;
        db.IdempotencyRecords.Add(IdempotencyRecord.Create(
            SettingsIdempotencyScope, key, requestHash, Guid.Empty));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (IdempotencyConflictException)
        {
            db.ChangeTracker.Clear();
            var concurrent = await GetIdempotencyRecordAsync(SettingsIdempotencyScope, key, cancellationToken);
            if (concurrent is null) throw;
            EnsureIdempotencyMatches(concurrent, requestHash);
            return await GetSettingsAsync(cancellationToken);
        }

        return ToSettingsSummary(settings);
    }

    public Task<CoachingBankTransferRequestSummary?> CreateBankTransferRequestAsync(
        Guid userId,
        string? userName,
        string? userEmail,
        CoachingBankTransferRequestCreate request,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        CreateBankTransferRequestAsync(userId, userName, userEmail, request, "Individual", idempotencyKey, cancellationToken);

    public Task<CoachingBankTransferRequestSummary?> CreateTeacherBankTransferRequestAsync(
        Guid userId,
        string? userName,
        string? userEmail,
        CoachingBankTransferRequestCreate request,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        CreateBankTransferRequestAsync(userId, userName, userEmail, request, "Teacher", idempotencyKey, cancellationToken);

    private Task<CoachingBankTransferRequestSummary?> CreateBankTransferRequestAsync(
        Guid userId, string? userName, string? userEmail, CoachingBankTransferRequestCreate request,
        string expectedAudience, string idempotencyKey, CancellationToken cancellationToken) =>
        ExecuteWithRetryAsync(() => CreateBankTransferRequestCoreAsync(
            userId, userName, userEmail, request, expectedAudience, idempotencyKey, cancellationToken), cancellationToken);

    private async Task<CoachingBankTransferRequestSummary?> CreateBankTransferRequestCoreAsync(
        Guid userId,
        string? userName,
        string? userEmail,
        CoachingBankTransferRequestCreate request,
        string expectedAudience,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!request.AdultPayerDeclaration) return null;
        var key = ValidateIdempotencyKey(userId, idempotencyKey);
        var reference = NormalizePaymentReference(request.PaymentReference);
        var payerName = NormalizeOptional(request.PayerName, 200);
        var note = NormalizeOptional(request.Note, 1000);
        if (reference is null)
        {
            return null;
        }

        var requestHash = CreateRequestHash(
            userId.ToString("D"), TransferCreateIdempotencyScope, expectedAudience, request.PlanId.ToString("D"),
            reference, payerName, note, "adult-payer-v1");
        var replay = await GetIdempotencyRecordAsync(TransferCreateIdempotencyScope, key, cancellationToken);
        if (replay is not null)
        {
            EnsureIdempotencyMatches(replay, requestHash);
            return await GetBankTransferSummaryAsync(replay.ResourceId, userId, cancellationToken);
        }

        var settings = await GetPublicSettingsAsync(cancellationToken);
        if (settings is null || userId == Guid.Empty)
        {
            return null;
        }

        var plan = await db.CoachingSubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.PlanId && item.IsActive && item.IsPublic,
            cancellationToken);
        if (plan is null || plan.Audience != expectedAudience || plan.IsContactOnly || plan.Price <= 0 || reference is null)
        {
            return null;
        }

        if (expectedAudience == "Teacher" && !await IsIndependentTeacherAsync(userId, cancellationToken))
        {
            return null;
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var existingReference = await db.CoachingBankTransferRequests
            .SingleOrDefaultAsync(item => item.UserId == userId && item.PaymentReference == reference, cancellationToken);
        if (existingReference is not null)
        {
            db.IdempotencyRecords.Add(IdempotencyRecord.Create(
                TransferCreateIdempotencyScope, key, requestHash, existingReference.Id));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch (IdempotencyConflictException)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                var concurrent = await GetIdempotencyRecordAsync(TransferCreateIdempotencyScope, key, cancellationToken);
                if (concurrent is null) throw;
                EnsureIdempotencyMatches(concurrent, requestHash);
                return await GetBankTransferSummaryAsync(concurrent.ResourceId, userId, cancellationToken);
            }

            return await GetBankTransferSummaryAsync(existingReference.Id, userId, cancellationToken);
        }

        var existing = await db.CoachingBankTransferRequests
            .Where(item => item.UserId == userId && item.PlanId == plan.Id && item.Status == "Pending")
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            db.IdempotencyRecords.Add(IdempotencyRecord.Create(
                TransferCreateIdempotencyScope, key, requestHash, existing.Id));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch (IdempotencyConflictException)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                var concurrent = await GetIdempotencyRecordAsync(TransferCreateIdempotencyScope, key, cancellationToken);
                if (concurrent is null) throw;
                EnsureIdempotencyMatches(concurrent, requestHash);
                return await GetBankTransferSummaryAsync(concurrent.ResourceId, userId, cancellationToken);
            }

            return await GetBankTransferSummaryAsync(existing, cancellationToken);
        }

        var requestEntity = new CoachingBankTransferRequest
        {
            UserId = userId,
            UserName = NormalizeOptional(userName, 200) ?? "Koçluk kullanıcısı",
            UserEmail = NormalizeOptional(userEmail, 320) ?? string.Empty,
            PlanId = plan.Id,
            Amount = plan.Price,
            Currency = settings.Currency,
            PaymentReference = reference,
            PayerName = payerName,
            Note = note,
            AdultPayerDeclarationVersion = 1,
            AdultPayerDeclaredAt = DateTime.UtcNow
        };
        db.CoachingBankTransferRequests.Add(requestEntity);
        db.IdempotencyRecords.Add(IdempotencyRecord.Create(
            TransferCreateIdempotencyScope, key, requestHash, requestEntity.Id));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (IdempotencyConflictException)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var concurrent = await GetIdempotencyRecordAsync(TransferCreateIdempotencyScope, key, cancellationToken);
            if (concurrent is null) throw;
            EnsureIdempotencyMatches(concurrent, requestHash);
            return await GetBankTransferSummaryAsync(concurrent.ResourceId, userId, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsSerializationFailure(exception) || IsBankTransferReferenceConflict(exception))
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var concurrent = await db.CoachingBankTransferRequests.AsNoTracking()
                .SingleOrDefaultAsync(item => item.UserId == userId && item.PaymentReference == reference, cancellationToken);
            return concurrent is null ? null : await GetBankTransferSummaryAsync(concurrent.Id, userId, cancellationToken);
        }

        return await GetBankTransferSummaryAsync(requestEntity, cancellationToken);
    }

    public async Task<IReadOnlyList<CoachingBankTransferRequestSummary>> GetMyBankTransferRequestsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await (from request in db.CoachingBankTransferRequests.AsNoTracking()
                          join plan in db.CoachingSubscriptionPlans.AsNoTracking() on request.PlanId equals plan.Id
                          where request.UserId == userId
                          orderby request.CreatedAt descending
                          select new { request, plan }).ToListAsync(cancellationToken);
        return rows.Select(row => ToTransferSummary(row.request, row.plan)).ToArray();
    }

    public async Task<PagedResponse<CoachingBankTransferRequestSummary>> GetBankTransferRequestsAsync(
        string? search,
        string? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(pageNumber, pageSize);
        var query = from request in db.CoachingBankTransferRequests.AsNoTracking()
                    join plan in db.CoachingSubscriptionPlans.AsNoTracking() on request.PlanId equals plan.Id
                    select new { request, plan };
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(row => row.request.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(row => row.request.UserName.Contains(term)
                || row.request.UserEmail.Contains(term)
                || row.request.PaymentReference.Contains(term)
                || row.plan.Name.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(row => row.request.CreatedAt)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResponse<CoachingBankTransferRequestSummary>(
            rows.Select(row => ToTransferSummary(row.request, row.plan)).ToArray(), pageNumber, pageSize, total);
    }

    public Task<CoachingBankTransferRequestSummary?> ReviewBankTransferRequestAsync(
        Guid id, CoachingBankTransferReviewRequest request, Guid actorId, string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        ExecuteWithRetryAsync(() => ReviewBankTransferRequestCoreAsync(id, request, actorId, idempotencyKey, cancellationToken), cancellationToken);

    private async Task<CoachingBankTransferRequestSummary?> ReviewBankTransferRequestCoreAsync(
        Guid id,
        CoachingBankTransferReviewRequest request,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var key = ValidateIdempotencyKey(actorId, idempotencyKey);
        var status = request.Status?.Trim();
        if (status is not ("Approved" or "Rejected"))
        {
            return null;
        }

        var reviewNote = NormalizeOptional(request.ReviewNote, 1000);
        if (status == "Rejected" && reviewNote is null)
        {
            return null;
        }

        var requestHash = CreateRequestHash(
            actorId.ToString("D"), TransferReviewIdempotencyScope, id.ToString("D"), status, reviewNote);
        var replay = await GetIdempotencyRecordAsync(TransferReviewIdempotencyScope, key, cancellationToken);
        if (replay is not null)
        {
            EnsureIdempotencyMatches(replay, requestHash);
            return await GetBankTransferSummaryAsync(replay.ResourceId, cancellationToken);
        }

        var transfer = await db.CoachingBankTransferRequests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (transfer is null)
        {
            return null;
        }

        if (transfer.Status != "Pending")
        {
            if (transfer.Status != status)
            {
                return null;
            }

            db.IdempotencyRecords.Add(IdempotencyRecord.Create(
                TransferReviewIdempotencyScope, key, requestHash, transfer.Id));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (IdempotencyConflictException)
            {
                db.ChangeTracker.Clear();
                var concurrent = await GetIdempotencyRecordAsync(TransferReviewIdempotencyScope, key, cancellationToken);
                if (concurrent is null) throw;
                EnsureIdempotencyMatches(concurrent, requestHash);
                return await GetBankTransferSummaryAsync(concurrent.ResourceId, cancellationToken);
            }

            return await GetBankTransferSummaryAsync(transfer, cancellationToken);
        }

        CoachingSubscriptionPlan? approvedPlan = null;
        DateTime? subscriptionStartDate = null;
        DateTime? subscriptionEndDate = null;
        CoachingSubscription? renewalSubscription = null;
        if (status == "Approved")
        {
            approvedPlan = await db.CoachingSubscriptionPlans.SingleOrDefaultAsync(item => item.Id == transfer.PlanId, cancellationToken);
            if (approvedPlan is null || approvedPlan.Audience is not ("Individual" or "Teacher"))
            {
                return null;
            }

            if (approvedPlan.Audience == "Teacher"
                && !await IsIndependentTeacherAsync(transfer.UserId, cancellationToken))
            {
                return null;
            }
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        if (approvedPlan is not null)
        {
            var now = DateTime.UtcNow;
            if (approvedPlan.Audience == "Teacher")
            {
                renewalSubscription = await db.CoachingSubscriptions
                    .Where(item => item.UserId == transfer.UserId
                        && item.PlanId == approvedPlan.Id
                        && item.Status == "Active"
                        && item.StartDate <= now)
                    .OrderByDescending(item => item.EndDate)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            subscriptionStartDate = renewalSubscription is not null && renewalSubscription.EndDate > now
                ? renewalSubscription.EndDate
                : now;
            subscriptionEndDate = subscriptionStartDate.Value.AddDays(approvedPlan.DurationDays);
            if (approvedPlan.Audience == "Teacher"
                && await HasOverlappingTeacherSubscriptionAsync(
                    transfer.UserId,
                    subscriptionStartDate.Value,
                    subscriptionEndDate.Value,
                    cancellationToken,
                    renewalSubscription?.Id))
            {
                return null;
            }
        }

        if (status == "Approved")
        {
            var plan = approvedPlan!;
            var subscription = renewalSubscription ?? new CoachingSubscription
            {
                PlanId = plan.Id,
                UserId = transfer.UserId,
                UserName = transfer.UserName,
                UserEmail = transfer.UserEmail,
                Status = "Active",
                StartDate = subscriptionStartDate!.Value,
                EndDate = subscriptionEndDate!.Value,
                Notes = "Banka transferi onaylandı.",
                BankTransferRequestId = transfer.Id,
                CreatedBy = actorId
            };
            if (renewalSubscription is null)
            {
                db.CoachingSubscriptions.Add(subscription);
            }
            else
            {
                subscription.EndDate = subscriptionEndDate!.Value;
                subscription.Status = "Active";
                subscription.UpdatedBy = actorId;
                subscription.UpdatedAt = DateTime.UtcNow;
            }

            transfer.SubscriptionId = subscription.Id;
            db.CoachingPaymentRecords.Add(new CoachingPaymentRecord
            {
                UserId = transfer.UserId,
                UserName = transfer.UserName,
                UserEmail = transfer.UserEmail,
                PlanId = transfer.PlanId,
                PlanName = plan.Name,
                SubscriptionId = subscription.Id,
                BankTransferRequestId = transfer.Id,
                Amount = transfer.Amount,
                Currency = transfer.Currency,
                Provider = "BankTransfer",
                Reference = transfer.PaymentReference
            });
        }

        transfer.Status = status;
        transfer.ReviewedBy = actorId;
        transfer.ReviewedAt = DateTime.UtcNow;
        transfer.ReviewNote = reviewNote;
        transfer.UpdatedAt = DateTime.UtcNow;
        transfer.Version++;
        db.IdempotencyRecords.Add(IdempotencyRecord.Create(
            TransferReviewIdempotencyScope, key, requestHash, transfer.Id));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (IdempotencyConflictException)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var concurrent = await GetIdempotencyRecordAsync(TransferReviewIdempotencyScope, key, cancellationToken);
            if (concurrent is null) throw;
            EnsureIdempotencyMatches(concurrent, requestHash);
            return await GetBankTransferSummaryAsync(concurrent.ResourceId, cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
        catch (DbUpdateException exception) when (IsSerializationFailure(exception))
        {
            return null;
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return await GetBankTransferSummaryAsync(transfer, cancellationToken);
    }

    public async Task<bool> DeleteBankTransferRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transfer = await db.CoachingBankTransferRequests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (transfer is null)
        {
            return false;
        }

        var idempotencyRecords = await db.IdempotencyRecords
            .Where(item => item.ResourceId == id
                && (item.Scope == TransferCreateIdempotencyScope || item.Scope == TransferReviewIdempotencyScope))
            .ToListAsync(cancellationToken);
        db.IdempotencyRecords.RemoveRange(idempotencyRecords);

        var paymentRecords = await db.CoachingPaymentRecords
            .Where(item => item.BankTransferRequestId == id)
            .ToListAsync(cancellationToken);
        foreach (var payment in paymentRecords)
        {
            payment.BankTransferRequestId = null;
        }

        var subscriptions = await db.CoachingSubscriptions
            .Where(item => item.BankTransferRequestId == id)
            .ToListAsync(cancellationToken);
        foreach (var subscription in subscriptions)
        {
            subscription.BankTransferRequestId = null;
        }

        db.CoachingBankTransferRequests.Remove(transfer);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PagedResponse<CoachingSubscriptionSummary>> GetSubscriptionsAsync(
        string? search,
        string? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(pageNumber, pageSize);
        var query = from subscription in db.CoachingSubscriptions.AsNoTracking()
                    join plan in db.CoachingSubscriptionPlans.AsNoTracking() on subscription.PlanId equals plan.Id
                    select new { subscription, plan };
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(row => row.subscription.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(row => (row.subscription.UserName != null && row.subscription.UserName.Contains(term))
                || (row.subscription.UserEmail != null && row.subscription.UserEmail.Contains(term))
                || row.plan.Name.Contains(term)
                || (row.subscription.InstitutionId.HasValue && row.subscription.InstitutionId.Value.ToString().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(row => row.subscription.CreatedAt)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var seatCounts = await GetSeatCountsAsync(rows.Select(row => row.subscription.Id).ToArray(), cancellationToken);
        var items = rows.Select(row => ToSubscriptionSummary(
            row.subscription, row.plan, seatCounts.GetValueOrDefault(row.subscription.Id))).ToArray();
        return new PagedResponse<CoachingSubscriptionSummary>(items, pageNumber, pageSize, total);
    }

    public Task<CoachingSubscriptionSummary?> CreateSubscriptionAsync(
        CoachingSubscriptionCreateRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var subscriptionId = Guid.NewGuid();
        return ExecuteWithRetryAsync(() => CreateSubscriptionCoreAsync(request, actorId, subscriptionId, cancellationToken), cancellationToken);
    }

    private async Task<CoachingSubscriptionSummary?> CreateSubscriptionCoreAsync(
        CoachingSubscriptionCreateRequest request,
        Guid actorId,
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        // A connection failure after COMMIT must not create a second license on retry.
        if (await db.CoachingSubscriptions.AsNoTracking().AnyAsync(item => item.Id == subscriptionId, cancellationToken))
        {
            return await GetSubscriptionAsync(subscriptionId, cancellationToken);
        }
        var userOwned = request.UserId.HasValue;
        var institutionOwned = request.InstitutionId.HasValue;
        if (userOwned == institutionOwned || request.UserId == Guid.Empty || request.InstitutionId == Guid.Empty)
        {
            return null;
        }

        var plan = await db.CoachingSubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.PlanId && item.IsActive,
            cancellationToken);
        if (plan is null
            || (userOwned && plan.Audience is not ("Individual" or "Teacher"))
            || (institutionOwned && plan.Audience != "Institution")
            || (plan.Audience is "Teacher" or "Institution" && plan.IncludedStudentSeats is null))
        {
            return null;
        }

        var paymentReference = institutionOwned ? NormalizePaymentReference(request.PaymentReference) : null;
        if (institutionOwned && request.PaymentReference is not null && paymentReference is null)
        {
            return null;
        }

        var startDate = request.StartDate.Kind == DateTimeKind.Utc ? request.StartDate : request.StartDate.ToUniversalTime();
        var studentIds = (request.StudentIds ?? Array.Empty<Guid>()).Distinct().ToArray();
        if (startDate < DateTime.UnixEpoch
            || (userOwned && studentIds.Length > 0)
            || studentIds.Any(studentId => studentId == Guid.Empty)
            || (institutionOwned && studentIds.Length > plan.IncludedStudentSeats.GetValueOrDefault()))
        {
            return null;
        }

        if (institutionOwned && paymentReference is not null)
        {
            var existing = await db.CoachingSubscriptions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.InstitutionId == request.InstitutionId
                    && item.PaymentReference == paymentReference, cancellationToken);
            if (existing is not null)
            {
                return await GetSubscriptionAsync(existing.Id, cancellationToken);
            }
        }

        var endDate = startDate.AddDays(plan.DurationDays);
        if (plan.Audience == "Teacher"
            && !await IsIndependentTeacherAsync(request.UserId!.Value, cancellationToken))
        {
            return null;
        }

        await using var transaction = plan.Audience == "Teacher" && db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        if (plan.Audience == "Teacher"
            && await HasOverlappingTeacherSubscriptionAsync(request.UserId!.Value, startDate, endDate, cancellationToken))
        {
            return null;
        }

        var subscription = new CoachingSubscription
        {
            Id = subscriptionId,
            PlanId = plan.Id,
            UserId = request.UserId,
            UserName = NormalizeOptional(request.UserName, 200),
            UserEmail = NormalizeOptional(request.UserEmail, 320),
            InstitutionId = request.InstitutionId,
            Status = "Active",
            StartDate = startDate,
            EndDate = endDate,
            Notes = NormalizeOptional(request.Notes, 2000),
            PaymentReference = paymentReference,
            CreatedBy = actorId
        };
        db.CoachingSubscriptions.Add(subscription);
        foreach (var studentId in studentIds)
        {
            db.CoachingSubscriptionSeats.Add(new CoachingSubscriptionSeat
            {
                SubscriptionId = subscription.Id,
                StudentId = studentId,
                AssignedBy = actorId
            });
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (Npgsql.PostgresException exception) when (exception.SqlState == Npgsql.PostgresErrorCodes.SerializationFailure)
        {
            return null;
        }
        catch (DbUpdateException exception) when (IsInstitutionPaymentReferenceConflict(exception))
        {
            db.ChangeTracker.Clear();
            var existing = await db.CoachingSubscriptions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.InstitutionId == request.InstitutionId
                    && item.PaymentReference == paymentReference, cancellationToken);
            return existing is null ? null : await GetSubscriptionAsync(existing.Id, cancellationToken);
        }

        return ToSubscriptionSummary(subscription, plan, (studentIds.Length, studentIds.Length));
    }

    public Task<CoachingSubscriptionSummary?> UpdateSubscriptionAsync(
        Guid id, CoachingSubscriptionUpdateRequest request, Guid actorId, CancellationToken cancellationToken = default) =>
        ExecuteWithRetryAsync(() => UpdateSubscriptionCoreAsync(id, request, actorId, cancellationToken), cancellationToken);

    private async Task<CoachingSubscriptionSummary?> UpdateSubscriptionCoreAsync(
        Guid id,
        CoachingSubscriptionUpdateRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var status = request.Status.Trim();
        if (!SubscriptionStatuses.Contains(status))
        {
            return null;
        }

        var subscription = await db.CoachingSubscriptions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subscription is null)
        {
            return null;
        }

        var endDate = request.EndDate.HasValue
            ? request.EndDate.Value.Kind == DateTimeKind.Utc ? request.EndDate.Value : request.EndDate.Value.ToUniversalTime()
            : subscription.EndDate;
        if (endDate < subscription.StartDate)
        {
            return null;
        }

        var plan = await db.CoachingSubscriptionPlans.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == subscription.PlanId, cancellationToken);
        var reactivatingTeacherPlan = status == "Active"
            && plan?.Audience == "Teacher"
            && subscription.UserId.HasValue;
        if (reactivatingTeacherPlan
            && !await IsIndependentTeacherAsync(subscription.UserId!.Value, cancellationToken))
        {
            return null;
        }

        await using var transaction = reactivatingTeacherPlan && db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        if (reactivatingTeacherPlan
            && await HasOverlappingTeacherSubscriptionAsync(
                subscription.UserId!.Value, subscription.StartDate, endDate, cancellationToken, subscription.Id))
        {
            return null;
        }

        subscription.Status = status;
        subscription.EndDate = endDate;
        subscription.Notes = NormalizeOptional(request.Notes, 2000);
        subscription.UpdatedBy = actorId;
        subscription.UpdatedAt = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (Npgsql.PostgresException exception) when (exception.SqlState == Npgsql.PostgresErrorCodes.SerializationFailure)
        {
            return null;
        }

        return await GetSubscriptionAsync(subscription.Id, cancellationToken);
    }

    public async Task<bool> ChangeStudentSeatAsync(
        Guid subscriptionId,
        Guid studentId,
        CoachingSubscriptionSeatChangeRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await db.CoachingSubscriptions.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == subscriptionId && item.InstitutionId.HasValue,
            cancellationToken);
        var seat = await db.CoachingSubscriptionSeats.SingleOrDefaultAsync(
            item => item.SubscriptionId == subscriptionId && item.StudentId == studentId,
            cancellationToken);
        if (subscription is null || seat is null)
        {
            return false;
        }

        seat.IsSuspended = request.IsSuspended;
        seat.SuspensionReason = request.IsSuspended ? NormalizeOptional(request.Reason, 500) : null;
        seat.AssignedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<CoachingSubscriptionSeatSummary>?> GetSubscriptionSeatsAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var isInstitutionPlan = await db.CoachingSubscriptions.AsNoTracking()
            .AnyAsync(item => item.Id == subscriptionId && item.InstitutionId.HasValue, cancellationToken);
        if (!isInstitutionPlan)
        {
            return null;
        }

        return await db.CoachingSubscriptionSeats.AsNoTracking()
            .Where(seat => seat.SubscriptionId == subscriptionId)
            .OrderBy(seat => seat.AssignedAt)
            .Select(seat => new CoachingSubscriptionSeatSummary(
                seat.StudentId, seat.IsSuspended, seat.SuspensionReason, seat.AssignedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<CoachingTeacherSubscriptionSeatManagementSummary?> GetMyTeacherSubscriptionAsync(
        Guid teacherId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsIndependentTeacherAsync(teacherId, cancellationToken))
        {
            return null;
        }

        var active = await GetActiveTeacherSubscriptionAsync(teacherId, cancellationToken);
        if (active is null)
        {
            return null;
        }

        var (subscription, plan) = active.Value;
        var seats = await db.CoachingSubscriptionSeats.AsNoTracking()
            .Where(seat => seat.SubscriptionId == subscription.Id)
            .OrderBy(seat => seat.AssignedAt)
            .Select(seat => new CoachingSubscriptionSeatSummary(
                seat.StudentId, seat.IsSuspended, seat.SuspensionReason, seat.AssignedAt))
            .ToArrayAsync(cancellationToken);

        return new CoachingTeacherSubscriptionSeatManagementSummary(
            subscription.Id,
            plan.Name,
            subscription.EndDate,
            plan.IncludedStudentSeats.GetValueOrDefault(),
            seats.Count(seat => !seat.IsSuspended),
            seats);
    }

    public Task<bool> AssignMyTeacherStudentSeatAsync(
        Guid teacherId, Guid studentId, CancellationToken cancellationToken = default) =>
        ExecuteWithRetryAsync(() => AssignMyTeacherStudentSeatCoreAsync(teacherId, studentId, cancellationToken), cancellationToken);

    private async Task<bool> AssignMyTeacherStudentSeatCoreAsync(
        Guid teacherId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (teacherId == Guid.Empty || studentId == Guid.Empty)
        {
            return false;
        }

        var active = await GetActiveTeacherSubscriptionAsync(teacherId, cancellationToken);
        if (active is null)
        {
            return false;
        }

        var (subscription, plan) = active.Value;
        if (!await IsAssignedIndependentTeacherStudentAsync(teacherId, studentId, cancellationToken))
        {
            return false;
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var seat = await db.CoachingSubscriptionSeats.SingleOrDefaultAsync(
            item => item.SubscriptionId == subscription.Id && item.StudentId == studentId,
            cancellationToken);
        if (seat is { IsSuspended: false })
        {
            return true;
        }

        var usedSeats = await db.CoachingSubscriptionSeats.CountAsync(
            item => item.SubscriptionId == subscription.Id && !item.IsSuspended,
            cancellationToken);
        if (usedSeats >= plan.IncludedStudentSeats.GetValueOrDefault())
        {
            return false;
        }

        if (seat is null)
        {
            db.CoachingSubscriptionSeats.Add(new CoachingSubscriptionSeat
            {
                SubscriptionId = subscription.Id,
                StudentId = studentId,
                AssignedBy = teacherId
            });
        }
        else
        {
            seat.IsSuspended = false;
            seat.SuspensionReason = null;
            seat.AssignedBy = teacherId;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (Npgsql.PostgresException exception) when (exception.SqlState == Npgsql.PostgresErrorCodes.SerializationFailure)
        {
            return false;
        }

        return true;
    }

    public async Task<bool> RemoveMyTeacherStudentSeatAsync(
        Guid teacherId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var active = await GetActiveTeacherSubscriptionAsync(teacherId, cancellationToken);
        if (active is null)
        {
            return false;
        }

        var seat = await db.CoachingSubscriptionSeats.SingleOrDefaultAsync(
            item => item.SubscriptionId == active.Value.Subscription.Id && item.StudentId == studentId,
            cancellationToken);
        if (seat is null)
        {
            return false;
        }

        seat.IsSuspended = true;
        seat.SuspensionReason = "Öğretmen abonelik koltuğunu kaldırdı.";
        seat.AssignedBy = teacherId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CoachingSubscriptionAccessSummary> GetMyAccessAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var rows = await (from subscription in db.CoachingSubscriptions.AsNoTracking()
                          join plan in db.CoachingSubscriptionPlans.AsNoTracking() on subscription.PlanId equals plan.Id
                          where subscription.Status == "Active"
                              && subscription.StartDate <= now
                              && subscription.EndDate >= now
                              && ((subscription.UserId == userId && plan.Audience == "Individual")
                                  || (subscription.InstitutionId.HasValue
                                      && db.CoachingSubscriptionSeats.Any(seat => seat.SubscriptionId == subscription.Id
                                          && seat.StudentId == userId && !seat.IsSuspended))
                                  || (plan.Audience == "Teacher"
                                      && subscription.UserId.HasValue
                                      && db.CoachingSubscriptionSeats.Any(seat => seat.SubscriptionId == subscription.Id
                                          && seat.StudentId == userId && !seat.IsSuspended)))
                          orderby subscription.EndDate descending
                          select new { subscription, plan })
            .ToListAsync(cancellationToken);
        var eligibleRows = new List<(CoachingSubscription Subscription, CoachingSubscriptionPlan Plan)>();
        foreach (var row in rows)
        {
            if (row.plan.Audience == "Teacher"
                && (!row.subscription.UserId.HasValue
                    || !await IsAssignedIndependentTeacherStudentAsync(row.subscription.UserId.Value, userId, cancellationToken)))
            {
                continue;
            }

            eligibleRows.Add((row.subscription, row.plan));
        }

        rows = eligibleRows.Select(row => new { subscription = row.Subscription, plan = row.Plan }).ToList();
        var seatCounts = await GetSeatCountsAsync(rows.Select(row => row.subscription.Id).ToArray(), cancellationToken);
        var subscriptions = rows.Select(row => ToSubscriptionSummary(
            row.subscription, row.plan, seatCounts.GetValueOrDefault(row.subscription.Id))).ToArray();
        var hasActive = subscriptions.Length > 0;
        return new CoachingSubscriptionAccessSummary(
            !settings.RequireActiveSubscription || hasActive,
            settings.RequireActiveSubscription,
            subscriptions.FirstOrDefault()?.Plan.Name,
            subscriptions.FirstOrDefault()?.Status,
            subscriptions.Select(item => (DateTime?)item.EndDate).Max(),
            subscriptions);
    }

    public async Task<PagedResponse<CoachingPaymentSummary>> GetPaymentsAsync(
        string? search,
        string? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(pageNumber, pageSize);
        var query = db.CoachingPaymentRecords.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(payment => payment.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(payment => payment.UserName.Contains(term)
                || payment.UserEmail.Contains(term)
                || payment.PlanName.Contains(term)
                || (payment.Reference != null && payment.Reference.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var payments = await query.OrderByDescending(payment => payment.CreatedAt)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = payments.Select(payment => new CoachingPaymentSummary(
            payment.Id,
            payment.UserId,
            payment.UserName,
            payment.UserEmail,
            payment.PlanName,
            payment.Amount,
            payment.Currency,
            payment.Status,
            payment.Provider,
            payment.Reference,
            payment.CreatedAt)).ToArray();
        return new PagedResponse<CoachingPaymentSummary>(items, pageNumber, pageSize, total);
    }

    private async Task<bool> IsIndependentTeacherAsync(Guid teacherId, CancellationToken cancellationToken)
    {
        if (teacherId == Guid.Empty)
        {
            return false;
        }

        try
        {
            return await identityAuthorizationClient.AuthorizeTeacherTargetsAsync(
                teacherId, Array.Empty<Guid>(), null, false, cancellationToken) is null;
        }
        catch (BusinessRuleException)
        {
            return false;
        }
    }

    private async Task<bool> IsAssignedIndependentTeacherStudentAsync(
        Guid teacherId,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await identityAuthorizationClient.AuthorizeTeacherTargetsAsync(
                teacherId, [studentId], null, false, cancellationToken) is null;
        }
        catch (BusinessRuleException)
        {
            return false;
        }
    }

    private async Task<bool> HasOverlappingTeacherSubscriptionAsync(
        Guid teacherId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken,
        Guid? excludingSubscriptionId = null) =>
        await (from subscription in db.CoachingSubscriptions.AsNoTracking()
               join plan in db.CoachingSubscriptionPlans.AsNoTracking() on subscription.PlanId equals plan.Id
               where subscription.UserId == teacherId
                   && (!excludingSubscriptionId.HasValue || subscription.Id != excludingSubscriptionId.Value)
                   && subscription.Status == "Active"
                   && plan.Audience == "Teacher"
                   && subscription.StartDate <= endDate
                   && subscription.EndDate >= startDate
               select subscription.Id).AnyAsync(cancellationToken);

    private async Task<(CoachingSubscription Subscription, CoachingSubscriptionPlan Plan)?> GetActiveTeacherSubscriptionAsync(
        Guid teacherId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var row = await (from subscription in db.CoachingSubscriptions.AsNoTracking()
                         join plan in db.CoachingSubscriptionPlans.AsNoTracking() on subscription.PlanId equals plan.Id
                         where subscription.UserId == teacherId
                             && subscription.Status == "Active"
                             && subscription.StartDate <= now
                             && subscription.EndDate >= now
                             && plan.Audience == "Teacher"
                         orderby subscription.EndDate descending
                         select new { subscription, plan })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.subscription, row.plan);
    }

    private async Task<CoachingSubscriptionSummary?> GetSubscriptionAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await (from subscription in db.CoachingSubscriptions.AsNoTracking()
                         join plan in db.CoachingSubscriptionPlans.AsNoTracking() on subscription.PlanId equals plan.Id
                         where subscription.Id == id
                         select new { subscription, plan }).SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var seatCounts = await GetSeatCountsAsync([id], cancellationToken);
        return ToSubscriptionSummary(row.subscription, row.plan, seatCounts.GetValueOrDefault(id));
    }

    private async Task<CoachingBankTransferRequestSummary?> GetBankTransferSummaryAsync(
        CoachingBankTransferRequest request,
        CancellationToken cancellationToken)
    {
        var plan = await db.CoachingSubscriptionPlans.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.PlanId, cancellationToken);
        return plan is null ? null : ToTransferSummary(request, plan);
    }

    private async Task<CoachingBankTransferRequestSummary?> GetBankTransferSummaryAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var request = await db.CoachingBankTransferRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return request is null ? null : await GetBankTransferSummaryAsync(request, cancellationToken);
    }

    private async Task<CoachingBankTransferRequestSummary?> GetBankTransferSummaryAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var request = await db.CoachingBankTransferRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        return request is null ? null : await GetBankTransferSummaryAsync(request, cancellationToken);
    }

    private Task<IdempotencyRecord?> GetIdempotencyRecordAsync(
        string scope,
        string key,
        CancellationToken cancellationToken) =>
        db.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken);

    private static string ValidateIdempotencyKey(Guid actorId, string? idempotencyKey)
    {
        var key = idempotencyKey?.Trim();
        if (actorId == Guid.Empty || key is null || key.Length is < 16 or > 128
            || key.Any(character => !(character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '.' or '_' or '-' or '~')))
        {
            throw new ArgumentException("Idempotency-Key 16-128 güvenli karakterden oluşmalıdır.", nameof(idempotencyKey));
        }

        return key;
    }

    private static string CreateRequestHash(params string?[] values)
    {
        var canonical = new StringBuilder();
        foreach (var value in values)
        {
            if (value is null)
            {
                canonical.Append("-1:");
                continue;
            }

            canonical.Append(value.Length).Append(':').Append(value);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void EnsureIdempotencyMatches(IdempotencyRecord record, string requestHash)
    {
        if (!record.Matches(requestHash))
        {
            throw new BusinessRuleException(
                "Idempotency.Conflict",
                "Aynı Idempotency-Key farklı bir istek gövdesiyle tekrar kullanılamaz.");
        }
    }

    private static string? NormalizePaymentReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var normalized = new StringBuilder();
        foreach (var character in reference.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_')
            {
                normalized.Append(character);
            }
            else if (character is '/' or ' ')
            {
                normalized.Append('-');
            }
            else
            {
                return null;
            }
        }

        var value = normalized.ToString().Trim('-');
        return value.Length is >= 6 and <= 100 ? value : null;
    }

    private static bool IsInstitutionPaymentReferenceConflict(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is Npgsql.PostgresException postgresException
                && string.Equals(postgresException.ConstraintName, "IX_subscriptions_InstitutionId_PaymentReference", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBankTransferReferenceConflict(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is Npgsql.PostgresException postgresException
                && string.Equals(postgresException.ConstraintName, "IX_bank_transfer_requests_UserId_PaymentReference", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSerializationFailure(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is Npgsql.PostgresException postgresException
                && postgresException.SqlState == Npgsql.PostgresErrorCodes.SerializationFailure)
            {
                return true;
            }
        }

        return false;
    }

    private Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var attempt = 0;
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Reload state after a rolled-back attempt instead of saving stale tracked entities.
            if (attempt++ > 0)
            {
                db.ChangeTracker.Clear();
            }
            return await operation();
        });
    }

    private async Task<Dictionary<Guid, (int Total, int Active)>> GetSeatCountsAsync(
        Guid[] subscriptionIds,
        CancellationToken cancellationToken)
    {
        if (subscriptionIds.Length == 0)
        {
            return [];
        }

        var parameter = Expression.Parameter(typeof(CoachingSubscriptionSeat), "seat");
        var property = Expression.Property(parameter, nameof(CoachingSubscriptionSeat.SubscriptionId));
        Expression predicate = Expression.Constant(false);
        foreach (var subscriptionId in subscriptionIds)
        {
            predicate = Expression.OrElse(
                predicate,
                Expression.Equal(property, Expression.Constant(subscriptionId)));
        }

        var filter = Expression.Lambda<Func<CoachingSubscriptionSeat, bool>>(predicate, parameter);
        return await db.CoachingSubscriptionSeats.AsNoTracking()
            .Where(filter)
            .GroupBy(seat => seat.SubscriptionId)
            .Select(group => new
            {
                SubscriptionId = group.Key,
                Total = group.Count(),
                Active = group.Count(seat => !seat.IsSuspended)
            })
            .ToDictionaryAsync(row => row.SubscriptionId, row => (row.Total, row.Active), cancellationToken);
    }

    private static CoachingSubscriptionPlanRequest NormalizePlan(CoachingSubscriptionPlanRequest request)
    {
        var name = Required(request.Name, "Plan adı zorunludur.", 150);
        var slug = CoachingManagementRules.NormalizeSlug(string.IsNullOrWhiteSpace(request.Slug) ? name : request.Slug);
        var description = request.Description?.Trim() ?? string.Empty;
        if (slug.Length is < 1 or > 80 || description.Length > 1000 || request.SortOrder is < 0 or > 10_000)
        {
            throw new ArgumentException("Plan bilgileri geçersiz veya sınırların dışında.");
        }

        var features = (request.Features ?? Array.Empty<string>())
            .Where(feature => !string.IsNullOrWhiteSpace(feature))
            .Select(feature => feature.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .ToArray();
        if (features.Any(feature => feature.Length > 200))
        {
            throw new ArgumentException("Plan özellikleri en fazla 200 karakter olabilir.");
        }

        return request with
        {
            Slug = slug,
            Name = name,
            Description = description,
            Features = features,
            BillingPeriod = request.BillingPeriod.Trim(),
            Audience = request.Audience.Trim()
        };
    }

    private static string Required(string? value, string message, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
        {
            throw new ArgumentException(message);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Bu alan en fazla {maxLength} karakter olabilir.");
        }

        return normalized;
    }

    private static void ValidatePaging(int pageNumber, int pageSize)
    {
        if (pageNumber is < 1 or > 1_000 || pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Sayfalama aralık dışı.");
        }
    }

    private static CoachingSubscriptionPlanSummary ToSummary(CoachingSubscriptionPlan plan) => new(
        plan.Id,
        plan.Slug,
        plan.Name,
        plan.Description,
        plan.Audience,
        plan.Price,
        plan.IsContactOnly,
        plan.BillingPeriod,
        plan.DurationDays,
        plan.IncludedStudentSeats,
        JsonSerializer.Deserialize<string[]>(plan.FeaturesJson, JsonOptions) ?? Array.Empty<string>(),
        plan.IsActive,
        plan.IsPublic,
        plan.SortOrder);

    private static CoachingSubscriptionSettingsSummary ToSettingsSummary(CoachingSubscriptionSettings settings)
    {
        var isPublic = settings.BankTransferEnabled
            && !string.IsNullOrWhiteSpace(settings.AccountHolder)
            && !string.IsNullOrWhiteSpace(settings.BankName)
            && CoachingManagementRules.IsValidIban(settings.Iban);
        return new CoachingSubscriptionSettingsSummary(
            settings.RequireActiveSubscription,
            settings.Currency,
            settings.AccountHolder,
            settings.BankName,
            isPublic ? CoachingManagementRules.NormalizeIban(settings.Iban) : settings.Iban,
            settings.PaymentInstructions,
            settings.BankTransferEnabled,
            isPublic,
            settings.UpdatedAt);
    }

    private static CoachingBankTransferRequestSummary ToTransferSummary(
        CoachingBankTransferRequest request,
        CoachingSubscriptionPlan plan) => new(
            request.Id,
            request.UserId,
            request.UserName,
            request.UserEmail,
            ToSummary(plan),
            request.Amount,
            request.Currency,
            request.PaymentReference,
            request.PayerName,
            request.Note,
            request.Status,
            request.SubscriptionId,
            request.ReviewedBy,
            request.ReviewedAt,
            request.ReviewNote,
            request.CreatedAt,
            request.AdultPayerDeclarationVersion,
            request.AdultPayerDeclaredAt);

    private static CoachingSubscriptionSummary ToSubscriptionSummary(
        CoachingSubscription subscription,
        CoachingSubscriptionPlan plan,
        (int Total, int Active) seatCounts) => new(
            subscription.Id,
            subscription.PlanId,
            ToSummary(plan),
            subscription.UserId,
            subscription.UserName,
            subscription.UserEmail,
            subscription.InstitutionId,
            subscription.Status,
            subscription.StartDate,
            subscription.EndDate,
            seatCounts.Total,
            seatCounts.Active,
            subscription.Notes,
            subscription.CreatedAt,
            subscription.PaymentReference);
}
