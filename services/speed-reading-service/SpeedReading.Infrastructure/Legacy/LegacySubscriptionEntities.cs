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
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
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
