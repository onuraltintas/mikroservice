using System.Security.Claims;
using Billing.Application;
using Billing.Domain;
using EduPlatform.Shared.Security.Services;
using Microsoft.AspNetCore.Mvc;

namespace Billing.API;

/// <summary>
/// Buying by bank transfer: the signed-in buyer's endpoints, the admin's review queue, and the entitlement query the
/// product services call inside the network.
/// </summary>
public static class OrderEndpoints
{
    public static void MapBillingOrderEndpoints(this WebApplication app)
    {
        var user = app.MapGroup("/api/billing/v1").RequireAuthorization(BillingPolicies.User);

        user.MapGet("/me", async ([FromQuery] string product, ClaimsPrincipal principal, IBillingOrders orders, CancellationToken cancellationToken) =>
            OkOrNotFound(await orders.GetMyBillingAsync(UserId(principal), product, cancellationToken)));
        user.MapGet("/documents", async ([FromQuery] Guid planId, [FromQuery] string? buyerName, [FromQuery] string? coupon, ClaimsPrincipal principal,
            IBillingOrders orders, CancellationToken cancellationToken) =>
            OkOrNotFound(await orders.PreviewDocumentsAsync(User(principal), planId, buyerName, coupon, cancellationToken)));
        user.MapGet("/orders", (ClaimsPrincipal principal, IBillingOrders orders, CancellationToken cancellationToken) =>
            orders.GetMyOrdersAsync(UserId(principal), cancellationToken));
        user.MapPost("/orders", async (CreateOrderRequest request, ClaimsPrincipal principal, IBillingOrders orders, CancellationToken cancellationToken) =>
            Results.Ok(await orders.CreateOrderAsync(User(principal), request, cancellationToken)));
        // "I have paid": a form, because a receipt may come with it.
        user.MapPost("/orders/{id:guid}/paid", async (Guid id, HttpRequest request, ClaimsPrincipal principal, IBillingOrders orders,
            CancellationToken cancellationToken) =>
        {
            string? note = null;
            ReceiptUpload? receipt = null;
            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(cancellationToken);
                note = form["note"].ToString();
                if (form.Files.GetFile("receipt") is { Length: > 0 } file)
                {
                    if (file.Length > BillingRules.MaxReceiptBytes) return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        detail: "Dekont en fazla 5 MB olabilir.", extensions: new Dictionary<string, object?> { ["code"] = "Billing.ReceiptInvalid" });
                    await using var stream = file.OpenReadStream();
                    using var buffer = new MemoryStream();
                    await stream.CopyToAsync(buffer, cancellationToken);
                    receipt = new ReceiptUpload(file.FileName, buffer.ToArray());
                }
            }
            return OkOrNotFound(await orders.DeclarePaidAsync(UserId(principal), id, note, receipt, cancellationToken));
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(BillingRules.MaxReceiptBytes + 64 * 1024));
        user.MapDelete("/orders/{id:guid}", async (Guid id, ClaimsPrincipal principal, IBillingOrders orders, CancellationToken cancellationToken) =>
            OkOrNotFound(await orders.WithdrawOrderAsync(UserId(principal), id, cancellationToken)));
        user.MapGet("/orders/{id:guid}/documents", async (Guid id, ClaimsPrincipal principal, IBillingOrders orders, CancellationToken cancellationToken) =>
            OkOrNotFound(await orders.GetAcceptedDocumentsAsync(UserId(principal), id, cancellationToken)));
        user.MapGet("/orders/{id:guid}/receipt", async (Guid id, ClaimsPrincipal principal, IBillingOrders orders, CancellationToken cancellationToken) =>
            File(await orders.GetReceiptAsync(UserId(principal), id, cancellationToken)));

        var admin = app.MapGroup("/api/billing/v1/admin").RequireAuthorization(BillingPolicies.Admin);

        admin.MapGet("/orders", ([FromQuery] OrderStatus? status, [FromQuery] string? product, IBillingOrders orders, CancellationToken cancellationToken) =>
            orders.GetOrdersAsync(status, product, cancellationToken));
        admin.MapPost("/orders/{id:guid}/approve", async (Guid id, ReviewOrderRequest? request, ClaimsPrincipal principal, IBillingOrders orders,
            CancellationToken cancellationToken) =>
            OkOrNotFound(await orders.ApproveOrderAsync(id, UserId(principal), request?.Note, cancellationToken)));
        admin.MapPost("/orders/{id:guid}/reject", async (Guid id, ReviewOrderRequest request, ClaimsPrincipal principal, IBillingOrders orders,
            CancellationToken cancellationToken) =>
            OkOrNotFound(await orders.RejectOrderAsync(id, UserId(principal), request.Note, cancellationToken)));
        admin.MapPost("/orders/{id:guid}/reverse", async (Guid id, ReviewOrderRequest request, ClaimsPrincipal principal, IBillingOrders orders,
            CancellationToken cancellationToken) =>
            OkOrNotFound(await orders.ReverseApprovalAsync(id, UserId(principal), request.Note, cancellationToken)));
        admin.MapGet("/orders/{id:guid}/receipt", async (Guid id, IBillingOrders orders, CancellationToken cancellationToken) =>
            File(await orders.GetReceiptAsync(null, id, cancellationToken)));
        admin.MapGet("/subscriptions", ([FromQuery] string? product, IBillingOrders orders, CancellationToken cancellationToken) =>
            orders.GetSubscriptionsAsync(product, cancellationToken));

        // Not under /api: the gateway never routes it. Product services call it with the shared service key.
        // A holder is a user, or an organization for what an institution plan gives the institution itself.
        app.MapGet("/internal/billing/v1/entitlements", async ([FromQuery] Guid? userId, [FromQuery] Guid? organizationId, [FromQuery] string product,
            HttpRequest request, IConfiguration configuration, IBillingOrders orders, CancellationToken cancellationToken) =>
            !InternalServiceAuthentication.IsValid(request, configuration)
                ? Results.Unauthorized()
                : (userId is null) == (organizationId is null)
                    ? Results.BadRequest()
                    : OkOrNotFound(await orders.GetAccessAsync((userId ?? organizationId)!.Value, product, cancellationToken)))
            .AllowAnonymous();
    }

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue("sub")!);

    private static BillingUser User(ClaimsPrincipal principal) =>
        new(UserId(principal), principal.FindFirstValue("email"), principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray());

    private static IResult OkOrNotFound<T>(T? value) where T : class => value is null ? Results.NotFound() : Results.Ok(value);

    private static IResult File(ReceiptFile? file) =>
        file is null ? Results.NotFound() : Results.File(file.Content, file.ContentType, file.FileName);
}

/// <summary>Hourly: drops pending orders whose waiting time is over, sends the reminders that are due, redacts what outlived its retention.</summary>
public sealed class PendingOrderExpiryWorker(IServiceScopeFactory scopes, ILogger<PendingOrderExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var expired = await scope.ServiceProvider.GetRequiredService<IBillingOrders>().ExpirePendingOrdersAsync(stoppingToken);
                if (expired > 0) logger.LogInformation("Billing: {Count} pending orders expired.", expired);
                var lifecycle = scope.ServiceProvider.GetRequiredService<IBillingLifecycle>();
                var reminders = await lifecycle.SendRemindersAsync(stoppingToken);
                if (reminders > 0) logger.LogInformation("Billing: {Count} subscription reminders sent.", reminders);
                var redacted = await lifecycle.RedactExpiredAsync(stoppingToken);
                if (redacted > 0) logger.LogInformation("Billing: records of {Count} erased accounts were redacted after their retention time.", redacted);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Billing: expiring pending orders failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
