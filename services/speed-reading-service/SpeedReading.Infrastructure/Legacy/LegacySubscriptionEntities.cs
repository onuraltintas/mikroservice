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
    public bool RequireActiveSubscription { get; set; } = true;
    public string AccountHolder { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public bool IsEnabled { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    /// <summary>Legal check Y1: the seller block of the sales documents. A sale needs all but MERSİS.</summary>
    public string? SellerTitle { get; set; }
    public string? SellerAddress { get; set; }
    public string? SellerTaxOffice { get; set; }
    public string? SellerTaxNumber { get; set; }
    public string? SellerMersisNumber { get; set; }
    public string? SellerEmail { get; set; }
    public string? SellerPhone { get; set; }
}

internal sealed class LegacyBankTransferPaymentRequest
{
    /// <summary>Legal check Y1: the documents the buyer accepted, exactly as shown (JSON of SpeedReadingSalesDocuments).</summary>
    public string? SalesDocumentsVersion { get; set; }
    public string? SalesDocumentsJson { get; set; }
    public DateTime? SalesDocumentsAcceptedAt { get; set; }
    public string? WithdrawalStatus { get; set; }
    public string? WithdrawalReason { get; set; }
    public DateTime? WithdrawalRequestedAt { get; set; }
    public string? WithdrawalReviewNote { get; set; }
    public DateTime? WithdrawalReviewedAt { get; set; }
    public Guid? WithdrawalReviewedBy { get; set; }
    /// <summary>Copied from the invoice record so lists need no join; null while no invoice is recorded.</summary>
    public string? InvoiceNumber { get; set; }
    public DateOnly? InvoiceIssuedOn { get; set; }
    public int? AdultPayerDeclarationVersion { get; set; }
    public DateTime? AdultPayerDeclaredAt { get; set; }
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

/// <summary>Billing parity (14.11.2026): the manually issued invoice (PDF) of an approved bank-transfer payment.</summary>
internal sealed class LegacySubscriptionInvoice
{
    public Guid Id { get; set; }
    public Guid PaymentRequestId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly IssuedOn { get; set; }
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Billing parity (14.11.2026): one row per expiry reminder sent, so no reminder goes out twice.</summary>
internal sealed class LegacySubscriptionNoticeReceipt
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    /// <summary>"7d", "1d" or "expired", with the end date it was sent for (an extended subscription is reminded again).</summary>
    public string Stage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
