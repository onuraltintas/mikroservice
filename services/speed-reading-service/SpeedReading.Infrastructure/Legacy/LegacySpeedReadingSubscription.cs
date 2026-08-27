using System.Text.Json;
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
    private readonly ISpeedReadingDataContext db;
    private readonly ISpeedReadingPaymentProvider paymentProvider;
    private readonly IyzicoOptions iyzicoOptions;
    private readonly ISpeedReadingUserDirectory userDirectory;

    internal LegacySpeedReadingSubscription(
        ISpeedReadingDataContext db,
        ISpeedReadingPaymentProvider paymentProvider,
        IyzicoOptions iyzicoOptions,
        ISpeedReadingUserDirectory userDirectory)
    {
        this.db = db;
        this.paymentProvider = paymentProvider;
        this.iyzicoOptions = iyzicoOptions;
        this.userDirectory = userDirectory;
    }

    public LegacySpeedReadingSubscription(SpeedReadingDbContext db)
        : this(db, new UnconfiguredPaymentProvider(), new IyzicoOptions(), new UnconfiguredUserDirectory())
    {
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
                    where includeInactive || (plan.IsActive && plan.IsPublic)
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
                         select new { plan, product }).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : ToSummary(row.plan, row.product);
    }

    public async Task<Guid?> CreatePlanAsync(CreateSubscriptionPlanRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (!await db.Products.AnyAsync(item => item.Id == request.ProductId, cancellationToken)
            || await db.SubscriptionPlans.AnyAsync(item => item.Slug == request.Slug.Trim(), cancellationToken))
        {
            return null;
        }

        var plan = new LegacySubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Slug = request.Slug.Trim(),
            ProductId = request.ProductId,
            Price = request.Price,
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

        if (request.Name is not null) plan.Name = request.Name.Trim();
        if (request.Description is not null) plan.Description = request.Description.Trim();
        if (request.Price.HasValue) plan.Price = request.Price.Value;
        if (request.BillingPeriod is not null) plan.BillingPeriod = request.BillingPeriod.Trim();
        if (request.DurationDays.HasValue) plan.DurationDays = request.DurationDays.Value == 0 ? null : request.DurationDays.Value;
        if (request.IsActive.HasValue) plan.IsActive = request.IsActive.Value;
        if (request.IsPublic.HasValue) plan.IsPublic = request.IsPublic.Value;
        if (request.SortOrder.HasValue) plan.SortOrder = request.SortOrder.Value;
        if (request.Features is not null) plan.Features = Serialize(request.Features);
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var product = await db.Products.AsNoTracking().SingleAsync(item => item.Id == plan.ProductId, cancellationToken);
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
        var plan = await db.SubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.PlanId, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        var subscription = new LegacyUserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            UserName = request.UserName,
            UserEmail = request.UserEmail,
            PlanId = plan.Id,
            ProductId = plan.ProductId,
            Status = "Active",
            StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
            EndDate = request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc) : null,
            Notes = request.Notes,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        db.UserSubscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);

        var product = await db.Products.AsNoTracking().SingleAsync(item => item.Id == plan.ProductId, cancellationToken);
        return ToSummary(subscription, plan, product);
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

        if (plan.Plan.Price <= 0)
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

    private static ProductSummary ToSummary(LegacyProduct product) =>
        new(product.Id, product.Slug, product.Name, product.Description, Deserialize(product.IncludedProductSlugsJson), product.IsActive, product.IsPublic, product.SortOrder);

    private static SubscriptionPlanSummary ToSummary(LegacySubscriptionPlan plan, LegacyProduct product)
    {
        var included = Deserialize(product.IncludedProductSlugsJson);
        var allSlugs = new HashSet<string>(included, StringComparer.OrdinalIgnoreCase) { product.Slug };
        var modules = new List<string>();
        if (allSlugs.Contains("hizliokuma")) modules.Add("SpeedReading");
        if (allSlugs.Contains("kocluk")) modules.Add("Coaching");
        return new SubscriptionPlanSummary(plan.Id, plan.Name, plan.Description, plan.Slug, plan.ProductId, product.Slug, product.Name, included, modules, plan.Price, plan.BillingPeriod, plan.DurationDays, plan.IsActive, plan.IsPublic, plan.SortOrder, Deserialize(plan.Features));
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
