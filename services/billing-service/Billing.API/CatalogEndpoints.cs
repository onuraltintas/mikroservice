using System.Security.Claims;
using Billing.Application;

namespace Billing.API;

public static class BillingPolicies
{
    public const string Admin = "BillingAdmin";
    /// <summary>Any signed-in user: a buyer manages only their own orders.</summary>
    public const string User = "BillingUser";
}

/// <summary>
/// The catalog and settings pages of the admin section "Abonelik ve Ödeme", and the catalog a product page shows
/// before anybody signs in.
/// </summary>
public static class CatalogEndpoints
{
    public static void MapBillingCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/billing/v1/catalog/{productCode}", async (string productCode, IBillingCatalog catalog, CancellationToken cancellationToken) =>
            await catalog.GetPublicCatalogAsync(productCode, cancellationToken) is { } result ? Results.Ok(result) : Results.NotFound())
            .AllowAnonymous();

        var admin = app.MapGroup("/api/billing/v1/admin").RequireAuthorization(BillingPolicies.Admin);

        admin.MapGet("/products", (IBillingCatalog catalog, CancellationToken cancellationToken) => catalog.GetProductsAsync(cancellationToken));
        admin.MapPost("/products", async (ProductRequest request, ClaimsPrincipal user, IBillingCatalog catalog, CancellationToken cancellationToken) =>
            Results.Ok(await catalog.CreateProductAsync(request, Actor(user), cancellationToken)));
        admin.MapPut("/products/{id:guid}", async (Guid id, ProductRequest request, ClaimsPrincipal user, IBillingCatalog catalog, CancellationToken cancellationToken) =>
            OkOrNotFound(await catalog.UpdateProductAsync(id, request, Actor(user), cancellationToken)));
        admin.MapPut("/products/{id:guid}/settings", async (Guid id, ProductSettingsRequest request, ClaimsPrincipal user, IBillingCatalog catalog, CancellationToken cancellationToken) =>
            OkOrNotFound(await catalog.UpdateProductSettingsAsync(id, request, Actor(user), cancellationToken)));
        admin.MapPost("/products/{id:guid}/entitlements", async (Guid id, EntitlementRequest request, IBillingCatalog catalog, CancellationToken cancellationToken) =>
            OkOrNotFound(await catalog.AddEntitlementAsync(id, request, cancellationToken)));
        admin.MapPut("/entitlements/{id:guid}", async (Guid id, EntitlementRequest request, IBillingCatalog catalog, CancellationToken cancellationToken) =>
            OkOrNotFound(await catalog.UpdateEntitlementAsync(id, request, cancellationToken)));
        admin.MapDelete("/entitlements/{id:guid}", async (Guid id, IBillingCatalog catalog, CancellationToken cancellationToken) =>
            await catalog.RemoveEntitlementAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());

        admin.MapGet("/plans", (IBillingCatalog catalog, CancellationToken cancellationToken) => catalog.GetPlansAsync(cancellationToken));
        admin.MapPost("/plans", async (PlanRequest request, ClaimsPrincipal user, IBillingCatalog catalog, CancellationToken cancellationToken) =>
            Results.Ok(await catalog.CreatePlanAsync(request, Actor(user), cancellationToken)));
        admin.MapPut("/plans/{id:guid}", async (Guid id, PlanRequest request, ClaimsPrincipal user, IBillingCatalog catalog, CancellationToken cancellationToken) =>
            OkOrNotFound(await catalog.UpdatePlanAsync(id, request, Actor(user), cancellationToken)));

        admin.MapGet("/seller", (IBillingCatalog catalog, CancellationToken cancellationToken) => catalog.GetSellerAsync(cancellationToken));
        admin.MapPut("/seller", (SellerRequest request, ClaimsPrincipal user, IBillingCatalog catalog, CancellationToken cancellationToken) =>
            catalog.UpdateSellerAsync(request, Actor(user), cancellationToken));

        admin.MapGet("/bank-accounts", (IBillingCatalog catalog, CancellationToken cancellationToken) => catalog.GetBankAccountsAsync(cancellationToken));
        admin.MapPost("/bank-accounts", async (BankAccountRequest request, ClaimsPrincipal user, IBillingCatalog catalog, CancellationToken cancellationToken) =>
            Results.Ok(await catalog.CreateBankAccountAsync(request, Actor(user), cancellationToken)));
        admin.MapPut("/bank-accounts/{id:guid}", async (Guid id, BankAccountRequest request, ClaimsPrincipal user, IBillingCatalog catalog, CancellationToken cancellationToken) =>
            OkOrNotFound(await catalog.UpdateBankAccountAsync(id, request, Actor(user), cancellationToken)));
        admin.MapDelete("/bank-accounts/{id:guid}", async (Guid id, IBillingCatalog catalog, CancellationToken cancellationToken) =>
            await catalog.RemoveBankAccountAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());
    }

    private static Guid Actor(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);

    private static IResult OkOrNotFound<T>(T? value) where T : class => value is null ? Results.NotFound() : Results.Ok(value);
}
