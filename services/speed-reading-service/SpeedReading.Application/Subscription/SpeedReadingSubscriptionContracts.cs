using SpeedReading.Application.Content;

namespace SpeedReading.Application.Subscription;

public sealed record ProductSummary(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    IReadOnlyList<string> IncludedProductSlugs,
    bool IsActive,
    bool IsPublic,
    int SortOrder);

public sealed record SubscriptionPlanSummary(
    Guid Id,
    string Name,
    string Description,
    string Slug,
    Guid ProductId,
    string ProductSlug,
    string ProductName,
    IReadOnlyList<string> IncludedProductSlugs,
    IReadOnlyList<string> Modules,
    decimal Price,
    string BillingPeriod,
    int? DurationDays,
    bool IsActive,
    bool IsPublic,
    int SortOrder,
    IReadOnlyList<string> Features);

public sealed record UserSubscriptionSummary(
    Guid Id,
    Guid UserId,
    string? UserName,
    string? UserEmail,
    SubscriptionPlanSummary Plan,
    string ProductSlug,
    string ProductName,
    string Status,
    DateTime StartDate,
    DateTime? EndDate,
    string? Notes,
    DateTime CreatedAt,
    bool IsActive);

public sealed record UserAccessSummary(
    IReadOnlyList<string> Products,
    bool HasSpeedReading,
    bool HasCoaching);

public sealed record PaymentSummary(
    Guid Id,
    Guid UserId,
    string UserEmail,
    string UserName,
    string PlanName,
    decimal Amount,
    string Currency,
    string Status,
    string Provider,
    string? ProviderPaymentId,
    string? ErrorMessage,
    Guid? SubscriptionId,
    DateTime CreatedAt);

public sealed record CreateProductRequest(
    string Slug,
    string Name,
    string Description,
    IReadOnlyList<string>? IncludedProductSlugs,
    bool IsActive,
    bool IsPublic,
    int SortOrder);

public sealed record UpdateProductRequest(
    string? Name,
    string? Description,
    IReadOnlyList<string>? IncludedProductSlugs,
    bool? IsActive,
    bool? IsPublic,
    int? SortOrder);

public sealed record CreateSubscriptionPlanRequest(
    string Name,
    string Description,
    string Slug,
    Guid ProductId,
    decimal Price,
    string BillingPeriod,
    int? DurationDays,
    bool IsActive,
    bool IsPublic,
    int SortOrder,
    IReadOnlyList<string>? Features);

public sealed record UpdateSubscriptionPlanRequest(
    string? Name,
    string? Description,
    decimal? Price,
    string? BillingPeriod,
    int? DurationDays,
    bool? IsActive,
    bool? IsPublic,
    int? SortOrder,
    IReadOnlyList<string>? Features);

public sealed record CreateUserSubscriptionRequest(
    Guid UserId,
    string? UserName,
    string? UserEmail,
    Guid PlanId,
    DateTime StartDate,
    DateTime? EndDate,
    string? Notes);

public sealed record UpdateUserSubscriptionRequest(
    string Status,
    DateTime? EndDate,
    string? Notes);

public sealed record InitializePaymentRequest(
    Guid PlanId,
    string? PhoneNumber,
    string? IdentityNumber,
    string? BillingAddress,
    string? City,
    string? ZipCode);

public sealed record PaymentBuyerInfo(
    string Id,
    string Name,
    string Surname,
    string Email,
    string PhoneNumber,
    string IdentityNumber,
    string BillingAddress,
    string City,
    string Country,
    string ZipCode,
    string IpAddress);

public sealed record PaymentProviderInitializationRequest(
    string ConversationId,
    string BasketId,
    string Locale,
    decimal Price,
    string Currency,
    string CallbackUrl,
    string ItemName,
    PaymentBuyerInfo Buyer);

public sealed record PaymentProviderInitializationResult(
    bool Succeeded,
    string? Token,
    string? PaymentPageUrl,
    string? CheckoutFormContent,
    string? ErrorMessage,
    string RawResponse);

public sealed record PaymentProviderRetrieveRequest(
    string ConversationId,
    string Token,
    string Locale);

public sealed record PaymentProviderRetrieveResult(
    bool RequestSucceeded,
    bool ResponseSignatureValid,
    string? ProviderStatus,
    int? FraudStatus,
    string? PaymentId,
    string? Currency,
    string? BasketId,
    string? ConversationId,
    decimal? Price,
    decimal? PaidPrice,
    string? Token,
    string? ErrorMessage,
    string RawResponse);

public interface ISpeedReadingPaymentProvider
{
    bool IsConfigured { get; }

    Task<PaymentProviderInitializationResult> InitializeAsync(
        PaymentProviderInitializationRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentProviderRetrieveResult> RetrieveAsync(
        PaymentProviderRetrieveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentInitializationResult(
    bool Available,
    bool Succeeded,
    string? Token,
    string? PaymentPageUrl,
    string? CheckoutFormContent,
    string? Message);

public sealed record PaymentVerificationResult(
    bool Available,
    bool Success,
    string Status,
    string? PlanName,
    decimal Amount,
    Guid? SubscriptionId,
    string? Message);

public interface ISpeedReadingSubscription
{
    Task<IReadOnlyList<ProductSummary>> GetProductsAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<ProductSummary?> GetProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateProductAsync(CreateProductRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<ProductSummary?> UpdateProductAsync(Guid id, UpdateProductRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> DeactivateProductAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionPlanSummary>> GetPlansAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<SubscriptionPlanSummary?> GetPlanAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid?> CreatePlanAsync(CreateSubscriptionPlanRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<SubscriptionPlanSummary?> UpdatePlanAsync(Guid id, UpdateSubscriptionPlanRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> DeactivatePlanAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);

    Task<SpeedReadingPage<UserSubscriptionSummary>> GetSubscriptionsAsync(string? search, string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSubscriptionSummary>> GetUserSubscriptionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserSubscriptionSummary?> CreateSubscriptionAsync(CreateUserSubscriptionRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<UserSubscriptionSummary?> UpdateSubscriptionAsync(Guid id, UpdateUserSubscriptionRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSubscriptionAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSubscriptionSummary>> GetMySubscriptionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserAccessSummary> GetMyAccessAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PaymentInitializationResult> InitializePaymentAsync(
        Guid userId,
        InitializePaymentRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);
    Task<PaymentVerificationResult> VerifyPaymentAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default);
    Task<PaymentVerificationResult> ProcessPaymentCallbackAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<SpeedReadingPage<PaymentSummary>> GetPaymentsAsync(int page, int pageSize, string? status, string? search, CancellationToken cancellationToken = default);
}
