namespace Billing.Domain;

/// <summary>How a bank-transfer payment of a product's orders is approved (user decision 14.11.2026: per product).</summary>
public enum BankTransferApproval
{
    /// <summary>Every order waits in the queue for an admin.</summary>
    Manual = 0,
    /// <summary>An order whose bank transaction matches exactly is approved by itself; the rest wait for an admin.</summary>
    Automatic = 1
}

/// <summary>Who buys a plan. A product sells to a kind of buyer only when it has a plan for that kind.</summary>
public enum BuyerType
{
    Individual = 0,
    Teacher = 1,
    Institution = 2
}

/// <summary>Who gets an entitlement of a plan: the buyer, or each user a seat of the subscription is given to.</summary>
public enum EntitlementHolder
{
    Buyer = 0,
    SeatHolder = 1
}

/// <summary>
/// Something sold on the platform (Koçluk, Hızlı Okuma, Kurs Yönetimi, later products). The product carries its two
/// billing settings: whether a subscription is needed at all, and how its bank transfers are approved.
/// </summary>
public sealed class Product
{
    public Guid Id { get; set; }
    /// <summary>Stable key the product service uses when it asks for entitlements, e.g. "coaching".</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Off: everybody with an account uses the product and nothing is sold. On: an entitlement is needed.</summary>
    public bool RequireSubscription { get; set; }
    public BankTransferApproval BankTransferApproval { get; set; }
    /// <summary>
    /// Sold to businesses (maxkampüs, user decision 20.11.2026): buyers read a business subscription contract instead of
    /// the consumer pre-information form and distance-sales contract, and there is no withdrawal (cayma).
    /// </summary>
    public bool BusinessSales { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>An access key a product understands, e.g. "coaching.student". Products check entitlements, never plan names.</summary>
public sealed class Entitlement
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>The entitlement carries a number (e.g. active student limit) set per plan.</summary>
    public bool HasLimit { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// A price for a period. Three settings make every sales model optional per product: who buys it, whether it has
/// seats, and which entitlements it gives. Entitlements of more than one product make a bundle.
/// </summary>
public sealed class Plan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BuyerType BuyerType { get; set; }
    public decimal Price { get; set; }
    /// <summary>
    /// Null: <see cref="Price"/> is what the buyer pays, taxes included (Koçluk, Hızlı Okuma). A rate (e.g. 20): the price
    /// is net and VAT at this percentage is added to every order of the plan (user decision 20.11.2026).
    /// </summary>
    public decimal? VatRate { get; set; }
    public string Currency { get; set; } = BillingRules.DefaultCurrency;
    public int DurationDays { get; set; }
    /// <summary>Free trial days offered once per account; 0 = none. Not for institution plans.</summary>
    public int TrialDays { get; set; }
    /// <summary>Seats included in the plan; null = a plan without seats.</summary>
    public int? SeatCount { get; set; }
    /// <summary>Price of one extra seat for the whole period, on teacher and institution plans; null = no extra seats.</summary>
    public decimal? ExtraSeatPrice { get; set; }
    /// <summary>Shown with "contact us"; cannot be bought online.</summary>
    public bool IsContactOnly { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = true;
    public int SortOrder { get; set; }
    /// <summary>JSON array of the selling points shown on the plan card.</summary>
    public string FeaturesJson { get; set; } = "[]";
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class PlanEntitlement
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid EntitlementId { get; set; }
    public EntitlementHolder Holder { get; set; }
    /// <summary>The number for an entitlement with a limit; null otherwise.</summary>
    public int? LimitValue { get; set; }
}

/// <summary>The seller named at the top of the pre-information form and the distance-sales contract. One row.</summary>
public sealed class Seller
{
    public const int SingleId = 1;
    public int Id { get; set; } = SingleId;
    public string? Title { get; set; }
    public string? Address { get; set; }
    public string? TaxOffice { get; set; }
    public string? TaxNumber { get; set; }
    public string? MersisNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    /// <summary>Where new orders and withdrawal requests are announced; empty sends nothing.</summary>
    public string? AlertEmail { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>A contract with a missing seller line is not a contract: everything but MERSİS is needed.</summary>
    public bool IsComplete => !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Address)
        && !string.IsNullOrWhiteSpace(TaxOffice) && !string.IsNullOrWhiteSpace(TaxNumber)
        && !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Phone);
}

/// <summary>A bank account buyers can pay into. There can be several (user decision 14.11.2026).</summary>
public sealed class BankAccount
{
    public Guid Id { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    /// <summary>Stored without spaces, e.g. TR330006100519786457841326.</summary>
    public string Iban { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    /// <summary>Which statement reader fits this bank's export (e.g. "Halkbank"); null = columns are mapped by hand.</summary>
    public string? StatementTemplate { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
