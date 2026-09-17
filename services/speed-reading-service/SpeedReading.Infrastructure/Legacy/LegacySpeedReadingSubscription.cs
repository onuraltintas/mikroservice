using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Contracts.Reporting;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Content;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Payments;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Infrastructure.Legacy;

public sealed class LegacySpeedReadingSubscription : ISpeedReadingSubscription
{
    private static readonly Guid BankTransferSettingsId = Guid.Parse("4c18b5a8-96e8-4af6-9195-0d4fdde3dcf2");
    private const string BankTransferSettingsScope = "speed-reading.bank-transfer.settings";
    private const string BankTransferRequestScope = "speed-reading.bank-transfer.request";
    private const string BankTransferReviewScope = "speed-reading.bank-transfer.review";

    private readonly ISpeedReadingDataContext db;
    private readonly OwnedSpeedReadingDbContext ownedDb;
    private readonly ISpeedReadingPaymentProvider paymentProvider;
    private readonly IyzicoOptions iyzicoOptions;
    private readonly ISpeedReadingUserDirectory userDirectory;

    internal LegacySpeedReadingSubscription(
        ISpeedReadingDataContext db,
        OwnedSpeedReadingDbContext ownedDb,
        ISpeedReadingPaymentProvider paymentProvider,
        IyzicoOptions iyzicoOptions,
        ISpeedReadingUserDirectory userDirectory)
    {
        this.db = db;
        this.ownedDb = ownedDb;
        this.paymentProvider = paymentProvider;
        this.iyzicoOptions = iyzicoOptions;
        this.userDirectory = userDirectory;
    }

