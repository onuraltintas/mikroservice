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
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Management;

public sealed class CoachingSubscriptionService(
    CoachingDbContext db,
    ICoachingIdentityAuthorizationClient identityAuthorizationClient,
    ICoachingEventPublisher? events = null) : ICoachingSubscription
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
            TrialDays = normalized.TrialDays,
            ExtraSeatPrice = normalized.ExtraSeatPrice,
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
        plan.TrialDays = normalized.TrialDays;
        plan.ExtraSeatPrice = normalized.ExtraSeatPrice;
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
        string expectedAudience, string idempotencyKey, CancellationToken cancellationToken, Guid? submittedBy = null, string? submitterName = null) =>
        ExecuteWithRetryAsync(() => CreateBankTransferRequestCoreAsync(
            userId, userName, userEmail, request, expectedAudience, idempotencyKey, cancellationToken, submittedBy, submitterName), cancellationToken);

    // Roadmap Y7: a verified parent (Identity decides) acts for the child; the subscription belongs to the child.
    private async Task<bool> IsLinkedParentAsync(Guid parentId, Guid studentId, CancellationToken cancellationToken) =>
        parentId != Guid.Empty && studentId != Guid.Empty && parentId != studentId
        && (await identityAuthorizationClient.AuthorizeStudentReadAsync(parentId, [studentId], cancellationToken)).Contains(studentId);

    public async Task<CoachingSubscriptionAccessSummary?> GetChildAccessAsync(Guid parentId, Guid studentId, CancellationToken cancellationToken = default) =>
        await IsLinkedParentAsync(parentId, studentId, cancellationToken) ? await GetMyAccessAsync(studentId, cancellationToken) : null;

    public async Task<IReadOnlyList<CoachingBankTransferRequestSummary>?> GetChildBankTransferRequestsAsync(Guid parentId, Guid studentId,
        CancellationToken cancellationToken = default) =>
        await IsLinkedParentAsync(parentId, studentId, cancellationToken) ? await GetMyBankTransferRequestsAsync(studentId, cancellationToken) : null;

    public async Task<CoachingBankTransferRequestSummary?> CreateChildBankTransferRequestAsync(Guid parentId, string? parentName, string? parentEmail,
        Guid studentId, string? studentName, string? studentEmail, CoachingBankTransferRequestCreate request, string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!await IsLinkedParentAsync(parentId, studentId, cancellationToken)) return null;
        return await CreateBankTransferRequestAsync(studentId, studentName, studentEmail, request, "Individual", idempotencyKey,
            cancellationToken, parentId, NormalizeOptional(parentName, 200));
    }

    private async Task<CoachingBankTransferRequestSummary?> CreateBankTransferRequestCoreAsync(
        Guid userId,
        string? userName,
        string? userEmail,
        CoachingBankTransferRequestCreate request,
        string expectedAudience,
        string idempotencyKey,
        CancellationToken cancellationToken,
        Guid? submittedBy = null,
        string? submitterName = null)
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
            reference, payerName, note, "adult-payer-v1", submittedBy?.ToString("D"),
            string.IsNullOrWhiteSpace(request.CouponCode) ? null : request.CouponCode.Trim().ToUpperInvariant(),
            request.AcceptSalesContract ? CoachingSalesDocumentTexts.Version : null);
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

        // While a teacher plan runs, only the same plan (renewal) or a larger one (upgrade) can be bought: a smaller
        // plan would take seats away from students who hold them.
        if (expectedAudience == "Teacher"
            && await GetActiveTeacherSubscriptionAsync(userId, cancellationToken) is { } running
            && running.Plan.Id != plan.Id
            && plan.IncludedStudentSeats.GetValueOrDefault() <= running.Plan.IncludedStudentSeats.GetValueOrDefault())
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

        // Roadmap O10: a discount code lowers the price frozen on the notice; the documents are kept as the buyer saw them.
        var quote = string.IsNullOrWhiteSpace(request.CouponCode) ? null
            : (await CoachingCoupons.QuoteAsync(db, request.CouponCode, plan, userId, DateTime.UtcNow, cancellationToken)).Quote;
        var amount = quote?.Amount ?? plan.Price;
        string? documentsJson = null;
        if (request.AcceptSalesContract)
        {
            var sellerSettings = await db.CoachingSubscriptionSettings.AsNoTracking().SingleAsync(item => item.Id == 1, cancellationToken);
            documentsJson = JsonSerializer.Serialize(CoachingSalesDocumentTexts.Render(new CoachingSalesTerms(sellerSettings,
                NormalizeOptional(submitterName ?? userName, 200) ?? "Koçluk kullanıcısı", submittedBy is null ? userEmail : null, plan.Name,
                plan.DurationDays, plan.Price, quote?.Discount ?? 0, amount, settings.Currency, quote?.Code, DateTime.UtcNow,
                settings.PaymentInstructions ?? string.Empty)), JsonOptions);
        }

        // The same receipt number from another payer is either a typo or a receipt used twice; the admin checks it.
        var usedByOthers = await db.CoachingBankTransferRequests.AsNoTracking()
            .AnyAsync(item => item.PaymentReference == reference && item.UserId != userId, cancellationToken);
        var requestEntity = new CoachingBankTransferRequest
        {
            UserId = userId,
            UserName = NormalizeOptional(userName, 200) ?? "Koçluk kullanıcısı",
            UserEmail = NormalizeOptional(userEmail, 320) ?? string.Empty,
            PlanId = plan.Id,
            Amount = amount,
            ListPrice = plan.Price,
            CouponCode = quote?.Code,
            DiscountAmount = quote?.Discount ?? 0,
            SalesContractVersion = documentsJson is null ? null : CoachingSalesDocumentTexts.Version,
            SalesContractAcceptedAt = documentsJson is null ? null : DateTime.UtcNow,
            SalesDocumentsJson = documentsJson,
            Currency = settings.Currency,
            DurationDays = plan.DurationDays,
            SubmittedByUserId = submittedBy,
            PaymentReference = reference,
            PayerName = payerName ?? submitterName,
            Note = note,
            AdultPayerDeclarationVersion = 1,
            AdultPayerDeclaredAt = DateTime.UtcNow
        };
        db.CoachingBankTransferRequests.Add(requestEntity);
        await NoticeAsync(null, null, CoachingSubscriptionNoticeKinds.TransferSubmitted, plan.Audience, plan.Name, null,
            $"{requestEntity.UserName} · {reference} · {requestEntity.Amount:0.00} {requestEntity.Currency}"
            + (usedByOthers ? " · Uyarı: bu dekont numarası başka bir kullanıcının bildiriminde de var." : string.Empty), cancellationToken);
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

    /// <summary>The payer fixes a typo in a notice that nobody has reviewed yet.</summary>
    public async Task<CoachingBankTransferRequestSummary?> UpdateMyBankTransferRequestAsync(Guid userId, Guid id,
        CoachingBankTransferRequestUpdate request, CancellationToken cancellationToken = default)
    {
        var transfer = await db.CoachingBankTransferRequests.SingleOrDefaultAsync(item => item.Id == id
            && (item.UserId == userId || item.SubmittedByUserId == userId), cancellationToken);
        if (transfer is null) return null;
        RequirePending(transfer);
        var reference = NormalizePaymentReference(request.PaymentReference)
            ?? throw new ArgumentException("Banka işlem referansı 6-100 harf, rakam veya tire içermelidir.");
        if (await db.CoachingBankTransferRequests.AnyAsync(item => item.UserId == transfer.UserId && item.Id != id
                && item.PaymentReference == reference, cancellationToken))
            throw new BusinessRuleException("Subscription.TransferReferenceUsed", "Bu işlem referansıyla daha önce ödeme bildirimi gönderdin.");
        transfer.PaymentReference = reference;
        transfer.PayerName = NormalizeOptional(request.PayerName, 200);
        transfer.Note = NormalizeOptional(request.Note, 1000);
        transfer.UpdatedAt = DateTime.UtcNow;
        transfer.Version++;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsBankTransferReferenceConflict(exception))
        {
            throw new BusinessRuleException("Subscription.TransferReferenceUsed", "Bu işlem referansıyla daha önce ödeme bildirimi gönderdin.");
        }
        return await GetBankTransferSummaryAsync(transfer, cancellationToken);
    }

    /// <summary>The payer takes back a notice sent by mistake; a new notice can then be sent for the same plan.</summary>
    public async Task<CoachingBankTransferRequestSummary?> WithdrawMyBankTransferRequestAsync(Guid userId, Guid id,
        CancellationToken cancellationToken = default)
    {
        var transfer = await db.CoachingBankTransferRequests.SingleOrDefaultAsync(item => item.Id == id
            && (item.UserId == userId || item.SubmittedByUserId == userId), cancellationToken);
        if (transfer is null) return null;
        if (transfer.Status == "Withdrawn") return await GetBankTransferSummaryAsync(transfer, cancellationToken);
        RequirePending(transfer);
        transfer.Status = "Withdrawn";
        transfer.UpdatedAt = DateTime.UtcNow;
        transfer.Version++;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("Subscription.TransferNotPending", "Ödeme bildirimi bu sırada incelendi; güncel durumu kontrol et.");
        }
        return await GetBankTransferSummaryAsync(transfer, cancellationToken);
    }

    private static void RequirePending(CoachingBankTransferRequest transfer)
    {
        if (transfer.Status != "Pending")
            throw new BusinessRuleException("Subscription.TransferNotPending", "Yalnız incelemeyi bekleyen ödeme bildirimi değiştirilebilir veya geri çekilebilir.");
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
        var references = rows.Select(row => row.request.PaymentReference).Distinct().ToArray();
        var senders = (await db.CoachingBankTransferRequests.AsNoTracking()
                .Where(item => Enumerable.Contains(references, item.PaymentReference))
                .Select(item => new { item.PaymentReference, item.UserId }).ToListAsync(cancellationToken))
            .GroupBy(item => item.PaymentReference)
            .ToDictionary(group => group.Key, group => group.Select(item => item.UserId).Distinct().ToArray());
        return new PagedResponse<CoachingBankTransferRequestSummary>(
            rows.Select(row => ToTransferSummary(row.request, row.plan,
                senders.GetValueOrDefault(row.request.PaymentReference)?.Count(user => user != row.request.UserId) ?? 0)).ToArray(),
            pageNumber, pageSize, total);
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
        var upgrade = false;
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
                        // The lifecycle job marks a lapsed subscription Expired; renewing it keeps its seats.
                        && (item.Status == "Active" || item.Status == "Expired")
                        && item.StartDate <= now)
                    .OrderByDescending(item => item.EndDate)
                    .FirstOrDefaultAsync(cancellationToken);
                if (renewalSubscription is null)
                {
                    // An upgrade: the running plan switches to the larger one now, keeps its seats and paid days, and
                    // the new period is added after them.
                    renewalSubscription = await (from item in db.CoachingSubscriptions
                                                 join plan in db.CoachingSubscriptionPlans on item.PlanId equals plan.Id
                                                 where item.UserId == transfer.UserId && item.Status == "Active" && plan.Audience == "Teacher"
                                                     && item.StartDate <= now && item.EndDate > now
                                                     && (plan.IncludedStudentSeats ?? 0) < (approvedPlan.IncludedStudentSeats ?? 0)
                                                 orderby item.EndDate descending
                                                 select item).FirstOrDefaultAsync(cancellationToken);
                    upgrade = renewalSubscription is not null;
                }
                subscriptionStartDate = renewalSubscription is not null && renewalSubscription.EndDate > now
                    ? renewalSubscription.EndDate
                    : now;
            }
            else
            {
                // An individual who pays before the current period ends keeps every paid day: the new period
                // starts when the last active individual period ends. The same plan is extended in place;
                // a different plan starts as its own subscription at that moment.
                var latest = await (from item in db.CoachingSubscriptions
                                    join plan in db.CoachingSubscriptionPlans on item.PlanId equals plan.Id
                                    where item.UserId == transfer.UserId && item.Status == "Active"
                                        && plan.Audience == "Individual" && item.EndDate > now
                                    orderby item.EndDate descending
                                    select item).FirstOrDefaultAsync(cancellationToken);
                subscriptionStartDate = latest?.EndDate ?? now;
                renewalSubscription = latest?.PlanId == approvedPlan.Id ? latest : null;
            }
            // The length the payer saw when sending the notice, not the plan's current setting.
            subscriptionEndDate = subscriptionStartDate.Value.AddDays(transfer.DurationDays ?? approvedPlan.DurationDays);
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
                if (upgrade) subscription.PlanId = plan.Id;
                subscription.UpdatedBy = actorId;
                subscription.UpdatedAt = DateTime.UtcNow;
            }

            db.CoachingSubscriptionPeriods.Add(new CoachingSubscriptionPeriod
            {
                SubscriptionId = subscription.Id,
                StartDate = subscriptionStartDate!.Value,
                EndDate = subscriptionEndDate!.Value,
                DurationDays = transfer.DurationDays ?? plan.DurationDays,
                Source = "BankTransfer",
                BankTransferRequestId = transfer.Id,
                CreatedBy = actorId
            });
            transfer.SubscriptionId = subscription.Id;
            await NoticeAsync(transfer.UserId, transfer.UserEmail, CoachingSubscriptionNoticeKinds.TransferApproved, plan.Audience,
                plan.Name, subscription.EndDate, null, cancellationToken);
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
            // Roadmap O10: a code counts as used once a payment with it is approved.
            if (transfer.CouponCode is { } code
                && await db.Set<CoachingCoupon>().SingleOrDefaultAsync(item => item.Code == code, cancellationToken) is { } coupon)
                coupon.UsedCount++;
        }

        else if (await db.CoachingSubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(item => item.Id == transfer.PlanId, cancellationToken)
            is { } rejectedPlan)
        {
            await NoticeAsync(transfer.UserId, transfer.UserEmail, CoachingSubscriptionNoticeKinds.TransferRejected, rejectedPlan.Audience,
                rejectedPlan.Name, null, reviewNote, cancellationToken);
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

    public async Task<CoachingBankTransferRequestSummary?> CancelApprovedBankTransferAsync(Guid id, string reason, Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var note = (reason ?? string.Empty).Trim();
        if (note.Length is < 5 or > 1000) throw new ArgumentException("İptal gerekçesi 5-1000 karakter olmalıdır.");
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var transfer = await db.CoachingBankTransferRequests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (transfer is null || transfer.Status != "Approved") return null;
        var plan = await db.CoachingSubscriptionPlans.SingleOrDefaultAsync(item => item.Id == transfer.PlanId, cancellationToken);
        var now = DateTime.UtcNow;

        if (transfer.SubscriptionId is { } subscriptionId
            && await db.CoachingSubscriptions.SingleOrDefaultAsync(item => item.Id == subscriptionId, cancellationToken) is { } subscription)
        {
            var period = await db.CoachingSubscriptionPeriods.SingleOrDefaultAsync(
                item => item.BankTransferRequestId == transfer.Id && item.Status == "Active", cancellationToken);
            if (period is not null) await CancelPeriodAsync(subscription, period, now, cancellationToken);
            else
            {
                // Approved before paid periods were recorded. The subscription this payment created ends now unless
                // later payments renewed it; those keep their time and the subscription loses this payment's days.
                var renewedByOthers = await db.CoachingBankTransferRequests.AnyAsync(item => item.SubscriptionId == subscription.Id
                    && item.Id != transfer.Id && item.Status == "Approved", cancellationToken);
                var days = transfer.DurationDays ?? plan?.DurationDays;
                if ((subscription.BankTransferRequestId == transfer.Id && !renewedByOthers) || days is null)
                {
                    subscription.Status = "Cancelled";
                    subscription.EndDate = now > subscription.StartDate ? now : subscription.StartDate;
                }
                else
                {
                    var shortened = subscription.EndDate.AddDays(-days.Value);
                    subscription.EndDate = shortened > subscription.StartDate ? shortened : subscription.StartDate;
                    if (subscription.EndDate <= now) subscription.Status = "Cancelled";
                }
            }
            subscription.UpdatedBy = actorId;
            subscription.UpdatedAt = now;
        }

        foreach (var payment in await db.CoachingPaymentRecords.Where(item => item.BankTransferRequestId == transfer.Id).ToListAsync(cancellationToken))
            payment.Status = "Cancelled";

        if (plan is not null)
        {
            await NoticeAsync(transfer.UserId, transfer.UserEmail, CoachingSubscriptionNoticeKinds.ApprovalCancelled, plan.Audience,
                plan.Name, null, note, cancellationToken);
        }

        transfer.Status = "Cancelled";
        transfer.ReviewNote = string.IsNullOrWhiteSpace(transfer.ReviewNote)
            ? $"İptal ({now:yyyy-MM-dd HH:mm} UTC, {actorId}): {note}"
            : $"{transfer.ReviewNote}\nİptal ({now:yyyy-MM-dd HH:mm} UTC, {actorId}): {note}";
        transfer.UpdatedAt = now;
        transfer.Version++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsSerializationFailure(exception))
        {
            return null;
        }
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await GetBankTransferSummaryAsync(transfer, cancellationToken);
    }

    /// <summary>
    /// Removes the unused part of a cancelled payment's period. Periods paid after it move forward by that much, so a
    /// renewal keeps its full length; the subscription ends when nothing paid is left.
    /// </summary>
    private async Task CancelPeriodAsync(CoachingSubscription subscription, CoachingSubscriptionPeriod period, DateTime now,
        CancellationToken cancellationToken)
    {
        var originalEnd = period.EndDate;
        var usedUntil = now < period.StartDate ? period.StartDate : now > originalEnd ? originalEnd : now;
        var removed = originalEnd - usedUntil;
        period.EndDate = usedUntil;
        period.Status = "Cancelled";
        period.CancelledAt = now;
        if (removed > TimeSpan.Zero)
        {
            foreach (var later in await db.CoachingSubscriptionPeriods.Where(item => item.SubscriptionId == subscription.Id
                && item.Status == "Active" && item.Id != period.Id && item.StartDate >= originalEnd).ToListAsync(cancellationToken))
            {
                later.StartDate -= removed;
                later.EndDate -= removed;
            }
            if (subscription.EndDate >= originalEnd) subscription.EndDate -= removed;
        }
        if (subscription.EndDate < subscription.StartDate) subscription.EndDate = subscription.StartDate;
        if (subscription.EndDate <= now || subscription.EndDate == subscription.StartDate)
        {
            subscription.Status = "Cancelled";
            if (subscription.EndDate > now) subscription.EndDate = subscription.StartDate > now ? subscription.StartDate : now;
        }
    }

    public async Task<bool> DeleteBankTransferRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transfer = await db.CoachingBankTransferRequests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (transfer is null)
        {
            return false;
        }

        // An approved transfer is the record of a received payment and of the access it granted. Deleting it
        // would free its payment reference for a second approval and unlink the payment and subscription.
        // A cancelled approval is the trail of a reversed payment and is kept the same way.
        if (transfer.Status is "Approved" or "Cancelled")
        {
            throw new BusinessRuleException(
                "Subscription.TransferApproved",
                "Onaylanmış veya iptal edilmiş banka transferi silinemez. Ödeme ve abonelik kaydı korunur.");
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
        if (request.Amount is < 0 or > 10_000_000) return null;
        // A renewal without a student list keeps the students who hold a seat today and are still members.
        if (institutionOwned && studentIds.Length == 0)
            studentIds = await CarriedSeatsAsync(request.InstitutionId!.Value, plan.IncludedStudentSeats.GetValueOrDefault(), cancellationToken);
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
        // A license granted by hand is still a sale (or a gift worth recording): it gets its paid period and payment.
        db.CoachingSubscriptionPeriods.Add(new CoachingSubscriptionPeriod
        {
            SubscriptionId = subscription.Id, StartDate = startDate, EndDate = endDate, DurationDays = plan.DurationDays,
            Source = "Manual", CreatedBy = actorId
        });
        db.CoachingPaymentRecords.Add(new CoachingPaymentRecord
        {
            UserId = request.UserId,
            InstitutionId = request.InstitutionId,
            UserName = subscription.UserName ?? (institutionOwned ? "Kurum lisansı" : "Koçluk kullanıcısı"),
            UserEmail = subscription.UserEmail ?? string.Empty,
            PlanId = plan.Id,
            PlanName = plan.Name,
            SubscriptionId = subscription.Id,
            Amount = request.Amount ?? plan.Price,
            Currency = (await GetSettingsAsync(cancellationToken)).Currency,
            Provider = "Manual",
            Reference = paymentReference
        });
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

    private async Task<Guid[]> CarriedSeatsAsync(Guid institutionId, int capacity, CancellationToken cancellationToken)
    {
        var previous = await db.CoachingSubscriptions.AsNoTracking()
            .Where(item => item.InstitutionId == institutionId && (item.Status == "Active" || item.Status == "Expired"))
            .OrderByDescending(item => item.EndDate).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
        if (previous is null || capacity <= 0) return [];
        var seated = await db.CoachingSubscriptionSeats.AsNoTracking()
            .Where(seat => seat.SubscriptionId == previous && !seat.IsSuspended)
            .OrderBy(seat => seat.AssignedAt).Select(seat => seat.StudentId).ToListAsync(cancellationToken);
        var carried = new List<Guid>();
        foreach (var student in seated)
        {
            if (carried.Count >= capacity) break;
            if (await identityAuthorizationClient.IsActiveInstitutionStudentAsync(institutionId, student, cancellationToken)) carried.Add(student);
        }
        return carried.ToArray();
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

        // Reopening a seat must fit the plan, like every other way of giving one.
        if (!request.IsSuspended && seat.IsSuspended)
        {
            var capacity = await db.CoachingSubscriptionPlans.AsNoTracking().Where(plan => plan.Id == subscription.PlanId)
                .Select(plan => plan.IncludedStudentSeats).SingleOrDefaultAsync(cancellationToken);
            if (await db.CoachingSubscriptionSeats.CountAsync(item => item.SubscriptionId == subscriptionId && !item.IsSuspended, cancellationToken)
                >= capacity.GetValueOrDefault() + subscription.ExtraSeats)
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
            if (row.subscription.InstitutionId is { } institutionId
                && !await identityAuthorizationClient.IsActiveInstitutionStudentAsync(institutionId, userId, cancellationToken))
                continue;
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
            payment.CreatedAt,
            payment.InstitutionId)).ToArray();
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

    // Published before SaveChanges so the bus outbox stores the notice in the same transaction as the change.
    private Task NoticeAsync(Guid? userId, string? email, string kind, string audience, string planName, DateTime? endsAt,
        string? detail, CancellationToken cancellationToken) =>
        events is null
            ? Task.CompletedTask
            : events.PublishAsync(new CoachingSubscriptionNoticeV1(Guid.NewGuid(), userId, string.IsNullOrWhiteSpace(email) ? null : email,
                kind, audience, planName, endsAt, detail, DateTime.UtcNow), cancellationToken);

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

        // Roadmap O10: a free trial is offered per plan, once per account; institutions are licensed, not trialled.
        if (request.TrialDays is < 0 or > 90 || request.TrialDays > 0 && request.Audience.Trim() == "Institution")
        {
            throw new ArgumentException("Deneme süresi 0–90 gün olabilir; kurum planlarında deneme süresi olmaz.");
        }

        // Roadmap O10b: institutions can buy extra seats mid-license at this yearly per-seat price.
        if (request.ExtraSeatPrice is { } extraSeatPrice
            && (extraSeatPrice <= 0 || extraSeatPrice > 1_000_000 || request.Audience.Trim() != "Institution"))
        {
            throw new ArgumentException("Ek koltuk fiyatı yalnız kurum planlarında, 0'dan büyük ve en çok 1.000.000 olabilir.");
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
        plan.SortOrder,
        plan.TrialDays,
        plan.ExtraSeatPrice);

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
        CoachingSubscriptionPlan plan,
        int duplicateReferenceCount = 0) => new(
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
            request.AdultPayerDeclaredAt,
            request.DurationDays ?? plan.DurationDays,
            duplicateReferenceCount,
            request.SubmittedByUserId,
            request.ListPrice,
            request.CouponCode,
            request.DiscountAmount,
            request.SalesContractVersion,
            request.WithdrawalStatus);

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
            subscription.PaymentReference,
            subscription.ExtraSeats);
}
