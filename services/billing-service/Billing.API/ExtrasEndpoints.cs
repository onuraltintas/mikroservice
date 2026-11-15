using System.Globalization;
using System.Security.Claims;
using Billing.Application;
using Billing.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Billing.API;

/// <summary>Coupons, free trials, withdrawal (cayma) and invoices: the buyer's side and the admin's side.</summary>
public static class ExtrasEndpoints
{
    public static void MapBillingExtrasEndpoints(this WebApplication app)
    {
        var user = app.MapGroup("/api/billing/v1").RequireAuthorization(BillingPolicies.User);
        user.MapPost("/coupons/quote", async (CouponQuoteRequest request, ClaimsPrincipal principal, IBillingExtras extras, CancellationToken cancellationToken) =>
            Results.Ok(await extras.QuoteCouponAsync(User(principal), request, cancellationToken)));
        user.MapPost("/trials", async (StartTrialRequest request, ClaimsPrincipal principal, IBillingExtras extras, CancellationToken cancellationToken) =>
            Results.Ok(await extras.StartTrialAsync(User(principal), request, cancellationToken)));
        user.MapPost("/orders/{id:guid}/withdrawal", async (Guid id, WithdrawalRequest? request, ClaimsPrincipal principal, IBillingExtras extras,
            CancellationToken cancellationToken) =>
            OkOrNotFound(await extras.RequestWithdrawalAsync(UserId(principal), id, request?.Reason, cancellationToken)));
        user.MapGet("/orders/{id:guid}/invoice", async (Guid id, ClaimsPrincipal principal, IBillingExtras extras, CancellationToken cancellationToken) =>
            File(await extras.GetInvoiceAsync(UserId(principal), id, cancellationToken)));

        var admin = app.MapGroup("/api/billing/v1/admin").RequireAuthorization(BillingPolicies.Admin);
        admin.MapGet("/coupons", (IBillingExtras extras, CancellationToken cancellationToken) => extras.GetCouponsAsync(cancellationToken));
        admin.MapPost("/coupons", async (CouponRequest request, ClaimsPrincipal principal, IBillingExtras extras, CancellationToken cancellationToken) =>
            Results.Ok(await extras.CreateCouponAsync(request, UserId(principal), cancellationToken)));
        admin.MapPut("/coupons/{id:guid}", async (Guid id, CouponRequest request, ClaimsPrincipal principal, IBillingExtras extras, CancellationToken cancellationToken) =>
            OkOrNotFound(await extras.UpdateCouponAsync(id, request, UserId(principal), cancellationToken)));

        admin.MapGet("/withdrawals", ([FromQuery] WithdrawalStatus? status, IBillingExtras extras, CancellationToken cancellationToken) =>
            extras.GetWithdrawalsAsync(status, cancellationToken));
        admin.MapPost("/withdrawals/{id:guid}/review", async (Guid id, ReviewWithdrawalRequest request, ClaimsPrincipal principal, IBillingExtras extras,
            CancellationToken cancellationToken) =>
            OkOrNotFound(await extras.ReviewWithdrawalAsync(id, request, UserId(principal), cancellationToken)));

        admin.MapPost("/orders/{id:guid}/invoice", async (Guid id, HttpRequest request, ClaimsPrincipal principal, IBillingExtras extras,
            CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType) return Invalid("Fatura dosyasını seçin (PDF, en çok 10 MB).");
            var form = await request.ReadFormAsync(cancellationToken);
            if (form.Files.GetFile("file") is not { Length: > 0 and <= ExtrasRules.MaxInvoiceBytes } file) return Invalid("Fatura dosyasını seçin (PDF, en çok 10 MB).");
            if (!DateOnly.TryParseExact(form["issuedOn"].ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var issuedOn))
                return Invalid("Fatura tarihini seçin.");
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            return OkOrNotFound(await extras.AttachInvoiceAsync(id, new InvoiceUpload(form["number"].ToString(), issuedOn, file.FileName, buffer.ToArray()),
                UserId(principal), cancellationToken));
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(ExtrasRules.MaxInvoiceBytes + 64 * 1024));
        admin.MapGet("/orders/{id:guid}/invoice", async (Guid id, IBillingExtras extras, CancellationToken cancellationToken) =>
            File(await extras.GetInvoiceAsync(null, id, cancellationToken)));
        admin.MapDelete("/orders/{id:guid}/invoice", async (Guid id, IBillingExtras extras, CancellationToken cancellationToken) =>
            await extras.RemoveInvoiceAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());
    }

    private static IResult Invalid(string detail) => Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: detail,
        extensions: new Dictionary<string, object?> { ["code"] = "Billing.InvoiceInvalid" });

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue("sub")!);

    private static BillingUser User(ClaimsPrincipal principal) =>
        new(UserId(principal), principal.FindFirstValue("email"), principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray());

    private static IResult OkOrNotFound<T>(T? value) where T : class => value is null ? Results.NotFound() : Results.Ok(value);

    private static IResult File(InvoiceFile? file) =>
        file is null ? Results.NotFound() : Results.File(file.Content, "application/pdf", file.FileName);
}
