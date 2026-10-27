using System.Security.Cryptography;
using System.Text.Json;
using Coaching.Application.Attachments;
using Coaching.Application.Subscriptions;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Management;

public sealed record CoachingTrialSummary(Guid SubscriptionId, Guid PlanId, string PlanName, DateTime StartDate, DateTime EndDate);
public sealed record CoachingCouponRequest(string Code, string Kind, decimal Value, DateTime? ValidFrom, DateTime? ValidUntil, int? MaxUses,
    IReadOnlyList<Guid>? PlanIds, bool IsActive);
public sealed record CoachingCouponSummary(Guid Id, string Code, string Kind, decimal Value, DateTime? ValidFrom, DateTime? ValidUntil, int? MaxUses,
    int UsedCount, IReadOnlyList<Guid> PlanIds, bool IsActive, DateTime CreatedAt);
public sealed record CoachingSellerInfo(string? Title, string? Address, string? TaxOffice, string? TaxNumber, string? MersisNumber, string? Email, string? Phone);
public sealed record CoachingWithdrawalSummary(Guid TransferId, string UserName, string UserEmail, string PlanName, decimal Amount, string Currency,
    DateTime? ApprovedAt, string? WithdrawalStatus, string? Reason, DateTime? RequestedAt, string? ReviewNote, DateTime? ReviewedAt);
public sealed record CoachingInvoiceSummary(Guid Id, Guid PaymentId, string InvoiceNumber, DateOnly IssuedOn, string FileName, long SizeBytes);
public sealed record CoachingBuyerPayment(Guid Id, Guid? TransferId, string PlanName, decimal Amount, string Currency, string Status, DateTime CreatedAt,
    decimal? ListPrice, string? CouponCode, decimal DiscountAmount, string? InvoiceNumber, DateOnly? InvoiceIssuedOn, string? WithdrawalStatus,
    DateTime? WithdrawUntil, bool HasSalesDocuments);

