using System.Security.Claims;
using Billing.Application;
using Billing.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Billing.API;

/// <summary>Bank statements of the admin section "Abonelik ve Ödeme": upload, lines, tying a line to an order.</summary>
public static class BankStatementEndpoints
{
    public static void MapBillingBankStatementEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/billing/v1/admin").RequireAuthorization(BillingPolicies.Admin);
        var upload = new RequestSizeLimitAttribute(StatementRules.MaxFileBytes + 64 * 1024);

        // Nothing is stored: the admin sees the first rows and the guessed columns before the upload counts.
        admin.MapPost("/bank-statements/preview", async (HttpRequest request, IBillingBankStatements statements, CancellationToken cancellationToken) =>
        {
            var file = await ReadFileAsync(request, cancellationToken);
            return file is null ? Missing() : Results.Ok(statements.Preview(file.Value.Name, file.Value.Content));
        }).DisableAntiforgery().WithMetadata(upload);

        admin.MapPost("/bank-statements", async (HttpRequest request, ClaimsPrincipal principal, IBillingBankStatements statements,
            CancellationToken cancellationToken) =>
        {
            var file = await ReadFileAsync(request, cancellationToken);
            if (file is null) return Missing();
            var form = await request.ReadFormAsync(cancellationToken);
            int? Number(string name) => int.TryParse(form[name].ToString(), out var value) ? value : null;
            if (!Guid.TryParse(form["bankAccountId"].ToString(), out var bankAccountId) || Number("headerRow") is not { } headerRow
                || Number("dateColumn") is not { } dateColumn || Number("descriptionColumn") is not { } descriptionColumn || Number("amountColumn") is not { } amountColumn)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Banka hesabını ve tarih, açıklama, tutar sütunlarını seçin.",
                    extensions: new Dictionary<string, object?> { ["code"] = "Billing.StatementInvalid" });
            var incoming = form["incomingValue"].ToString();
            var mapping = new StatementMapping(headerRow, dateColumn, descriptionColumn, amountColumn, Number("senderColumn"), Number("referenceColumn"),
                Number("directionColumn"), string.IsNullOrWhiteSpace(incoming) ? null : incoming.Trim());
            return Results.Ok(await statements.ImportAsync(bankAccountId, file.Value.Name, file.Value.Content, mapping, UserId(principal), cancellationToken));
        }).DisableAntiforgery().WithMetadata(upload);

        admin.MapGet("/bank-statements", (IBillingBankStatements statements, CancellationToken cancellationToken) => statements.GetImportsAsync(cancellationToken));
        admin.MapGet("/bank-transactions", ([FromQuery] BankTransactionStatus? status, IBillingBankStatements statements, CancellationToken cancellationToken) =>
            statements.GetTransactionsAsync(status, cancellationToken));
        admin.MapPost("/bank-transactions/{id:guid}/approve", async (Guid id, TieTransactionRequest request, ClaimsPrincipal principal,
            IBillingBankStatements statements, CancellationToken cancellationToken) =>
            OkOrNotFound(await statements.ApproveWithOrderAsync(id, request.OrderId, UserId(principal), cancellationToken)));
        admin.MapPost("/bank-transactions/{id:guid}/ignore", async (Guid id, ReviewOrderRequest? request, ClaimsPrincipal principal,
            IBillingBankStatements statements, CancellationToken cancellationToken) =>
            OkOrNotFound(await statements.SetIgnoredAsync(id, true, request?.Note, UserId(principal), cancellationToken)));
        admin.MapPost("/bank-transactions/{id:guid}/restore", async (Guid id, ClaimsPrincipal principal, IBillingBankStatements statements,
            CancellationToken cancellationToken) =>
            OkOrNotFound(await statements.SetIgnoredAsync(id, false, null, UserId(principal), cancellationToken)));
    }

    private static async Task<(string Name, byte[] Content)?> ReadFileAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType) return null;
        var form = await request.ReadFormAsync(cancellationToken);
        if (form.Files.GetFile("file") is not { Length: > 0 and <= StatementRules.MaxFileBytes } file) return null;
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return (file.FileName, buffer.ToArray());
    }

    private static IResult Missing() => Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Ekstre dosyasını seçin (CSV ya da .xlsx, en çok 5 MB).",
        extensions: new Dictionary<string, object?> { ["code"] = "Billing.StatementInvalid" });

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue("sub")!);

    private static IResult OkOrNotFound<T>(T? value) where T : class => value is null ? Results.NotFound() : Results.Ok(value);
}
