using System.Security.Claims;
using Billing.Application;
using Microsoft.AspNetCore.Mvc;

namespace Billing.API;

public static class AdministrationEndpoints
{
    public static void MapBillingAdministrationEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/billing/v1/admin").RequireAuthorization(BillingPolicies.Admin);
        admin.MapGet("/overview", ([FromQuery] string? product, IBillingAdministration administration, CancellationToken cancellationToken) =>
            administration.GetOverviewAsync(product, cancellationToken));
        admin.MapPost("/subscriptions", async (GrantSubscriptionRequest request, ClaimsPrincipal principal, IBillingAdministration administration,
            CancellationToken cancellationToken) => Results.Ok(await administration.GrantAsync(request, UserId(principal), cancellationToken)));
        admin.MapPost("/subscriptions/{id:guid}/extend", async (Guid id, ExtendSubscriptionRequest request, ClaimsPrincipal principal,
            IBillingAdministration administration, CancellationToken cancellationToken) =>
            OkOrNotFound(await administration.ExtendAsync(id, request, UserId(principal), cancellationToken)));
        admin.MapPost("/subscriptions/{id:guid}/cancel", async (Guid id, CancelSubscriptionRequest request, ClaimsPrincipal principal,
            IBillingAdministration administration, CancellationToken cancellationToken) =>
            OkOrNotFound(await administration.CancelAsync(id, request, UserId(principal), cancellationToken)));
    }

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue("sub")!);
    private static IResult OkOrNotFound<T>(T? value) where T : class => value is null ? Results.NotFound() : Results.Ok(value);
}
