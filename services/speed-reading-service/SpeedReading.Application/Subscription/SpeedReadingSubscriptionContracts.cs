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
    bool IsContactOnly,
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

public sealed record BankTransferPaymentSettingsSummary(
    Guid? Id,
    string AccountHolder,
    string BankName,
    string Iban,
    string? Instructions,
    bool IsEnabled,
    bool IsPubliclyAvailable,
    DateTime? UpdatedAt);

public sealed record BankTransferPaymentRequestSummary(
    Guid Id,
    Guid UserId,
    string UserName,
    string UserEmail,
    Guid PlanId,
    string PlanName,
    decimal Amount,
    string Currency,
    string PaymentReference,
    string? PayerName,
    string? Note,
    string Status,
    Guid? SubscriptionId,
    DateTime CreatedAt,
    Guid? ReviewedBy,
    DateTime? ReviewedAt,
    string? ReviewNote);

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
    bool IsContactOnly,
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
    bool? IsContactOnly,
    string? BillingPeriod,
    int? DurationDays,
    bool? IsActive,
    bool? IsPublic,
    int? SortOrder,
    IReadOnlyList<string>? Features);

public sealed record UpdateBankTransferPaymentSettingsRequest(
    string AccountHolder,
    string BankName,
    string Iban,
    string? Instructions,
    bool IsEnabled);

public sealed record CreateBankTransferPaymentRequest(
    Guid PlanId,
    string PaymentReference,
    string? PayerName,
    string? Note);

public sealed record ReviewBankTransferPaymentRequest(
    string Status,
    string? ReviewNote);

public sealed record CreateUserSubscriptionRequest(
    Guid UserId,
    string? UserName,
    string? UserEmail,
    Guid PlanId,
    DateTime StartDate,
    DateTime? EndDate,
    string? Notes);

public sealed record InstitutionAccessRecipient(
    Guid UserId,
    string? UserName,
    string? UserEmail);

public sealed record CreateInstitutionAccessRequest(
    Guid InstitutionId,
    Guid PlanId,
    DateTime StartDate,
    IReadOnlyList<InstitutionAccessRecipient> Recipients,
    string? PaymentReference,
    string? Notes);

public sealed record InstitutionAccessLicenseSummary(
    Guid Id,
    Guid InstitutionId,
    SubscriptionPlanSummary Plan,
    string Status,
    int SeatCount,
    int UsedSeatCount,
    IReadOnlyList<Guid> ActiveStudentIds,
    IReadOnlyList<Guid> SuspendedStudentIds,
    DateTime StartDate,
    DateTime EndDate,
    string? PaymentReference,
    string? Notes,
    DateTime ApprovedAt);

public sealed record InstitutionStudentAccessChangeRequest(bool IsSuspended, string? Reason);

public sealed record InstitutionAccessApprovalSummary(
    int CreatedCount,
    int ExistingCount,
    IReadOnlyList<UserSubscriptionSummary> Subscriptions,
    InstitutionAccessLicenseSummary? License);

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

    Task<BankTransferPaymentSettingsSummary?> GetPublicBankTransferSettingsAsync(CancellationToken cancellationToken = default);
    Task<BankTransferPaymentSettingsSummary?> GetBankTransferSettingsAsync(CancellationToken cancellationToken = default);
    Task<BankTransferPaymentSettingsSummary?> UpdateBankTransferSettingsAsync(UpdateBankTransferPaymentSettingsRequest request, Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<BankTransferPaymentRequestSummary?> CreateBankTransferPaymentRequestAsync(Guid userId, CreateBankTransferPaymentRequest request, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BankTransferPaymentRequestSummary>> GetMyBankTransferPaymentRequestsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SpeedReadingPage<BankTransferPaymentRequestSummary>> GetBankTransferPaymentRequestsAsync(string? search, string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<BankTransferPaymentRequestSummary?> ReviewBankTransferPaymentRequestAsync(Guid id, ReviewBankTransferPaymentRequest request, Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<SpeedReadingPage<UserSubscriptionSummary>> GetSubscriptionsAsync(string? search, string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSubscriptionSummary>> GetUserSubscriptionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserSubscriptionSummary?> CreateSubscriptionAsync(CreateUserSubscriptionRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<InstitutionAccessApprovalSummary?> CreateInstitutionAccessAsync(CreateInstitutionAccessRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<InstitutionAccessLicenseSummary?> GetInstitutionAccessOverviewAsync(Guid institutionId, CancellationToken cancellationToken = default);
    Task<bool> ChangeInstitutionStudentAccessAsync(Guid institutionId, Guid studentId, InstitutionStudentAccessChangeRequest request, Guid actorId, CancellationToken cancellationToken = default);
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