/// <summary>An invoice PDF opened for download; the caller disposes it.</summary>
public sealed class CoachingInvoiceFile(Stream content, string fileName) : IAsyncDisposable
{
    public Stream Content { get; } = content;
    public string FileName { get; } = fileName;
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

/// <summary>
/// Roadmap O10: tools for selling by bank transfer — a per-plan free trial once per account, admin discount codes, the
/// pre-information form and distance-sales contract kept with each purchase, the 14-day withdrawal (cayma) request that an
/// admin approves (which reverses the payment) or rejects, and invoices issued manually and recorded with their PDF.
/// </summary>
public sealed class CoachingSalesService(CoachingDbContext db, ICoachingSubscription subscriptions, IAssignmentAttachmentStorage storage,
    IAssignmentAttachmentScanner scanner, TimeProvider clock)
{
    public const int WithdrawalDays = 14;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---- Trial -------------------------------------------------------------------------------------------------------

    /// <summary>Starts the plan's free trial: a student for an individual plan, a teacher for a teacher plan; once per account.</summary>
    public async Task<CoachingTrialSummary> StartTrialAsync(Guid userId, string? userName, string? userEmail, string role, Guid planId, CancellationToken ct)
    {
        var plan = await db.CoachingSubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == planId && x.IsActive && x.IsPublic, ct)
            ?? throw new KeyNotFoundException();
        var audience = role switch { "Student" => "Individual", "Teacher" => "Teacher", _ => null };
        if (userId == Guid.Empty || audience is null || plan.Audience != audience)
            throw new BusinessRuleException("Authorization.Forbidden", "Bu planın denemesini başlatamazsınız.");
        if (plan.TrialDays <= 0) throw new ArgumentException("Bu planda ücretsiz deneme yok.");
        if (await db.Set<CoachingSubscriptionTrial>().AnyAsync(x => x.UserId == userId && x.Audience == audience, ct)
            || await db.CoachingSubscriptions.AnyAsync(x => x.UserId == userId
                && db.CoachingSubscriptionPlans.Any(p => p.Id == x.PlanId && p.Audience == audience), ct))
            throw new BusinessRuleException("Sales.TrialUsed", "Ücretsiz deneme hesap başına bir kez kullanılabilir.");

        var now = Now;
        var subscription = new CoachingSubscription
        {
            PlanId = plan.Id, UserId = userId, UserName = Optional(userName, 200), UserEmail = Optional(userEmail, 320), Status = "Active",
            StartDate = now, EndDate = now.AddDays(plan.TrialDays), Notes = $"Ücretsiz deneme ({plan.TrialDays} gün)", CreatedBy = userId, CreatedAt = now
        };
        db.CoachingSubscriptions.Add(subscription);
        db.CoachingSubscriptionPeriods.Add(new CoachingSubscriptionPeriod
        {
            SubscriptionId = subscription.Id, StartDate = subscription.StartDate, EndDate = subscription.EndDate, DurationDays = plan.TrialDays,
            Source = "Trial", Status = "Active", CreatedBy = userId, CreatedAt = now
        });
        db.Add(new CoachingSubscriptionTrial { UserId = userId, Audience = audience, PlanId = plan.Id, SubscriptionId = subscription.Id, StartedAt = now, EndsAt = subscription.EndDate });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new BusinessRuleException("Sales.TrialUsed", "Ücretsiz deneme hesap başına bir kez kullanılabilir."); }
        return new(subscription.Id, plan.Id, plan.Name, subscription.StartDate, subscription.EndDate);
    }

    // ---- Coupons ------------------------------------------------------------------------------------------------------

    public async Task<CoachingCouponQuote> QuoteAsync(Guid userId, string code, Guid planId, CancellationToken ct)
    {
        var plan = await db.CoachingSubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == planId && x.IsActive && x.IsPublic && !x.IsContactOnly, ct)
            ?? throw new ArgumentException("Plan bulunamadı.");
        return (await CoachingCoupons.QuoteAsync(db, code, plan, userId, Now, ct)).Quote;
    }

    public async Task<IReadOnlyList<CoachingCouponSummary>> ListCouponsAsync(CancellationToken ct) =>
        (await db.Set<CoachingCoupon>().AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync(ct)).Select(View).ToArray();

    public async Task<CoachingCouponSummary> CreateCouponAsync(Guid actor, CoachingCouponRequest request, CancellationToken ct)
    {
        var code = CoachingCoupons.Normalize(request.Code);
        if (await db.Set<CoachingCoupon>().AnyAsync(x => x.Code == code, ct)) throw new ArgumentException("Bu indirim kodu zaten var.");
        var coupon = new CoachingCoupon { Code = code, CreatedBy = actor, CreatedAt = Now };
        await Apply(coupon, request, ct);
        db.Add(coupon);
        await db.SaveChangesAsync(ct);
        return View(coupon);
    }

    /// <summary>Changes a code's limits; the code itself and its usage count stay.</summary>
    public async Task<CoachingCouponSummary> UpdateCouponAsync(Guid actor, Guid id, CoachingCouponRequest request, CancellationToken ct)
    {
        var coupon = await db.Set<CoachingCoupon>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
        await Apply(coupon, request, ct);
        coupon.UpdatedBy = actor; coupon.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);
        return View(coupon);
    }

    private async Task Apply(CoachingCoupon coupon, CoachingCouponRequest request, CancellationToken ct)
    {
        var kind = request.Kind?.Trim();
        if (kind is not ("Percent" or "Amount") || request.Value <= 0 || kind == "Percent" && request.Value > 100 || request.Value > 1_000_000
            || request.MaxUses is < 1 or > 1_000_000 || request.ValidFrom > request.ValidUntil)
            throw new ArgumentException("İndirim türü, değeri, kullanım sınırı ya da tarihleri geçersiz.");
        var plans = (request.PlanIds ?? []).Distinct().ToList();
        if (plans.Count > 0 && await db.CoachingSubscriptionPlans.CountAsync(x => plans.Contains(x.Id), ct) != plans.Count)
            throw new ArgumentException("Seçilen planlardan biri bulunamadı.");
        coupon.Kind = kind; coupon.Value = Math.Round(request.Value, 2); coupon.MaxUses = request.MaxUses; coupon.IsActive = request.IsActive;
        coupon.ValidFrom = request.ValidFrom?.ToUniversalTime(); coupon.ValidUntil = request.ValidUntil?.ToUniversalTime();
        coupon.PlanIdsJson = JsonSerializer.Serialize(plans);
    }

    private static CoachingCouponSummary View(CoachingCoupon x) =>
        new(x.Id, x.Code, x.Kind, x.Value, x.ValidFrom, x.ValidUntil, x.MaxUses, x.UsedCount, CoachingCoupons.PlanIds(x), x.IsActive, x.CreatedAt);

    // ---- Seller and documents ----------------------------------------------------------------------------------------

    public async Task<CoachingSellerInfo> GetSellerAsync(CancellationToken ct)
    {
        var s = await db.CoachingSubscriptionSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct) ?? new CoachingSubscriptionSettings();
        return new(s.SellerTitle, s.SellerAddress, s.SellerTaxOffice, s.SellerTaxNumber, s.SellerMersisNumber, s.SellerEmail, s.SellerPhone);
    }

    public async Task<CoachingSellerInfo> UpdateSellerAsync(Guid actor, CoachingSellerInfo request, CancellationToken ct)
    {
        var settings = await db.CoachingSubscriptionSettings.SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (settings is null) { settings = new CoachingSubscriptionSettings(); db.CoachingSubscriptionSettings.Add(settings); }
        settings.SellerTitle = Optional(request.Title, 200); settings.SellerAddress = Optional(request.Address, 500);
        settings.SellerTaxOffice = Optional(request.TaxOffice, 100); settings.SellerTaxNumber = Digits(request.TaxNumber, 10, 11, "Vergi numarası");
        settings.SellerMersisNumber = Digits(request.MersisNumber, 16, 16, "MERSİS numarası");
        settings.SellerEmail = Optional(request.Email, 320); settings.SellerPhone = Optional(request.Phone, 40);
        if (settings.SellerEmail is { } email && !email.Contains('@')) throw new ArgumentException("Satıcı e-postası geçersiz.");
        settings.UpdatedBy = actor; settings.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);
        return await GetSellerAsync(ct);
    }

    /// <summary>The documents a buyer reads before sending a bank-transfer notice for <paramref name="planId"/>.</summary>
    public async Task<CoachingSalesDocuments> RenderDocumentsAsync(Guid buyerId, string buyerName, string? buyerEmail, Guid planId, string? couponCode, CancellationToken ct)
    {
        var plan = await db.CoachingSubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == planId && x.IsActive && x.IsPublic && !x.IsContactOnly, ct)
            ?? throw new ArgumentException("Plan bulunamadı.");
        var quote = string.IsNullOrWhiteSpace(couponCode) ? null : (await CoachingCoupons.QuoteAsync(db, couponCode, plan, buyerId, Now, ct)).Quote;
        var settings = await db.CoachingSubscriptionSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct) ?? new CoachingSubscriptionSettings();
        return CoachingSalesDocumentTexts.Render(new(settings, Optional(buyerName, 200) ?? "Koçluk kullanıcısı", buyerEmail, plan.Name, plan.DurationDays,
            plan.Price, quote?.Discount ?? 0, quote?.Amount ?? plan.Price, settings.Currency, quote?.Code, Now, settings.PaymentInstructions ?? string.Empty));
    }

    /// <summary>The documents accepted with a transfer, for its payer or the parent who sent it.</summary>
    public async Task<CoachingSalesDocuments> GetTransferDocumentsAsync(Guid viewer, Guid transferId, CancellationToken ct)
    {
        var transfer = await OwnTransferAsync(viewer, transferId, ct);
        return transfer.SalesDocumentsJson is { } json ? JsonSerializer.Deserialize<CoachingSalesDocuments>(json, Json)! : throw new KeyNotFoundException();
    }

    // ---- Withdrawal (cayma) -------------------------------------------------------------------------------------------

    public async Task<CoachingBuyerPayment> RequestWithdrawalAsync(Guid viewer, Guid transferId, string? reason, CancellationToken ct)
    {
        var transfer = await OwnTransferAsync(viewer, transferId, ct, track: true);
        if (transfer.Status != "Approved" || transfer.ReviewedAt is null) throw new BusinessRuleException("Sales.NotWithdrawable", "Yalnız onaylanmış bir ödemeden cayılabilir.");
        if (transfer.WithdrawalStatus is "Requested" or "Approved") throw new BusinessRuleException("Sales.WithdrawalExists", "Bu ödeme için cayma talebiniz zaten var.");
        if (Now > transfer.ReviewedAt.Value.AddDays(WithdrawalDays))
            throw new BusinessRuleException("Sales.WithdrawalPeriodOver", $"Cayma süresi ({WithdrawalDays} gün) doldu.");
        transfer.WithdrawalStatus = "Requested"; transfer.WithdrawalReason = Optional(reason, 500) ?? "Gerekçe belirtilmedi";
        transfer.WithdrawalRequestedAt = Now; transfer.WithdrawalReviewNote = null; transfer.WithdrawalReviewedAt = null; transfer.WithdrawalReviewedBy = null;
        transfer.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);
        return (await ListMyPaymentsAsync(viewer, ct)).Single(x => x.TransferId == transferId);
    }

    public async Task<IReadOnlyList<CoachingWithdrawalSummary>> ListWithdrawalsAsync(CancellationToken ct)
    {
        var rows = await (from transfer in db.CoachingBankTransferRequests.AsNoTracking()
                          join plan in db.CoachingSubscriptionPlans.AsNoTracking() on transfer.PlanId equals plan.Id
                          where transfer.WithdrawalStatus != null
                          orderby transfer.WithdrawalRequestedAt descending
                          select new { transfer, plan.Name }).Take(200).ToListAsync(ct);
        return rows.Select(x => new CoachingWithdrawalSummary(x.transfer.Id, x.transfer.UserName, x.transfer.UserEmail, x.Name, x.transfer.Amount,
            x.transfer.Currency, x.transfer.ReviewedAt, x.transfer.WithdrawalStatus, x.transfer.WithdrawalReason, x.transfer.WithdrawalRequestedAt,
            x.transfer.WithdrawalReviewNote, x.transfer.WithdrawalReviewedAt)).ToArray();
    }

    /// <summary>Approving cancels the payment and ends the access it gave (the refund is made by bank); rejecting needs a reason.</summary>
    public async Task ReviewWithdrawalAsync(Guid actor, Guid transferId, bool approve, string? note, CancellationToken ct)
    {
        var text = Optional(note, 500);
        if (text is null) throw new ArgumentException("Cayma kararı için bir not yazın (iade tarihi ya da ret gerekçesi).");
        var transfer = await db.CoachingBankTransferRequests.SingleOrDefaultAsync(x => x.Id == transferId && x.WithdrawalStatus == "Requested", ct)
            ?? throw new KeyNotFoundException();
        if (approve)
        {
            if (await subscriptions.CancelApprovedBankTransferAsync(transferId, $"Cayma: {text}", actor, ct) is null)
                throw new BusinessRuleException("Sales.NotWithdrawable", "Bu ödeme artık onaylı değil.");
            db.ChangeTracker.Clear();
            transfer = await db.CoachingBankTransferRequests.SingleAsync(x => x.Id == transferId, ct);
        }
        transfer.WithdrawalStatus = approve ? "Approved" : "Rejected";
        transfer.WithdrawalReviewNote = text; transfer.WithdrawalReviewedAt = Now; transfer.WithdrawalReviewedBy = actor; transfer.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);
    }

    // ---- Payments and invoices ----------------------------------------------------------------------------------------

    /// <summary>The payments the viewer made: their own, and those a parent sent for a child.</summary>
    public async Task<IReadOnlyList<CoachingBuyerPayment>> ListMyPaymentsAsync(Guid viewer, CancellationToken ct)
    {
        var rows = await (from payment in db.CoachingPaymentRecords.AsNoTracking()
                          join t in db.CoachingBankTransferRequests.AsNoTracking() on payment.BankTransferRequestId equals t.Id into transfers
                          from transfer in transfers.DefaultIfEmpty()
                          join i in db.Set<CoachingInvoice>().AsNoTracking() on payment.Id equals i.PaymentRecordId into invoices
                          from invoice in invoices.DefaultIfEmpty()
                          where payment.InstitutionId == null && (payment.UserId == viewer || transfer != null && transfer.SubmittedByUserId == viewer)
                          orderby payment.CreatedAt descending
                          select new { payment, transfer, invoice }).Take(100).ToListAsync(ct);
        return rows.Select(x => new CoachingBuyerPayment(x.payment.Id, x.transfer?.Id, x.payment.PlanName, x.payment.Amount, x.payment.Currency,
            x.payment.Status, x.payment.CreatedAt, x.transfer?.ListPrice, x.transfer?.CouponCode, x.transfer?.DiscountAmount ?? 0,
            x.invoice?.InvoiceNumber, x.invoice?.IssuedOn, x.transfer?.WithdrawalStatus,
            x.transfer is { Status: "Approved", ReviewedAt: { } approved } && x.transfer.WithdrawalStatus is null or "Rejected" ? approved.AddDays(WithdrawalDays) : null,
            x.transfer?.SalesDocumentsJson is not null)).ToArray();
    }

    public async Task<CoachingInvoiceSummary> AttachInvoiceAsync(Guid actor, Guid paymentId, string invoiceNumber, DateOnly issuedOn, string fileName,
        Stream content, CancellationToken ct)
    {
        var number = (invoiceNumber ?? string.Empty).Trim().ToUpperInvariant();
        if (number.Length is < 3 or > 40 || number.Any(c => !char.IsLetterOrDigit(c) && c is not ('-' or '/' or '.')))
            throw new ArgumentException("Fatura numarası 3–40 karakter olmalı (harf, rakam, - / .).");
        if (issuedOn > DateOnly.FromDateTime(Now.AddDays(1)) || issuedOn.Year < 2020) throw new ArgumentException("Fatura tarihi geçersiz.");
        if (!await db.CoachingPaymentRecords.AnyAsync(x => x.Id == paymentId, ct)) throw new KeyNotFoundException();
        if (await db.Set<CoachingInvoice>().AnyAsync(x => x.PaymentRecordId == paymentId, ct)) throw new ArgumentException("Bu ödemenin faturası zaten kayıtlı; önce kaldırın.");
        if (await db.Set<CoachingInvoice>().AnyAsync(x => x.InvoiceNumber == number, ct)) throw new ArgumentException("Bu fatura numarası başka bir ödemede kayıtlı.");
        var name = Path.GetFileName((fileName ?? string.Empty).Trim());
        if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || name.Length > 255) throw new ArgumentException("Fatura PDF dosyası olmalıdır.");
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length is 0 or > AssignmentAttachmentPolicy.MaxFileSizeBytes) throw new ArgumentException("Fatura dosyası en fazla 10 MB olabilir.");
        var id = Guid.NewGuid(); var key = $"invoices/{id:N}";
        var sha = Convert.ToHexString(SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)));
        buffer.Position = 0;
        try { await storage.StoreAsync(key, buffer, "application/pdf", buffer.Length, sha, ct); }
        catch (ArgumentException) { throw new ArgumentException("Dosya geçerli bir PDF değil."); }
        try
        {
            AttachmentScanResult scan;
            await using (var copy = await storage.OpenReadAsync(key, ct)) scan = await scanner.ScanAsync(copy, ct);
            if (!scan.IsClean) throw new ArgumentException("Dosya güvenlik taramasından geçemedi.");
            var invoice = new CoachingInvoice { Id = id, PaymentRecordId = paymentId, InvoiceNumber = number, IssuedOn = issuedOn, FileName = name,
                StorageKey = key, Sha256 = sha, SizeBytes = buffer.Length, CreatedBy = actor, CreatedAt = Now };
            db.Add(invoice);
            await db.SaveChangesAsync(ct);
            return new(invoice.Id, paymentId, number, issuedOn, name, invoice.SizeBytes);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }
    }

    public async Task RemoveInvoiceAsync(Guid actor, Guid paymentId, CancellationToken ct)
    {
        var invoice = await db.Set<CoachingInvoice>().SingleOrDefaultAsync(x => x.PaymentRecordId == paymentId, ct) ?? throw new KeyNotFoundException();
        db.Remove(invoice);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(invoice.StorageKey, CancellationToken.None);
    }

    /// <summary>The invoice of one of the viewer's payments (or, with <paramref name="asAdmin"/>, any payment).</summary>
    public async Task<CoachingInvoiceFile> OpenInvoiceAsync(Guid viewer, Guid paymentId, CancellationToken ct, bool asAdmin = false)
    {
        if (!asAdmin && (await ListMyPaymentsAsync(viewer, ct)).All(x => x.Id != paymentId)) throw new KeyNotFoundException();
        var invoice = await db.Set<CoachingInvoice>().AsNoTracking().SingleOrDefaultAsync(x => x.PaymentRecordId == paymentId, ct) ?? throw new KeyNotFoundException();
        return new(await storage.OpenReadAsync(invoice.StorageKey, ct), invoice.FileName);
    }

    public async Task<IReadOnlyDictionary<Guid, CoachingInvoiceSummary>> InvoicesForAsync(IReadOnlyCollection<Guid> paymentIds, CancellationToken ct)
    {
        var ids = paymentIds.ToList();
        return await db.Set<CoachingInvoice>().AsNoTracking().Where(x => ids.Contains(x.PaymentRecordId))
            .ToDictionaryAsync(x => x.PaymentRecordId, x => new CoachingInvoiceSummary(x.Id, x.PaymentRecordId, x.InvoiceNumber, x.IssuedOn, x.FileName, x.SizeBytes), ct);
    }

    private async Task<CoachingBankTransferRequest> OwnTransferAsync(Guid viewer, Guid transferId, CancellationToken ct, bool track = false)
    {
        var query = track ? db.CoachingBankTransferRequests : db.CoachingBankTransferRequests.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == transferId && (x.UserId == viewer || x.SubmittedByUserId == viewer), ct)
            ?? throw new KeyNotFoundException();
    }

    private static string? Optional(string? value, int max)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length > max || text.Any(c => char.IsControl(c) && c is not ('\n' or '\r'))) throw new ArgumentException($"Bir alan en fazla {max} karakter olabilir.");
        return text;
    }

    private static string? Digits(string? value, int min, int max, string label)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        return text.Length >= min && text.Length <= max && text.All(char.IsAsciiDigit) ? text : throw new ArgumentException($"{label} {min}{(min == max ? "" : "–" + max)} haneli olmalı.");
    }
}