    public async Task<IReadOnlyList<ProductSummary>> GetProductsAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = db.Products.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(product => product.IsActive && product.IsPublic);
        }

        var products = await query.OrderBy(product => product.SortOrder).ToListAsync(cancellationToken);
        return products.Select(ToSummary).ToList();
    }

    public async Task<ProductSummary?> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return product is null ? null : ToSummary(product);
    }

    public async Task<Guid> CreateProductAsync(CreateProductRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var product = new LegacyProduct
        {
            Id = Guid.NewGuid(),
            Slug = request.Slug.Trim().ToLowerInvariant(),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            IncludedProductSlugsJson = Serialize(request.IncludedProductSlugs),
            IsActive = request.IsActive,
            IsPublic = request.IsPublic,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return product.Id;
    }

    public async Task<ProductSummary?> UpdateProductAsync(Guid id, UpdateProductRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return null;
        }

        if (request.Name is not null) product.Name = request.Name.Trim();
        if (request.Description is not null) product.Description = request.Description.Trim();
        if (request.IncludedProductSlugs is not null) product.IncludedProductSlugsJson = Serialize(request.IncludedProductSlugs);
        if (request.IsActive.HasValue) product.IsActive = request.IsActive.Value;
        if (request.IsPublic.HasValue) product.IsPublic = request.IsPublic.Value;
        if (request.SortOrder.HasValue) product.SortOrder = request.SortOrder.Value;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(product);
    }

    public async Task<bool> DeactivateProductAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return false;
        }

        var hasActivePlan = await db.SubscriptionPlans.AnyAsync(item => item.ProductId == id && item.IsActive, cancellationToken);
        if (hasActivePlan)
        {
            return false;
        }

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<SubscriptionPlanSummary>> GetPlansAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = from plan in db.SubscriptionPlans.AsNoTracking()
                    join product in db.Products.AsNoTracking() on plan.ProductId equals product.Id
                    where includeInactive || (plan.IsActive && plan.IsPublic && product.IsActive && product.IsPublic)
                    orderby plan.SortOrder
                    select new { plan, product };
        var rows = await query.ToListAsync(cancellationToken);
        return rows.Select(row => ToSummary(row.plan, row.product)).ToList();
    }

    public async Task<SubscriptionPlanSummary?> GetPlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await (from plan in db.SubscriptionPlans.AsNoTracking()
                         join product in db.Products.AsNoTracking() on plan.ProductId equals product.Id
                         where plan.Id == id
                            && plan.IsActive
                            && plan.IsPublic
                            && product.IsActive
                            && product.IsPublic
                         select new { plan, product }).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : ToSummary(row.plan, row.product);
    }

    public async Task<Guid?> CreatePlanAsync(CreateSubscriptionPlanRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var name = NormalizeRequired(request.Name, 200);
        var description = NormalizeRequired(request.Description, 1_000);
        var slug = NormalizePlanSlug(request.Slug);
        var product = await db.Products.SingleOrDefaultAsync(item => item.Id == request.ProductId, cancellationToken);
        if (name is null
            || description is null
            || slug is null
            || product is null
            || !BankTransferPaymentRules.IsValidPlanDefinition(request.Price, request.BillingPeriod, request.DurationDays, request.IsContactOnly)
            || (request.IsActive && request.IsPublic && !(product.IsActive && product.IsPublic))
            || await db.SubscriptionPlans.AnyAsync(item => item.Slug == slug, cancellationToken))
        {
            return null;
        }

        var plan = new LegacySubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Slug = slug,
            ProductId = request.ProductId,
            Price = request.Price,
            IsContactOnly = request.IsContactOnly,
            BillingPeriod = request.BillingPeriod.Trim(),
            DurationDays = request.DurationDays,
            IsActive = request.IsActive,
            IsPublic = request.IsPublic,
            SortOrder = request.SortOrder,
            Features = Serialize(request.Features),
            CreatedAt = DateTime.UtcNow
        };
        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        return plan.Id;
    }

    public async Task<SubscriptionPlanSummary?> UpdatePlanAsync(Guid id, UpdateSubscriptionPlanRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var plan = await db.SubscriptionPlans.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        var effectivePrice = request.Price ?? plan.Price;
        var effectiveIsContactOnly = request.IsContactOnly ?? plan.IsContactOnly;
        var effectiveBillingPeriod = request.BillingPeriod ?? plan.BillingPeriod;
        var effectiveDurationDays = request.DurationDays ?? plan.DurationDays;
        var effectiveIsActive = request.IsActive ?? plan.IsActive;
        var effectiveIsPublic = request.IsPublic ?? plan.IsPublic;
        var product = await db.Products.AsNoTracking().SingleAsync(item => item.Id == plan.ProductId, cancellationToken);
        var name = request.Name is null ? plan.Name : NormalizeRequired(request.Name, 200);
        var description = request.Description is null ? plan.Description : NormalizeRequired(request.Description, 1_000);
        if (name is null
            || description is null
            || !BankTransferPaymentRules.IsValidPlanDefinition(effectivePrice, effectiveBillingPeriod, effectiveDurationDays, effectiveIsContactOnly)
            || (effectiveIsActive && effectiveIsPublic && !(product.IsActive && product.IsPublic)))
        {
            return null;
        }

        plan.Name = name;
        plan.Description = description;
        if (request.Price.HasValue) plan.Price = request.Price.Value;
        if (request.IsContactOnly.HasValue) plan.IsContactOnly = request.IsContactOnly.Value;
        if (request.BillingPeriod is not null) plan.BillingPeriod = request.BillingPeriod.Trim();
        if (request.DurationDays.HasValue) plan.DurationDays = request.DurationDays.Value == 0 ? null : request.DurationDays.Value;
        if (request.IsActive.HasValue) plan.IsActive = request.IsActive.Value;
        if (request.IsPublic.HasValue) plan.IsPublic = request.IsPublic.Value;
        if (request.SortOrder.HasValue) plan.SortOrder = request.SortOrder.Value;
        if (request.Features is not null) plan.Features = Serialize(request.Features);
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return ToSummary(plan, product);
    }

    public async Task<bool> DeactivatePlanAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var plan = await db.SubscriptionPlans.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (plan is null)
        {
            return false;
        }

        plan.IsActive = false;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<BankTransferPaymentSettingsSummary?> GetPublicBankTransferSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await db.BankTransferPaymentSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == BankTransferSettingsId, cancellationToken);
        return settings is null || !BankTransferPaymentRules.HasCompletePublicSettings(
            settings.IsEnabled, settings.AccountHolder, settings.BankName, settings.Iban)
            ? null
            : ToBankTransferSettingsSummary(settings);
    }

    public async Task<BankTransferPaymentSettingsSummary?> GetBankTransferSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await db.BankTransferPaymentSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == BankTransferSettingsId, cancellationToken);
        return settings is null ? null : ToBankTransferSettingsSummary(settings);
    }

    public async Task<BankTransferPaymentSettingsSummary?> UpdateBankTransferSettingsAsync(
        UpdateBankTransferPaymentSettingsRequest request,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        OwnedContentMutationIdempotency.Validate(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        var requestHash = OwnedContentMutationIdempotency.CreateRequestHash(actorId, BankTransferSettingsScope, BankTransferSettingsId, request);
        var replay = await OwnedContentMutationIdempotency.GetAsync(ownedDb, BankTransferSettingsScope, key, cancellationToken);
        if (replay is not null)
        {
            OwnedContentMutationIdempotency.EnsureReplayMatches(replay, requestHash);
            return await GetBankTransferSettingsAsync(cancellationToken);
        }

        var accountHolder = NormalizeRequired(request.AccountHolder, 200);
        var bankName = NormalizeRequired(request.BankName, 200);
        var iban = BankTransferPaymentRules.NormalizeIban(request.Iban);
        if (accountHolder is null || bankName is null || iban is null
            || (request.IsEnabled && !BankTransferPaymentRules.HasCompletePublicSettings(true, accountHolder, bankName, iban)))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var settings = await db.BankTransferPaymentSettings
            .SingleOrDefaultAsync(item => item.Id == BankTransferSettingsId, cancellationToken);
        if (settings is null)
        {
            settings = new LegacyBankTransferPaymentSettings
            {
                Id = BankTransferSettingsId,
                CreatedAt = now,
                CreatedBy = actorId
            };
            db.BankTransferPaymentSettings.Add(settings);
        }

        settings.AccountHolder = accountHolder;
        settings.BankName = bankName;
        settings.Iban = iban;
        settings.Instructions = NormalizeOptional(request.Instructions, 2_000);
        settings.IsEnabled = request.IsEnabled;
        settings.UpdatedAt = now;
        settings.UpdatedBy = actorId;
        OwnedContentMutationIdempotency.Add(ownedDb, BankTransferSettingsScope, key, requestHash, BankTransferSettingsId, now);
        var concurrent = await OwnedContentMutationIdempotency.SaveAsync(ownedDb, BankTransferSettingsScope, key, cancellationToken);
        if (concurrent is not null)
        {
            OwnedContentMutationIdempotency.EnsureReplayMatches(concurrent, requestHash);
        }

        return await GetBankTransferSettingsAsync(cancellationToken);
    }

    public async Task<BankTransferPaymentRequestSummary?> CreateBankTransferPaymentRequestAsync(
        Guid userId,
        CreateBankTransferPaymentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        OwnedContentMutationIdempotency.Validate(userId, idempotencyKey);
        var key = idempotencyKey.Trim();
        var requestHash = OwnedContentMutationIdempotency.CreateRequestHash(userId, BankTransferRequestScope, Guid.Empty, request);
        var replay = await OwnedContentMutationIdempotency.GetAsync(ownedDb, BankTransferRequestScope, key, cancellationToken);
        if (replay is not null)
        {
            OwnedContentMutationIdempotency.EnsureReplayMatches(replay, requestHash);
            return await GetBankTransferPaymentRequestAsync(replay.ResourceId, userId, cancellationToken);
        }

        var settings = await GetPublicBankTransferSettingsAsync(cancellationToken);
        var paymentReference = BankTransferPaymentRules.NormalizePaymentReference(request.PaymentReference);
        if (settings is null || paymentReference is null)
        {
            return null;
        }

        var planRow = await (from plan in db.SubscriptionPlans.AsNoTracking()
                             join product in db.Products.AsNoTracking() on plan.ProductId equals product.Id
                             where plan.Id == request.PlanId
                                && plan.IsActive
                                && plan.IsPublic
                                && product.IsActive
                                && product.IsPublic
                                && plan.Price > 0
                                && !plan.IsContactOnly
                             select new { Plan = plan, Product = product }).SingleOrDefaultAsync(cancellationToken);
        if (planRow is null)
        {
            return null;
        }

        var existing = await db.BankTransferPaymentRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId && item.PaymentReference == paymentReference, cancellationToken);
        if (existing is not null)
        {
            return await GetBankTransferPaymentRequestAsync(existing.Id, userId, cancellationToken);
        }

        var user = (await userDirectory.GetUsersAsync([userId], cancellationToken)).Users
            .SingleOrDefault(item => item.UserId == userId);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return null;
        }

        var userName = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(userName)) userName = user.Email.Trim();
        var now = DateTime.UtcNow;
        var paymentRequest = new LegacyBankTransferPaymentRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = userName,
            UserEmail = user.Email.Trim(),
            PlanId = planRow.Plan.Id,
            Amount = planRow.Plan.Price,
            Currency = "TRY",
            PaymentReference = paymentReference,
            PayerName = NormalizeOptional(request.PayerName, 200),
            Note = NormalizeOptional(request.Note, 2_000),
            Status = BankTransferPaymentRules.PendingStatus,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.BankTransferPaymentRequests.Add(paymentRequest);
        OwnedContentMutationIdempotency.Add(ownedDb, BankTransferRequestScope, key, requestHash, paymentRequest.Id, now);
        var concurrent = await OwnedContentMutationIdempotency.SaveAsync(ownedDb, BankTransferRequestScope, key, cancellationToken);
        if (concurrent is not null)
        {
            OwnedContentMutationIdempotency.EnsureReplayMatches(concurrent, requestHash);
            return await GetBankTransferPaymentRequestAsync(concurrent.ResourceId, userId, cancellationToken);
        }

        return ToBankTransferPaymentRequestSummary(paymentRequest, planRow.Plan);
    }

    public async Task<IReadOnlyList<BankTransferPaymentRequestSummary>> GetMyBankTransferPaymentRequestsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await (from paymentRequest in db.BankTransferPaymentRequests.AsNoTracking()
                          join plan in db.SubscriptionPlans.AsNoTracking() on paymentRequest.PlanId equals plan.Id
                          where paymentRequest.UserId == userId
                          orderby paymentRequest.CreatedAt descending
                          select new { paymentRequest, plan }).ToListAsync(cancellationToken);
        return rows.Select(row => ToBankTransferPaymentRequestSummary(row.paymentRequest, row.plan)).ToList();
    }

    public async Task<SpeedReadingPage<BankTransferPaymentRequestSummary>> GetBankTransferPaymentRequestsAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedSize) = NormalizePage(page, pageSize);
        var query = from paymentRequest in db.BankTransferPaymentRequests.AsNoTracking()
                    join plan in db.SubscriptionPlans.AsNoTracking() on paymentRequest.PlanId equals plan.Id
                    select new { paymentRequest, plan };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(row => row.paymentRequest.UserName.ToLower().Contains(value)
                || row.paymentRequest.UserEmail.ToLower().Contains(value)
                || row.paymentRequest.PaymentReference.ToLower().Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(row => row.paymentRequest.Status == status.Trim());
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(row => row.paymentRequest.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);
        return new SpeedReadingPage<BankTransferPaymentRequestSummary>(
            rows.Select(row => ToBankTransferPaymentRequestSummary(row.paymentRequest, row.plan)).ToList(),
            normalizedPage,
            normalizedSize,
            total);
    }

    public async Task<BankTransferPaymentRequestSummary?> ReviewBankTransferPaymentRequestAsync(
        Guid id,
        ReviewBankTransferPaymentRequest request,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        OwnedContentMutationIdempotency.Validate(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        var requestHash = OwnedContentMutationIdempotency.CreateRequestHash(actorId, BankTransferReviewScope, id, request);
        var replay = await OwnedContentMutationIdempotency.GetAsync(ownedDb, BankTransferReviewScope, key, cancellationToken);
        if (replay is not null)
        {
            OwnedContentMutationIdempotency.EnsureReplayMatches(replay, requestHash);
            return await GetBankTransferPaymentRequestAsync(replay.ResourceId, null, cancellationToken);
        }

        var row = await (from paymentRequest in db.BankTransferPaymentRequests
                         join plan in db.SubscriptionPlans.AsNoTracking() on paymentRequest.PlanId equals plan.Id
                         join product in db.Products.AsNoTracking() on plan.ProductId equals product.Id
                         where paymentRequest.Id == id
                         select new { paymentRequest, plan, product })
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;

        var targetStatus = request.Status?.Trim();
        if (string.Equals(row.paymentRequest.Status, targetStatus, StringComparison.OrdinalIgnoreCase))
        {
            return ToBankTransferPaymentRequestSummary(row.paymentRequest, row.plan);
        }
        if (!BankTransferPaymentRules.CanTransition(row.paymentRequest.Status, targetStatus)) return null;

        var reviewNote = NormalizeOptional(request.ReviewNote, 2_000);
        if (string.Equals(targetStatus, BankTransferPaymentRules.RejectedStatus, StringComparison.OrdinalIgnoreCase)
            && reviewNote is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        row.paymentRequest.Status = targetStatus!;
        row.paymentRequest.ReviewedBy = actorId;
        row.paymentRequest.ReviewedAt = now;
        row.paymentRequest.ReviewNote = reviewNote;
        row.paymentRequest.UpdatedAt = now;
        if (string.Equals(targetStatus, BankTransferPaymentRules.ApprovedStatus, StringComparison.OrdinalIgnoreCase))
        {
            var subscriptionNote = $"Bank transfer payment {row.paymentRequest.Id:N}";
            var subscription = await db.UserSubscriptions
                .Where(item => item.UserId == row.paymentRequest.UserId
                    && item.PlanId == row.plan.Id
                    && item.Status == "Active"
                    && !item.IsDeleted
                    && item.Notes == subscriptionNote
                    && (!item.EndDate.HasValue || item.EndDate > now))
                .OrderBy(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (subscription is null)
            {
                subscription = new LegacyUserSubscription
                {
                    Id = Guid.NewGuid(),
                    UserId = row.paymentRequest.UserId,
                    UserName = row.paymentRequest.UserName,
                    UserEmail = row.paymentRequest.UserEmail,
                    PlanId = row.plan.Id,
                    ProductId = row.plan.ProductId,
                    Status = "Active",
                    StartDate = now,
                    EndDate = SpeedReadingAccessRules.ResolveEndDate(now, null, row.plan.DurationDays),
                    Notes = subscriptionNote,
                    CreatedBy = actorId,
                    CreatedAt = now
                };
                db.UserSubscriptions.Add(subscription);
            }
            row.paymentRequest.SubscriptionId = subscription.Id;
        }

        OwnedContentMutationIdempotency.Add(ownedDb, BankTransferReviewScope, key, requestHash, id, now);
        try
        {
            var concurrent = await OwnedContentMutationIdempotency.SaveAsync(ownedDb, BankTransferReviewScope, key, cancellationToken);
            if (concurrent is not null)
            {
                OwnedContentMutationIdempotency.EnsureReplayMatches(concurrent, requestHash);
                return await GetBankTransferPaymentRequestAsync(concurrent.ResourceId, null, cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("BankTransferPaymentRequest", id);
        }

        return ToBankTransferPaymentRequestSummary(row.paymentRequest, row.plan);
    }

    public async Task<SpeedReadingPage<UserSubscriptionSummary>> GetSubscriptionsAsync(string? search, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedSize) = NormalizePage(page, pageSize);
        var query = SubscriptionRows();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(row => (row.Subscription.UserName ?? "").ToLower().Contains(value)
                || (row.Subscription.UserEmail ?? "").ToLower().Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(row => row.Subscription.Status == status.Trim());
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(row => row.Subscription.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);
        var items = rows.Select(row => ToSummary(row.Subscription, row.Plan, row.Product)).ToList();
        return new SpeedReadingPage<UserSubscriptionSummary>(items, normalizedPage, normalizedSize, total);
    }

    public async Task<IReadOnlyList<UserSubscriptionSummary>> GetUserSubscriptionsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await GetUserSubscriptionRows(userId, cancellationToken);

    public async Task<UserSubscriptionSummary?> CreateSubscriptionAsync(CreateUserSubscriptionRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var row = await (from plan in db.SubscriptionPlans.AsNoTracking()
                         join product in db.Products.AsNoTracking() on plan.ProductId equals product.Id
                         where plan.Id == request.PlanId && plan.IsActive && product.IsActive
                         select new { Plan = plan, Product = product }).SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var subscriptionPlan = row.Plan;

        var subscription = new LegacyUserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            UserName = request.UserName,
            UserEmail = request.UserEmail,
            PlanId = subscriptionPlan.Id,
            ProductId = subscriptionPlan.ProductId,
            Status = "Active",
            StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
            EndDate = SpeedReadingAccessRules.ResolveEndDate(
                DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
                request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc) : null,
                subscriptionPlan.DurationDays),
            Notes = request.Notes,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        db.UserSubscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);

        return ToSummary(subscription, subscriptionPlan, row.Product);
    }

    public async Task<InstitutionAccessApprovalSummary?> CreateInstitutionAccessAsync(
        CreateInstitutionAccessRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var planRow = await (from plan in db.SubscriptionPlans.AsNoTracking()
                             join product in db.Products.AsNoTracking() on plan.ProductId equals product.Id
                             where plan.Id == request.PlanId && product.IsActive
                             select new { Plan = plan, Product = product }).SingleOrDefaultAsync(cancellationToken);
        if (request.InstitutionId == Guid.Empty
            || planRow is null
            || !BankTransferPaymentRules.IsInstitutionAccessPlan(
                planRow.Plan.IsActive,
                planRow.Plan.IsPublic,
                planRow.Plan.IsContactOnly,
                planRow.Plan.DurationDays))
        {
            return null;
        }

        var institutionPlan = planRow.Plan;

        var recipients = request.Recipients
            .GroupBy(item => item.UserId)
            .Select(group => group.First())
            .ToList();
        if (recipients.Count == 0)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var paymentReference = NormalizePaymentReference(request.PaymentReference);
        if (paymentReference is not null)
        {
            var existingLicense = await db.InstitutionAccessLicenses.AsNoTracking()
                .SingleOrDefaultAsync(item => item.InstitutionId == request.InstitutionId
                    && item.PaymentReference == paymentReference, cancellationToken);
            if (existingLicense is not null)
            {
                var existingSummary = await ToInstitutionAccessLicenseSummaryAsync(existingLicense, cancellationToken);
                return new InstitutionAccessApprovalSummary(0, recipients.Count, [], existingSummary);
            }
        }

        var recipientIds = recipients.Select(item => item.UserId).ToList();
        var activeUserIds = await db.UserSubscriptions.AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.PlanId == institutionPlan.Id
                && (item.Status == "Active" || item.Status == "Paused")
                && recipientIds.Contains(item.UserId)
                && (!item.EndDate.HasValue || item.EndDate > now))
            .Select(item => item.UserId)
            .ToHashSetAsync(cancellationToken);

        var startDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
        var endDate = SpeedReadingAccessRules.ResolveEndDate(startDate, null, institutionPlan.DurationDays);
        if (endDate is null)
        {
            return null;
        }
        var newSubscriptions = recipients
            .Where(recipient => !activeUserIds.Contains(recipient.UserId))
            .Select(recipient => new LegacyUserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = recipient.UserId,
                UserName = recipient.UserName,
                UserEmail = recipient.UserEmail,
                PlanId = institutionPlan.Id,
                ProductId = institutionPlan.ProductId,
                Status = "Active",
                StartDate = startDate,
                EndDate = endDate,
                Notes = request.Notes,
                CreatedBy = actorId,
                CreatedAt = now
            })
            .ToList();

        if (newSubscriptions.Count == 0)
        {
            return new InstitutionAccessApprovalSummary(0, recipients.Count, [], null);
        }

        var license = new LegacyInstitutionAccessLicense
        {
            Id = Guid.NewGuid(),
            InstitutionId = request.InstitutionId,
            PlanId = institutionPlan.Id,
            Status = "Active",
            SeatCount = newSubscriptions.Count,
            StartDate = startDate,
            EndDate = endDate.Value,
            PaymentReference = paymentReference,
            Notes = request.Notes?.Trim(),
            ApprovedBy = actorId,
            ApprovedAt = now
        };
        newSubscriptions.ForEach(subscription => subscription.InstitutionAccessLicenseId = license.Id);
        db.InstitutionAccessLicenses.Add(license);
        db.UserSubscriptions.AddRange(newSubscriptions);
        await db.SaveChangesAsync(cancellationToken);

        return new InstitutionAccessApprovalSummary(
            newSubscriptions.Count,
            recipients.Count - newSubscriptions.Count,
            newSubscriptions.Select(subscription => ToSummary(subscription, institutionPlan, planRow.Product)).ToList(),
            ToInstitutionAccessLicenseSummary(license, institutionPlan, planRow.Product, newSubscriptions.Select(item => item.UserId).ToList()));
    }

    public async Task<InstitutionAccessLicenseSummary?> GetInstitutionAccessOverviewAsync(
        Guid institutionId,
        CancellationToken cancellationToken = default)
    {
        if (institutionId == Guid.Empty) return null;
        var license = await db.InstitutionAccessLicenses.AsNoTracking()
            .Where(item => item.InstitutionId == institutionId && item.Status == "Active")
            .OrderByDescending(item => item.EndDate)
            .ThenByDescending(item => item.ApprovedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return license is null ? null : await ToInstitutionAccessLicenseSummaryAsync(license, cancellationToken);
    }

    public async Task<bool> ChangeInstitutionStudentAccessAsync(
        Guid institutionId,
        Guid studentId,
        InstitutionStudentAccessChangeRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (institutionId == Guid.Empty || studentId == Guid.Empty || actorId == Guid.Empty) return false;
        var now = DateTime.UtcNow;
        var subscription = await (from item in db.UserSubscriptions
                                  join license in db.InstitutionAccessLicenses on item.InstitutionAccessLicenseId equals license.Id
                                  where license.InstitutionId == institutionId
                                      && item.UserId == studentId
                                      && !item.IsDeleted
                                      && item.Status == (request.IsSuspended ? "Active" : "Paused")
                                      && (!item.EndDate.HasValue || item.EndDate > now)
                                  orderby license.EndDate descending, item.CreatedAt descending
                                  select item).FirstOrDefaultAsync(cancellationToken);
        if (subscription is null || subscription.InstitutionAccessLicenseId is null) return false;

        subscription.Status = request.IsSuspended ? "Paused" : "Active";
        subscription.UpdatedAt = now;
        subscription.UpdatedBy = actorId;
        db.InstitutionAccessActions.Add(new LegacyInstitutionAccessAction
        {
            Id = Guid.NewGuid(),
            InstitutionAccessLicenseId = subscription.InstitutionAccessLicenseId.Value,
            StudentId = studentId,
            Action = request.IsSuspended ? "Suspend" : "Resume",
            Reason = NormalizeActionReason(request.Reason),
            PerformedBy = actorId,
            PerformedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<UserSubscriptionSummary?> UpdateSubscriptionAsync(Guid id, UpdateUserSubscriptionRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var subscription = await db.UserSubscriptions.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (subscription is null)
        {
            return null;
        }

        subscription.Status = request.Status.Trim();
        if (request.EndDate.HasValue) subscription.EndDate = DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc);
        if (request.Notes is not null) subscription.Notes = request.Notes;
        subscription.UpdatedAt = DateTime.UtcNow;
        subscription.UpdatedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);

        return await GetSubscriptionAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteSubscriptionAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        var subscription = await db.UserSubscriptions.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (subscription is null)
        {
            return false;
        }

        subscription.IsDeleted = true;
        subscription.DeletedAt = DateTime.UtcNow;
        subscription.UpdatedBy = actorId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<IReadOnlyList<UserSubscriptionSummary>> GetMySubscriptionsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        GetUserSubscriptionsAsync(userId, cancellationToken);

    public async Task<UserAccessSummary> GetMyAccessAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var products = await (from subscription in db.UserSubscriptions.AsNoTracking()
                              join product in db.Products.AsNoTracking() on subscription.ProductId equals product.Id
                              where subscription.UserId == userId
                                  && !subscription.IsDeleted
                                  && subscription.Status == "Active"
                                  && (subscription.EndDate == null || subscription.EndDate > DateTime.UtcNow)
                              select product).ToListAsync(cancellationToken);
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var product in products)
        {
            slugs.Add(product.Slug);
            foreach (var included in Deserialize(product.IncludedProductSlugsJson))
            {
                slugs.Add(included);
            }
        }

        var ordered = slugs.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
        return new UserAccessSummary(ordered, ordered.Contains("hizliokuma", StringComparer.OrdinalIgnoreCase), ordered.Contains("kocluk", StringComparer.OrdinalIgnoreCase));
    }

    public async Task<PaymentInitializationResult> InitializePaymentAsync(
        Guid userId,
        InitializePaymentRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (!paymentProvider.IsConfigured)
        {
            return new(false, false, null, null, null, "Payment provider is not configured for this deployment.");
        }

        var buyerValidationMessage = ValidateBuyer(request);
        if (buyerValidationMessage is not null)
        {
            return new(true, false, null, null, null, buyerValidationMessage);
        }

        var plan = await (from candidate in db.SubscriptionPlans.AsNoTracking()
                          join product in db.Products.AsNoTracking() on candidate.ProductId equals product.Id
                          where candidate.Id == request.PlanId
                              && candidate.IsActive
                              && candidate.IsPublic
                              && product.IsActive
                              && product.IsPublic
                          select new { Plan = candidate, Product = product })
            .SingleOrDefaultAsync(cancellationToken);
        if (plan is null)
        {
            return new(true, false, null, null, null, "The selected payment plan is not available.");
        }

        if (!BankTransferPaymentRules.CanRequestBankTransfer(plan.Plan.Price, plan.Plan.IsContactOnly))
        {
            return new(true, false, null, null, null, "A paid plan must be selected for checkout.");
        }

        var user = (await userDirectory.GetUsersAsync([userId], cancellationToken)).Users.SingleOrDefault(item => item.UserId == userId);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return new(true, false, null, null, null, "A complete user profile is required before checkout.");
        }

        var userName = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(userName))
        {
            userName = user.Email.Trim();
        }

        if (string.IsNullOrWhiteSpace(userName))
        {
            return new(true, false, null, null, null, "A complete user name is required before checkout.");
        }

        var payment = new LegacyPayment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = user.Email.Trim(),
            UserName = userName,
            PlanId = plan.Plan.Id,
            Amount = plan.Plan.Price,
            Currency = "TRY",
            Status = SpeedReadingPaymentRules.PendingStatus,
            Provider = "Iyzico",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);

        var conversationId = payment.Id.ToString("N");
        PaymentProviderInitializationResult providerResult;
        try
        {
            providerResult = await paymentProvider.InitializeAsync(
                new PaymentProviderInitializationRequest(
                    conversationId,
                    $"speed-reading-{plan.Plan.Id:N}",
                    "tr",
                    plan.Plan.Price,
                    payment.Currency,
                    iyzicoOptions.CallbackUrl!,
                    plan.Plan.Name,
                    new PaymentBuyerInfo(
                        userId.ToString("N"),
                        user.FirstName.Trim(),
                        user.LastName.Trim(),
                        user.Email.Trim(),
                        NormalizePhone(request.PhoneNumber!),
                        request.IdentityNumber!.Trim(),
                        request.BillingAddress!.Trim(),
                        request.City!.Trim(),
                        "Turkey",
                        request.ZipCode!.Trim(),
                        ipAddress ?? "127.0.0.1")),
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            payment.Status = SpeedReadingPaymentRules.FailedStatus;
            payment.ErrorMessage = "Payment provider request timed out.";
            payment.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return new(true, false, null, null, null, payment.ErrorMessage);
        }
        catch (HttpRequestException)
        {
            payment.Status = SpeedReadingPaymentRules.FailedStatus;
            payment.ErrorMessage = "Payment provider could not be reached.";
            payment.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return new(true, false, null, null, null, payment.ErrorMessage);
        }

        payment.ProviderResponse = providerResult.RawResponse;
        payment.UpdatedAt = DateTime.UtcNow;
        if (!providerResult.Succeeded || string.IsNullOrWhiteSpace(providerResult.Token))
        {
            payment.Status = SpeedReadingPaymentRules.FailedStatus;
            payment.ErrorMessage = providerResult.ErrorMessage ?? "Payment provider rejected checkout initialization.";
            await db.SaveChangesAsync(cancellationToken);
            return new(true, false, null, null, null, payment.ErrorMessage);
        }

        payment.ProviderToken = providerResult.Token;
        payment.ErrorMessage = null;
        await db.SaveChangesAsync(cancellationToken);
        return new(true, true, providerResult.Token, providerResult.PaymentPageUrl, providerResult.CheckoutFormContent, null);
    }

    public async Task<PaymentVerificationResult> VerifyPaymentAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (!paymentProvider.IsConfigured)
        {
            return UnavailableVerification();
        }

        var payment = await db.Payments.SingleOrDefaultAsync(
            item => item.UserId == userId
                && item.Provider == "Iyzico"
                && item.ProviderToken == token.Trim(),
            cancellationToken);
        return payment is null
            ? FailedVerification("Payment could not be found.")
            : await RetrieveAndApplyPaymentAsync(payment, cancellationToken);
    }

    public async Task<PaymentVerificationResult> ProcessPaymentCallbackAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (!paymentProvider.IsConfigured)
        {
            return UnavailableVerification();
        }

        var normalizedToken = token.Trim();
        var payment = await db.Payments.SingleOrDefaultAsync(
            item => item.Provider == "Iyzico" && item.ProviderToken == normalizedToken,
            cancellationToken);
        return payment is null
            ? FailedVerification("Payment could not be found.")
            : await RetrieveAndApplyPaymentAsync(payment, cancellationToken);
    }

    public async Task<SpeedReadingPage<PaymentSummary>> GetPaymentsAsync(int page, int pageSize, string? status, string? search, CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedSize) = NormalizePage(page, pageSize);
        var query = from payment in db.Payments.AsNoTracking()
                    join plan in db.SubscriptionPlans.AsNoTracking() on payment.PlanId equals plan.Id
                    select new { payment, plan };
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(row => row.payment.Status == status.Trim());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(row => row.payment.UserEmail.ToLower().Contains(value) || row.payment.UserName.ToLower().Contains(value));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(row => row.payment.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);
        var items = rows.Select(row => new PaymentSummary(
            row.payment.Id,
            row.payment.UserId,
            row.payment.UserEmail,
            row.payment.UserName,
            row.plan.Name,
            row.payment.Amount,
            row.payment.Currency,
            row.payment.Status,
            row.payment.Provider,
            row.payment.ProviderPaymentId,
            row.payment.ErrorMessage,
            row.payment.SubscriptionId,
            row.payment.CreatedAt)).ToList();
        return new SpeedReadingPage<PaymentSummary>(items, normalizedPage, normalizedSize, total);
    }

    private async Task<PaymentVerificationResult> RetrieveAndApplyPaymentAsync(
        LegacyPayment payment,
        CancellationToken cancellationToken)
    {
        var plan = await db.SubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(item => item.Id == payment.PlanId, cancellationToken);
        if (plan is null)
        {
            return FailedVerification("The payment plan no longer exists.");
        }

        if (string.Equals(payment.Status, SpeedReadingPaymentRules.SuccessStatus, StringComparison.OrdinalIgnoreCase)
            && payment.SubscriptionId.HasValue)
        {
            return SuccessVerification(plan.Name, payment.Amount, payment.SubscriptionId);
        }

        PaymentProviderRetrieveResult providerResult;
        try
        {
            providerResult = await paymentProvider.RetrieveAsync(
                new PaymentProviderRetrieveRequest(payment.Id.ToString("N"), payment.ProviderToken!, "tr"),
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await MarkPendingAsync(payment, "Payment provider request timed out.", cancellationToken);
        }
        catch (HttpRequestException)
        {
            return await MarkPendingAsync(payment, "Payment provider could not be reached.", cancellationToken);
        }

        payment.ProviderResponse = providerResult.RawResponse;
        payment.UpdatedAt = DateTime.UtcNow;
        if (!providerResult.RequestSucceeded)
        {
            return await MarkPendingAsync(payment, providerResult.ErrorMessage ?? "Payment result could not be retrieved.", cancellationToken);
        }

        if (!providerResult.ResponseSignatureValid)
        {
            payment.Status = SpeedReadingPaymentRules.FailedStatus;
            payment.ErrorMessage = "Payment provider response signature is invalid.";
            await db.SaveChangesAsync(cancellationToken);
            return FailedVerification(payment.ErrorMessage, plan.Name, payment.Amount);
        }

        var expectedBasketId = $"speed-reading-{payment.PlanId:N}";
        var responseMatchesPayment = string.Equals(providerResult.Token, payment.ProviderToken, StringComparison.Ordinal)
            && string.Equals(providerResult.ConversationId, payment.Id.ToString("N"), StringComparison.Ordinal)
            && string.Equals(providerResult.BasketId, expectedBasketId, StringComparison.Ordinal)
            && string.Equals(providerResult.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase)
            && providerResult.Price.HasValue
            && providerResult.PaidPrice.HasValue
            && Math.Abs(providerResult.Price.Value - payment.Amount) <= 0.01m
            && Math.Abs(providerResult.PaidPrice.Value - payment.Amount) <= 0.01m;
        if (!responseMatchesPayment)
        {
            payment.Status = SpeedReadingPaymentRules.FailedStatus;
            payment.ErrorMessage = "Payment result does not match the checkout request.";
            await db.SaveChangesAsync(cancellationToken);
            return FailedVerification(payment.ErrorMessage, plan.Name, payment.Amount);
        }

        var resolvedStatus = SpeedReadingPaymentRules.ResolveStatus(providerResult.ProviderStatus, providerResult.FraudStatus);
        payment.Status = resolvedStatus;
        payment.ProviderPaymentId = providerResult.PaymentId;
        payment.ErrorMessage = providerResult.ErrorMessage;
        if (!string.Equals(resolvedStatus, SpeedReadingPaymentRules.SuccessStatus, StringComparison.Ordinal))
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(true, false, resolvedStatus, plan.Name, payment.Amount, null, providerResult.ErrorMessage);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Entry(payment).State = EntityState.Detached;
        var persistedPayment = await db.Payments
            .SingleOrDefaultAsync(item => item.Id == payment.Id, cancellationToken);
        if (persistedPayment is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return FailedVerification("Payment could not be found.", plan.Name, payment.Amount);
        }

        payment = persistedPayment;
        if (string.Equals(payment.Status, SpeedReadingPaymentRules.SuccessStatus, StringComparison.OrdinalIgnoreCase)
            && payment.SubscriptionId.HasValue)
        {
            await transaction.CommitAsync(cancellationToken);
            return SuccessVerification(plan.Name, payment.Amount, payment.SubscriptionId);
        }

        var existingSubscription = payment.SubscriptionId.HasValue
            ? await db.UserSubscriptions.SingleOrDefaultAsync(
                item => item.Id == payment.SubscriptionId.Value && !item.IsDeleted,
                cancellationToken)
            : null;
        if (existingSubscription is null)
        {
            var now = DateTime.UtcNow;
            existingSubscription = new LegacyUserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = payment.UserId,
                UserName = payment.UserName,
                UserEmail = payment.UserEmail,
                PlanId = plan.Id,
                ProductId = plan.ProductId,
                Status = "Active",
                StartDate = now,
                EndDate = SpeedReadingPaymentRules.ResolveEndDate(now, plan.DurationDays),
                Notes = $"Iyzico payment {payment.Id:N}",
                CreatedBy = payment.UserId,
                CreatedAt = now
            };
            db.UserSubscriptions.Add(existingSubscription);
        }

        payment.SubscriptionId = existingSubscription.Id;
        payment.Status = SpeedReadingPaymentRules.SuccessStatus;
        payment.ErrorMessage = null;
        payment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SuccessVerification(plan.Name, payment.Amount, existingSubscription.Id);
    }

    private async Task<PaymentVerificationResult> MarkPendingAsync(
        LegacyPayment payment,
        string message,
        CancellationToken cancellationToken)
    {
        payment.Status = SpeedReadingPaymentRules.PendingStatus;
        payment.ErrorMessage = message;
        payment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var plan = await db.SubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(item => item.Id == payment.PlanId, cancellationToken);
        return new(true, false, SpeedReadingPaymentRules.PendingStatus, plan?.Name, payment.Amount, null, message);
    }

    private static string? ValidateBuyer(InitializePaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return "Phone number is required for checkout.";
        }

        var digits = new string(request.PhoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length is < 10 or > 15)
        {
            return "Enter a valid phone number for checkout.";
        }

        if (!SpeedReadingPaymentRules.IsValidIdentityNumber(request.IdentityNumber?.Trim()))
        {
            return "Enter a valid 11-digit identity number for checkout.";
        }

        if (string.IsNullOrWhiteSpace(request.BillingAddress)
            || request.BillingAddress.Trim().Length < 5
            || string.IsNullOrWhiteSpace(request.City)
            || string.IsNullOrWhiteSpace(request.ZipCode))
        {
            return "Billing address details are required for checkout.";
        }

        return null;
    }

    private static string NormalizePhone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 10
            ? $"+90{digits}"
            : digits.StartsWith('0') && digits.Length == 11
                ? $"+9{digits}"
                : value.Trim();
    }

    private static PaymentVerificationResult UnavailableVerification() =>
        new(false, false, "Unavailable", null, 0, null, "Payment provider is not configured for this deployment.");

    private static PaymentVerificationResult FailedVerification(
        string message,
        string? planName = null,
        decimal amount = 0) =>
        new(true, false, SpeedReadingPaymentRules.FailedStatus, planName, amount, null, message);

    private static PaymentVerificationResult SuccessVerification(
        string planName,
        decimal amount,
        Guid? subscriptionId) =>
        new(true, true, SpeedReadingPaymentRules.SuccessStatus, planName, amount, subscriptionId, null);

    private IQueryable<SubscriptionRow> SubscriptionRows()
    {
        return from subscription in db.UserSubscriptions.AsNoTracking()
               join plan in db.SubscriptionPlans.AsNoTracking() on subscription.PlanId equals plan.Id
               join product in db.Products.AsNoTracking() on subscription.ProductId equals product.Id
               where !subscription.IsDeleted
               select new SubscriptionRow { Subscription = subscription, Plan = plan, Product = product };
    }

    private async Task<IReadOnlyList<UserSubscriptionSummary>> GetUserSubscriptionRows(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await SubscriptionRows()
            .Where(row => row.Subscription.UserId == userId)
            .OrderByDescending(row => row.Subscription.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(row => ToSummary(row.Subscription, row.Plan, row.Product)).ToList();
    }

    private async Task<UserSubscriptionSummary?> GetSubscriptionAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await SubscriptionRows().SingleOrDefaultAsync(item => item.Subscription.Id == id, cancellationToken);
        return row is null
            ? null
            : ToSummary(row.Subscription, row.Plan, row.Product);
    }

    private async Task<InstitutionAccessLicenseSummary?> ToInstitutionAccessLicenseSummaryAsync(
        LegacyInstitutionAccessLicense license,
        CancellationToken cancellationToken)
    {
        var row = await (from plan in db.SubscriptionPlans.AsNoTracking()
                         join product in db.Products.AsNoTracking() on plan.ProductId equals product.Id
                         where plan.Id == license.PlanId
                         select new { plan, product }).SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        var accessRows = await db.UserSubscriptions.AsNoTracking()
            .Where(item => item.InstitutionAccessLicenseId == license.Id
                && !item.IsDeleted
                && (!item.EndDate.HasValue || item.EndDate > DateTime.UtcNow))
            .Select(item => new { item.UserId, item.Status })
            .ToListAsync(cancellationToken);
        return ToInstitutionAccessLicenseSummary(
            license,
            row.plan,
            row.product,
            accessRows.Where(item => item.Status == "Active").Select(item => item.UserId).ToList(),
            accessRows.Where(item => item.Status == "Paused").Select(item => item.UserId).ToList());
    }

    private static InstitutionAccessLicenseSummary ToInstitutionAccessLicenseSummary(
        LegacyInstitutionAccessLicense license,
        LegacySubscriptionPlan plan,
        LegacyProduct product,
        IReadOnlyList<Guid> activeStudentIds,
        IReadOnlyList<Guid>? suspendedStudentIds = null) =>
        new(license.Id, license.InstitutionId, ToSummary(plan, product), license.Status, license.SeatCount,
            activeStudentIds.Count + (suspendedStudentIds?.Count ?? 0), activeStudentIds, suspendedStudentIds ?? [], license.StartDate, license.EndDate, license.PaymentReference, license.Notes,
            license.ApprovedAt);

    private async Task<BankTransferPaymentRequestSummary?> GetBankTransferPaymentRequestAsync(
        Guid id,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var row = await (from paymentRequest in db.BankTransferPaymentRequests.AsNoTracking()
                         join plan in db.SubscriptionPlans.AsNoTracking() on paymentRequest.PlanId equals plan.Id
                         where paymentRequest.Id == id && (!userId.HasValue || paymentRequest.UserId == userId.Value)
                         select new { paymentRequest, plan }).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : ToBankTransferPaymentRequestSummary(row.paymentRequest, row.plan);
    }

    private static BankTransferPaymentSettingsSummary ToBankTransferSettingsSummary(LegacyBankTransferPaymentSettings settings) =>
        new(
            settings.Id,
            settings.AccountHolder,
            settings.BankName,
            settings.Iban,
            settings.Instructions,
            settings.IsEnabled,
            BankTransferPaymentRules.HasCompletePublicSettings(
                settings.IsEnabled,
                settings.AccountHolder,
                settings.BankName,
                settings.Iban),
            settings.UpdatedAt ?? settings.CreatedAt);

    private static BankTransferPaymentRequestSummary ToBankTransferPaymentRequestSummary(
        LegacyBankTransferPaymentRequest paymentRequest,
        LegacySubscriptionPlan plan) =>
        new(
            paymentRequest.Id,
            paymentRequest.UserId,
            paymentRequest.UserName,
            paymentRequest.UserEmail,
            paymentRequest.PlanId,
            plan.Name,
            paymentRequest.Amount,
            paymentRequest.Currency,
            paymentRequest.PaymentReference,
            paymentRequest.PayerName,
            paymentRequest.Note,
            paymentRequest.Status,
            paymentRequest.SubscriptionId,
            paymentRequest.CreatedAt,
            paymentRequest.ReviewedBy,
            paymentRequest.ReviewedAt,
            paymentRequest.ReviewNote);

    private static string? NormalizeRequired(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength ? null : normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized[..Math.Min(normalized.Length, maxLength)];
    }

    private static string? NormalizePlanSlug(string? value)
    {
        var slug = value?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(slug)
            || slug.Length is < 3 or > 100
            || slug.Any(character => !((character is >= 'a' and <= 'z') || char.IsDigit(character) || character == '-'))
                ? null
                : slug;
    }

    private static string? NormalizePaymentReference(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized[..Math.Min(normalized.Length, 200)];
    }

    private static string? NormalizeActionReason(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized[..Math.Min(normalized.Length, 1_000)];
    }

    private static ProductSummary ToSummary(LegacyProduct product) =>
        new(product.Id, product.Slug, product.Name, product.Description, Deserialize(product.IncludedProductSlugsJson), product.IsActive, product.IsPublic, product.SortOrder);

    private static SubscriptionPlanSummary ToSummary(LegacySubscriptionPlan plan, LegacyProduct product)
    {
        var included = Deserialize(product.IncludedProductSlugsJson);
        var allSlugs = new HashSet<string>(included, StringComparer.OrdinalIgnoreCase) { product.Slug };
        var modules = new List<string>();
        if (allSlugs.Contains("hizliokuma")) modules.Add("SpeedReading");
        if (allSlugs.Contains("kocluk")) modules.Add("Coaching");
        return new SubscriptionPlanSummary(plan.Id, plan.Name, plan.Description, plan.Slug, plan.ProductId, product.Slug, product.Name, included, modules, plan.Price, plan.IsContactOnly, plan.BillingPeriod, plan.DurationDays, plan.IsActive, plan.IsPublic, plan.SortOrder, Deserialize(plan.Features));
    }

    private static UserSubscriptionSummary ToSummary(LegacyUserSubscription subscription, LegacySubscriptionPlan plan, LegacyProduct product)
    {
        var isActive = string.Equals(subscription.Status, "Active", StringComparison.OrdinalIgnoreCase)
            && (!subscription.EndDate.HasValue || subscription.EndDate.Value > DateTime.UtcNow);
        return new UserSubscriptionSummary(subscription.Id, subscription.UserId, subscription.UserName, subscription.UserEmail, ToSummary(plan, product), product.Slug, product.Name, subscription.Status, subscription.StartDate, subscription.EndDate, subscription.Notes, subscription.CreatedAt, isActive);
    }

    private static string Serialize<T>(T? value) => JsonSerializer.Serialize(value ?? (object)Array.Empty<string>());

    private static List<string> Deserialize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(value) ?? []; }
        catch (JsonException) { return []; }
    }

    private static (int Page, int Size) NormalizePage(int page, int pageSize) => (Math.Max(page, 1), Math.Clamp(pageSize, 1, 100));

    private sealed class SubscriptionRow
    {
        public LegacyUserSubscription Subscription { get; init; } = null!;
        public LegacySubscriptionPlan Plan { get; init; } = null!;
        public LegacyProduct Product { get; init; } = null!;
    }

    private sealed class UnconfiguredPaymentProvider : ISpeedReadingPaymentProvider
    {
        public bool IsConfigured => false;

        public Task<PaymentProviderInitializationResult> InitializeAsync(
            PaymentProviderInitializationRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentProviderInitializationResult(
                false,
                null,
                null,
                null,
                "Payment provider is not configured for this deployment.",
                string.Empty));

        public Task<PaymentProviderRetrieveResult> RetrieveAsync(
            PaymentProviderRetrieveRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentProviderRetrieveResult(
                false,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "Payment provider is not configured for this deployment.",
                string.Empty));
    }

    private sealed class UnconfiguredUserDirectory : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse([]));

        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(
            string? role,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
