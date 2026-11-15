using System.Security.Claims;
using Billing.Application;
using EduPlatform.Shared.Security.Services;
using Microsoft.AspNetCore.Mvc;

namespace Billing.API;

/// <summary>
/// Seats: the owner (a teacher, an institution's admin) sees the pool and buys extra seats; product services give and
/// take seats inside the network, because only they know who may sit in a pool; the system admin can take a seat back.
/// </summary>
public static class SeatEndpoints
{
    public static void MapBillingSeatEndpoints(this WebApplication app)
    {
        var user = app.MapGroup("/api/billing/v1").RequireAuthorization(BillingPolicies.User);
        user.MapGet("/organizations", (ClaimsPrincipal principal, IBillingSeats seats, CancellationToken cancellationToken) =>
            seats.GetMyOrganizationsAsync(User(principal), cancellationToken));
        user.MapGet("/subscriptions/{id:guid}/seats", async (Guid id, ClaimsPrincipal principal, IBillingSeats seats, CancellationToken cancellationToken) =>
            OkOrNotFound(await seats.GetMySeatsAsync(User(principal), id, cancellationToken)));
        user.MapGet("/subscriptions/{id:guid}/seat-quote", async (Guid id, [FromQuery] int count, [FromQuery] string? buyerName, ClaimsPrincipal principal,
            IBillingSeats seats, CancellationToken cancellationToken) =>
            OkOrNotFound(await seats.QuoteSeatOrderAsync(User(principal), id, count, buyerName, cancellationToken)));
        user.MapPost("/subscriptions/{id:guid}/seat-orders", async (Guid id, CreateSeatOrderRequest request, ClaimsPrincipal principal, IBillingSeats seats,
            CancellationToken cancellationToken) =>
            OkOrNotFound(await seats.CreateSeatOrderAsync(User(principal), id, request, cancellationToken)));

        var admin = app.MapGroup("/api/billing/v1/admin").RequireAuthorization(BillingPolicies.Admin);
        admin.MapGet("/subscriptions/{id:guid}/seats", async (Guid id, IBillingSeats seats, CancellationToken cancellationToken) =>
            OkOrNotFound(await seats.GetSeatsAsync(id, cancellationToken)));
        admin.MapPost("/subscriptions/{id:guid}/seats/{userId:guid}/release", async (Guid id, Guid userId, ClaimsPrincipal principal, IBillingSeats seats,
            CancellationToken cancellationToken) =>
            OkOrNotFound(await seats.AdminReleaseAsync(id, userId, Guid.Parse(principal.FindFirstValue("sub")!), cancellationToken)));

        // Not under /api: the gateway never routes these. Product services call them with the shared service key.
        var service = app.MapGroup("/internal/billing/v1/seats").AllowAnonymous().AddEndpointFilter(async (context, next) =>
            InternalServiceAuthentication.IsValid(context.HttpContext.Request, context.HttpContext.RequestServices.GetRequiredService<IConfiguration>())
                ? await next(context)
                : Results.Unauthorized());
        service.MapGet("", async ([FromQuery] string product, [FromQuery] Guid? ownerUserId, [FromQuery] Guid? organizationId, IBillingSeats seats,
            CancellationToken cancellationToken) =>
            OkOrNotFound(await seats.GetPoolAsync(new SeatOwner(ownerUserId, organizationId), product, cancellationToken)));
        service.MapPost("/assign", (SeatChangeRequest request, IBillingSeats seats, CancellationToken cancellationToken) => seats.AssignAsync(request, cancellationToken));
        service.MapPost("/release", (SeatChangeRequest request, IBillingSeats seats, CancellationToken cancellationToken) => seats.ReleaseAsync(request, cancellationToken));
    }

    private static BillingUser User(ClaimsPrincipal principal) =>
        new(Guid.Parse(principal.FindFirstValue("sub")!), principal.FindFirstValue("email"), principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray());

    private static IResult OkOrNotFound<T>(T? value) where T : class => value is null ? Results.NotFound() : Results.Ok(value);
}
