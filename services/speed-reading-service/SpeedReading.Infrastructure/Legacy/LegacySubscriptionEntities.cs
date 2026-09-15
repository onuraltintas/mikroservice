namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacyProduct
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IncludedProductSlugsJson { get; set; } = "[]";
    public bool IsActive { get; set; }
    public bool IsPublic { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

internal sealed class LegacySubscriptionPlan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public decimal Price { get; set; }
    public bool IsContactOnly { get; set; }
    public string BillingPeriod { get; set; } = string.Empty;
    public int? DurationDays { get; set; }
    public bool IsActive { get; set; }
    public bool IsPublic { get; set; }
    public int SortOrder { get; set; }
    public string? Features { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

internal sealed class LegacyUserSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public Guid PlanId { get; set; }
    public Guid ProductId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
    public Guid? InstitutionAccessLicenseId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

internal sealed class LegacyInstitutionAccessLicense
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid PlanId { get; set; }
    public string Status { get; set; } = "Active";
    public int SeatCount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
    public Guid ApprovedBy { get; set; }
    public DateTime ApprovedAt { get; set; }
}

internal sealed class LegacyInstitutionAccessAction
{
    public Guid Id { get; set; }
    public Guid InstitutionAccessLicenseId { get; set; }
    public Guid StudentId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public Guid PerformedBy { get; set; }
    public DateTime PerformedAt { get; set; }
}

internal sealed class LegacyPayment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? ProviderToken { get; set; }
    public string? ProviderPaymentId { get; set; }
    public string? ProviderResponse { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? SubscriptionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

internal sealed class LegacyBankTransferPaymentSettings
{
    public Guid Id { get; set; }
    public string AccountHolder { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public bool IsEnabled { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

internal sealed class LegacyBankTransferPaymentRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string PaymentReference { get; set; } = string.Empty;
    public string? PayerName { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "Pending";
    public Guid? SubscriptionId { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
