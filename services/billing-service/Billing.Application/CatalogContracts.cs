using Billing.Domain;

namespace Billing.Application;

public sealed record EntitlementView(Guid Id, string Key, string Name, bool HasLimit);

public sealed record ProductView(
    Guid Id,
    string Code,
    string Name,
    bool RequireSubscription,
    BankTransferApproval BankTransferApproval,
    bool IsActive,
    int SortOrder,
    IReadOnlyList<EntitlementView> Entitlements,
    /// <summary>Sold to businesses: business contract, no withdrawal (see <see cref="Product.BusinessSales"/>).</summary>
    bool BusinessSales = false,
    /// <summary>Students free, teachers and institutions pay (see <see cref="Product.StaffSeatsRequired"/>).</summary>
    bool StaffSeatsRequired = false);

public sealed record ProductRequest(string Code, string Name, bool IsActive, int SortOrder);

/// <summary>The two billing settings of a product, changed on their own so a catalog edit never flips them by accident.</summary>
public sealed record ProductSettingsRequest(bool RequireSubscription, BankTransferApproval BankTransferApproval,
    /// <summary>Null leaves the setting as it is (older screens do not send it).</summary>
    bool? BusinessSales = null,
    /// <summary>Null leaves the setting as it is (older screens do not send it).</summary>
    bool? StaffSeatsRequired = null);

public sealed record EntitlementRequest(string Key, string Name, bool HasLimit);

public sealed record PlanEntitlementView(Guid EntitlementId, string Key, string Name, string ProductCode, EntitlementHolder Holder, int? LimitValue);

public sealed record PlanEntitlementRequest(Guid EntitlementId, EntitlementHolder Holder, int? LimitValue);

public sealed record PlanView(
    Guid Id,
    string Name,
    string Description,
    BuyerType BuyerType,
    decimal Price,
    string Currency,
    int DurationDays,
    int TrialDays,
    int? SeatCount,
    decimal? ExtraSeatPrice,
    bool IsContactOnly,
    bool IsActive,
    bool IsPublic,
    int SortOrder,
    IReadOnlyList<string> Features,
    IReadOnlyList<PlanEntitlementView> Entitlements,
    /// <summary>The products whose entitlements the plan gives; more than one is a bundle.</summary>
    IReadOnlyList<string> ProductCodes,
    /// <summary>Null: the price includes taxes. A rate: the price is net and VAT at this percentage is added.</summary>
    decimal? VatRate = null,
    /// <summary>What a buyer pays for the plan: the price plus VAT.</summary>
    decimal PriceWithVat = 0);

public sealed record PlanRequest(
    string Name,
    string? Description,
    BuyerType BuyerType,
    decimal Price,
    int DurationDays,
    int TrialDays,
    int? SeatCount,
    decimal? ExtraSeatPrice,
    bool IsContactOnly,
    bool IsActive,
    bool IsPublic,
    int SortOrder,
    IReadOnlyList<string>? Features,
    IReadOnlyList<PlanEntitlementRequest>? Entitlements,
    decimal? VatRate = null);

public sealed record SellerView(
    string? Title,
    string? Address,
    string? TaxOffice,
    string? TaxNumber,
    string? MersisNumber,
    string? Email,
    string? Phone,
    string? AlertEmail,
    bool IsComplete);

public sealed record SellerRequest(
    string? Title,
    string? Address,
    string? TaxOffice,
    string? TaxNumber,
    string? MersisNumber,
    string? Email,
    string? Phone,
    string? AlertEmail);

public sealed record BankAccountView(
    Guid Id,
    string BankName,
    string AccountHolder,
    string Iban,
    string IbanFormatted,
    string? Instructions,
    string? StatementTemplate,
    bool IsActive,
    int SortOrder);

public sealed record BankAccountRequest(
    string BankName,
    string AccountHolder,
    string Iban,
    string? Instructions,
    string? StatementTemplate,
    bool IsActive,
    int SortOrder);

/// <summary>What a buyer may know about a bank account.</summary>
public sealed record PublicBankAccount(string BankName, string AccountHolder, string Iban, string IbanFormatted, string? Instructions);

/// <summary>What a product page shows before anybody signs in.</summary>
public sealed record PublicCatalog(
    string ProductCode,
    string ProductName,
    bool RequireSubscription,
    /// <summary>Plans can be ordered: the seller details are complete and at least one bank account is open.</summary>
    bool SalesOpen,
    IReadOnlyList<PlanView> Plans,
    IReadOnlyList<PublicBankAccount> BankAccounts,
    bool BusinessSales = false,
    /// <summary>Students free, teachers and institutions pay: only their plans are listed and sold.</summary>
    bool StaffSeatsRequired = false);

/// <summary>The catalog and settings of the billing service (stage 1 of billing-service-plan.md).</summary>
public interface IBillingCatalog
{
    Task<IReadOnlyList<ProductView>> GetProductsAsync(CancellationToken cancellationToken = default);
    Task<ProductView> CreateProductAsync(ProductRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<ProductView?> UpdateProductAsync(Guid id, ProductRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<ProductView?> UpdateProductSettingsAsync(Guid id, ProductSettingsRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<ProductView?> AddEntitlementAsync(Guid productId, EntitlementRequest request, CancellationToken cancellationToken = default);
    Task<ProductView?> UpdateEntitlementAsync(Guid entitlementId, EntitlementRequest request, CancellationToken cancellationToken = default);
    /// <summary>False when the entitlement is unknown; a business rule error when a plan still gives it.</summary>
    Task<bool> RemoveEntitlementAsync(Guid entitlementId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanView>> GetPlansAsync(CancellationToken cancellationToken = default);
    Task<PlanView> CreatePlanAsync(PlanRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<PlanView?> UpdatePlanAsync(Guid id, PlanRequest request, Guid actorId, CancellationToken cancellationToken = default);

    Task<SellerView> GetSellerAsync(CancellationToken cancellationToken = default);
    Task<SellerView> UpdateSellerAsync(SellerRequest request, Guid actorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BankAccountView>> GetBankAccountsAsync(CancellationToken cancellationToken = default);
    Task<BankAccountView> CreateBankAccountAsync(BankAccountRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<BankAccountView?> UpdateBankAccountAsync(Guid id, BankAccountRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> RemoveBankAccountAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Null when the product is unknown or inactive.</summary>
    Task<PublicCatalog?> GetPublicCatalogAsync(string productCode, CancellationToken cancellationToken = default);
}
