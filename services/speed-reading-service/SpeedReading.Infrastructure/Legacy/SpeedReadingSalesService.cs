using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Infrastructure.Legacy;

/// <summary>
/// Legal check Y1 (user decision 14.11.2026): the seller details, the sales documents and the withdrawal (cayma) of
/// Hızlı Okuma bank-transfer purchases. The refund itself is made by bank; approving a withdrawal ends the access the
/// payment gave and keeps the payment request as the financial record.
/// </summary>
public sealed class SpeedReadingSalesService : ISpeedReadingSales
{
    private readonly ISpeedReadingDataContext db;
    private readonly ISpeedReadingUserDirectory userDirectory;

    internal SpeedReadingSalesService(ISpeedReadingDataContext db, ISpeedReadingUserDirectory userDirectory)
    {
        this.db = db;
        this.userDirectory = userDirectory;
    }

    internal static SpeedReadingSellerInfo Seller(LegacyBankTransferPaymentSettings? settings) =>
        new(settings?.SellerTitle, settings?.SellerAddress, settings?.SellerTaxOffice, settings?.SellerTaxNumber,
            settings?.SellerMersisNumber, settings?.SellerEmail, settings?.SellerPhone);

    public async Task<SpeedReadingSellerInfo> GetSellerAsync(CancellationToken cancellationToken = default) =>
        Seller(await db.BankTransferPaymentSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == LegacySpeedReadingSubscription.BankTransferSettingsId, cancellationToken));

    public async Task<SpeedReadingSellerInfo> UpdateSellerAsync(SpeedReadingSellerInfo request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var email = Optional(request.Email, 320);
        if (email is not null && !email.Contains('@')) throw new BusinessRuleException("Sales.SellerInvalid", "Satıcı e-postası geçersiz.");
        var taxNumber = Digits(request.TaxNumber, 10, 11, "Vergi numarası");
        var mersis = Digits(request.MersisNumber, 16, 16, "MERSİS numarası");
        var now = DateTime.UtcNow;
        var settings = await db.BankTransferPaymentSettings
            .SingleOrDefaultAsync(item => item.Id == LegacySpeedReadingSubscription.BankTransferSettingsId, cancellationToken);
        if (settings is null)
        {
            settings = new LegacyBankTransferPaymentSettings { Id = LegacySpeedReadingSubscription.BankTransferSettingsId, CreatedBy = actorId, CreatedAt = now };
            db.BankTransferPaymentSettings.Add(settings);
        }
        settings.SellerTitle = Optional(request.Title, 200);
        settings.SellerAddress = Optional(request.Address, 500);
        settings.SellerTaxOffice = Optional(request.TaxOffice, 100);
        settings.SellerTaxNumber = taxNumber;
        settings.SellerMersisNumber = mersis;
        settings.SellerEmail = email;
        settings.SellerPhone = Optional(request.Phone, 40);
        settings.UpdatedBy = actorId;
        settings.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return Seller(settings);
    }

    public async Task<SpeedReadingSalesDocuments?> RenderAsync(Guid userId, Guid planId, CancellationToken cancellationToken = default)
    {
        var settings = await db.BankTransferPaymentSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == LegacySpeedReadingSubscription.BankTransferSettingsId, cancellationToken);
        if (settings is null || !BankTransferPaymentRules.HasCompletePublicSettings(settings.IsEnabled, settings.AccountHolder, settings.BankName, settings.Iban))
            return null;
        var plan = await (from item in db.SubscriptionPlans.AsNoTracking()
                          join product in db.Products.AsNoTracking() on item.ProductId equals product.Id
                          where item.Id == planId && item.IsActive && item.IsPublic && product.IsActive && product.IsPublic
                             && item.Price > 0 && !item.IsContactOnly
                          select item).SingleOrDefaultAsync(cancellationToken);
        if (plan?.DurationDays is not > 0) return null;
        var seller = Seller(settings);
        if (!seller.IsComplete)
            throw new BusinessRuleException("Sales.SellerMissing", "Satış belgeleri henüz hazır değil. Lütfen daha sonra tekrar deneyin.");
        var user = (await userDirectory.GetUsersAsync([userId], cancellationToken)).Users.SingleOrDefault(item => item.UserId == userId);
        if (user is null || string.IsNullOrWhiteSpace(user.Email)) return null;
        var name = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(name)) name = user.Email.Trim();
        return SpeedReadingSalesDocumentTexts.Render(new(seller, name, user.Email.Trim(), plan.Name, plan.DurationDays.Value, plan.Price, "TRY",
            DateTime.UtcNow, settings.Instructions ?? string.Empty));
    }

    public async Task<SpeedReadingSalesDocuments?> GetAcceptedAsync(Guid userId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var json = await db.BankTransferPaymentRequests.AsNoTracking()
            .Where(item => item.Id == requestId && item.UserId == userId)
            .Select(item => item.SalesDocumentsJson)
            .SingleOrDefaultAsync(cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<SpeedReadingSalesDocuments>(json);
    }

    public async Task<BankTransferPaymentRequestSummary?> RequestWithdrawalAsync(Guid userId, Guid requestId, string? reason, CancellationToken cancellationToken = default)
    {
        var row = await RowAsync(requestId, cancellationToken);
        if (row is null || row.Value.Request.UserId != userId) return null;
        var request = row.Value.Request;
        var now = DateTime.UtcNow;
        if (request.Status != BankTransferPaymentRules.ApprovedStatus || request.ReviewedAt is null)
            throw new BusinessRuleException("Sales.NotWithdrawable", "Yalnız onaylanmış bir ödemeden cayılabilir.");
        if (request.WithdrawalStatus is SpeedReadingSalesDocumentTexts.WithdrawalRequested or SpeedReadingSalesDocumentTexts.WithdrawalApproved)
            throw new BusinessRuleException("Sales.WithdrawalExists", "Bu ödeme için cayma talebiniz zaten var.");
        if (now > request.ReviewedAt.Value.AddDays(SpeedReadingSalesDocumentTexts.WithdrawalDays))
            throw new BusinessRuleException("Sales.WithdrawalPeriodOver", $"Cayma süresi ({SpeedReadingSalesDocumentTexts.WithdrawalDays} gün) doldu.");
        request.WithdrawalStatus = SpeedReadingSalesDocumentTexts.WithdrawalRequested;
        request.WithdrawalReason = Optional(reason, 500) ?? "Gerekçe belirtilmedi";
        request.WithdrawalRequestedAt = now;
        request.WithdrawalReviewNote = null;
        request.WithdrawalReviewedAt = null;
        request.WithdrawalReviewedBy = null;
        request.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return LegacySpeedReadingSubscription.ToBankTransferPaymentRequestSummary(request, row.Value.Plan);
    }

    public async Task<BankTransferPaymentRequestSummary?> ReviewWithdrawalAsync(Guid requestId, ReviewBankTransferWithdrawalRequest review, Guid actorId, CancellationToken cancellationToken = default)
    {
        // Only a rejection needs the admin's words; an approval without a note gets the standard sentence.
        var note = Optional(review.Note, 500);
        if (note is null && !review.Approve) throw new BusinessRuleException("Sales.WithdrawalNoteRequired", "Cayma talebini reddetmek için gerekçe yazın.");
        var row = await RowAsync(requestId, cancellationToken);
        if (row is null || row.Value.Request.WithdrawalStatus != SpeedReadingSalesDocumentTexts.WithdrawalRequested) return null;
        var request = row.Value.Request;
        var now = DateTime.UtcNow;
        if (review.Approve)
        {
            if (request.Status != BankTransferPaymentRules.ApprovedStatus)
                throw new BusinessRuleException("Sales.NotWithdrawable", "Bu ödeme artık onaylı değil.");
            // The access this payment gave ends now; the payment request stays as the financial record.
            if (request.SubscriptionId is { } subscriptionId
                && await db.UserSubscriptions.SingleOrDefaultAsync(item => item.Id == subscriptionId && !item.IsDeleted, cancellationToken) is { } subscription)
            {
                subscription.Status = "Cancelled";
                if (subscription.EndDate is null || subscription.EndDate > now) subscription.EndDate = now;
                subscription.UpdatedAt = now;
                subscription.UpdatedBy = actorId;
            }
        }
        request.WithdrawalStatus = review.Approve ? SpeedReadingSalesDocumentTexts.WithdrawalApproved : SpeedReadingSalesDocumentTexts.WithdrawalRejected;
        request.WithdrawalReviewNote = note ?? SpeedReadingSalesDocumentTexts.ApprovedWithdrawalNote;
        request.WithdrawalReviewedAt = now;
        request.WithdrawalReviewedBy = actorId;
        request.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return LegacySpeedReadingSubscription.ToBankTransferPaymentRequestSummary(request, row.Value.Plan);
    }

    private async Task<(LegacyBankTransferPaymentRequest Request, LegacySubscriptionPlan Plan)?> RowAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var row = await (from request in db.BankTransferPaymentRequests
                         join plan in db.SubscriptionPlans.AsNoTracking() on request.PlanId equals plan.Id
                         where request.Id == requestId
                         select new { request, plan }).AsTracking().SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.request, row.plan);
    }

    private static string? Optional(string? value, int maxLength)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text[..Math.Min(text.Length, maxLength)];
    }

    private static string? Digits(string? value, int min, int max, string label)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (text.Length < min || text.Length > max || !text.All(char.IsAsciiDigit))
            throw new BusinessRuleException("Sales.SellerInvalid", min == max ? $"{label} {min} haneli olmalıdır." : $"{label} {min} ya da {max} haneli olmalıdır.");
        return text;
    }
}
