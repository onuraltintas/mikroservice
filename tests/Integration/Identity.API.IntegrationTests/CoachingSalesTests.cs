using System.Text;
using Coaching.API.Controllers;
using Coaching.Application.Attachments;
using Coaching.Application.Subscriptions;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Attachments;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.API.IntegrationTests;

// Roadmap O10: selling by bank transfer — a per-plan free trial once per account, admin coupons, the distance-sales
// contract and pre-information form accepted with the purchase, the 14-day withdrawal request and manual invoice records.
public sealed partial class CoachingCmsAndSubscriptionServiceTests
{
    private static readonly byte[] InvoicePdf = Encoding.ASCII.GetBytes("%PDF-1.4\nfatura");

    [Fact]
    public async Task APlansTrialStartsOncePerAccountGivesAccessAndAPaidPeriodFollowsIt()
    {
        await using var s = await SalesSetup.CreateAsync();
        var plan = (await s.Subscriptions.CreatePlanAsync(Plan("trial", "Bireysel yıllık", "Individual", 1200m, null) with { TrialDays = 14 }, s.Admin))!.Value;

        var trial = await s.Sales.StartTrialAsync(s.User, "Ada", "ada@example.test", "Student", plan, default);

        trial.EndDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(14), TimeSpan.FromMinutes(1));
        (await s.Subscriptions.GetMyAccessAsync(s.User)).HasAccess.Should().BeTrue();
        (await s.Db.CoachingSubscriptionPeriods.SingleAsync()).Source.Should().Be("Trial");
        (await s.Db.CoachingPaymentRecords.AnyAsync()).Should().BeFalse("a trial is not a payment");
        (await Assert.ThrowsAsync<BusinessRuleException>(() => s.Sales.StartTrialAsync(s.User, "Ada", "ada@example.test", "Student", plan, default)))
            .Code.Should().Be("Sales.TrialUsed");
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Sales.StartTrialAsync(Guid.NewGuid(), "Can", null, "Teacher", plan, default));

        var paid = await s.Subscriptions.CreateBankTransferRequestAsync(s.User, "Ada", "ada@example.test",
            new CoachingBankTransferRequestCreate(plan, "EFT-AFTER-TRIAL", "Ada", null, true, AcceptSalesContract: true), "create-after-trial-01");
        await s.Subscriptions.ReviewBankTransferRequestAsync(paid!.Id, new("Approved", null), s.Admin, "review-after-trial-01");
        (await s.Db.CoachingSubscriptions.SingleAsync()).EndDate.Should().BeCloseTo(trial.EndDate.AddDays(365), TimeSpan.FromMinutes(1),
            "the paid year starts when the trial ends");

        var noTrial = (await s.Subscriptions.CreatePlanAsync(Plan("no-trial", "Aylık", "Individual", 100m, null), s.Admin))!.Value;
        await Assert.ThrowsAsync<ArgumentException>(() => s.Sales.StartTrialAsync(Guid.NewGuid(), "Can", null, "Student", noTrial, default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Subscriptions.CreatePlanAsync(
            Plan("institution-trial", "Kurum", "Institution", 5000m, 50) with { TrialDays = 7 }, s.Admin));
    }

    [Fact]
    public async Task ACouponLowersThePriceFrozenOnTheTransferAndIsCountedWhenApproved()
    {
        await using var s = await SalesSetup.CreateAsync();
        var plan = (await s.Subscriptions.CreatePlanAsync(Plan("coupon", "Bireysel yıllık", "Individual", 1200m, null), s.Admin))!.Value;
        var other = (await s.Subscriptions.CreatePlanAsync(Plan("coupon-other", "Bireysel aylık", "Individual", 150m, null), s.Admin))!.Value;
        await s.Sales.CreateCouponAsync(s.Admin, new("ekim20", "Percent", 20m, null, null, 1, [plan], true), default);

        var quote = await s.Sales.QuoteAsync(s.User, " Ekim20 ", plan, default);
        (quote.Code, quote.ListPrice, quote.Discount, quote.Amount).Should().Be(("EKIM20", 1200m, 240m, 960m));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Sales.QuoteAsync(s.User, "EKIM20", other, default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Sales.QuoteAsync(s.User, "YOK", plan, default));

        var transfer = await s.Subscriptions.CreateBankTransferRequestAsync(s.User, "Ada", "ada@example.test",
            new CoachingBankTransferRequestCreate(plan, "EFT-COUPON-01", "Ada", null, true, "ekim20", AcceptSalesContract: true), "create-coupon-0001");
        (transfer!.Amount, transfer.ListPrice, transfer.CouponCode, transfer.DiscountAmount).Should().Be((960m, 1200m, "EKIM20", 240m));
        await s.Subscriptions.ReviewBankTransferRequestAsync(transfer.Id, new("Approved", null), s.Admin, "review-coupon-0001");

        (await s.Db.CoachingPaymentRecords.SingleAsync()).Amount.Should().Be(960m);
        (await s.Sales.ListCouponsAsync(default)).Single().UsedCount.Should().Be(1);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Sales.QuoteAsync(Guid.NewGuid(), "EKIM20", plan, default));

        await s.Sales.CreateCouponAsync(s.Admin, new("BEDAVA", "Amount", 2000m, null, null, null, [], true), default);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Sales.QuoteAsync(s.User, "BEDAVA", plan, default));
        await s.Sales.CreateCouponAsync(s.Admin, new("GECTI", "Amount", 100m, null, DateTime.UtcNow.AddDays(-1), null, [], true), default);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Sales.QuoteAsync(s.User, "GECTI", plan, default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Sales.CreateCouponAsync(s.Admin, new("EKIM20", "Amount", 10m, null, null, null, [], true), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Sales.CreateCouponAsync(s.Admin, new("YUZDE", "Percent", 120m, null, null, null, [], true), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Subscriptions.CreateBankTransferRequestAsync(s.User, "Ada", "ada@example.test",
            new CoachingBankTransferRequestCreate(plan, "EFT-COUPON-02", "Ada", null, true, "YOK", AcceptSalesContract: true), "create-coupon-0002"));
    }

    [Fact]
    public async Task TheSalesDocumentsNameTheSellerAndThePriceAndAreKeptWithTheTransfer()
    {
        await using var s = await SalesSetup.CreateAsync();
        var plan = (await s.Subscriptions.CreatePlanAsync(Plan("contract", "Bireysel yıllık", "Individual", 1200m, null), s.Admin))!.Value;
        await s.Sales.UpdateSellerAsync(s.Admin, new("Edu İvme Eğitim Ltd. Şti.", "Atatürk Cad. No: 1 Kadıköy/İstanbul", "Kadıköy", "1234567890",
            null, "destek@example.test", "0216 000 00 00"), default);
        await s.Sales.CreateCouponAsync(s.Admin, new("EKIM20", "Percent", 20m, null, null, null, [], true), default);

        var documents = await s.Sales.RenderDocumentsAsync(s.User, "Ada Yılmaz", "ada@example.test", plan, "EKIM20", default);
        documents.PreInformation.Should().Contain("Edu İvme Eğitim Ltd. Şti.").And.Contain("Bireysel yıllık").And.Contain("960,00 TRY").And.Contain("365 gün");
        documents.DistanceSales.Should().Contain("Ada Yılmaz").And.Contain("14").And.Contain("cayma");

        var transfer = await s.Subscriptions.CreateBankTransferRequestAsync(s.User, "Ada Yılmaz", "ada@example.test",
            new CoachingBankTransferRequestCreate(plan, "EFT-CONTRACT-1", "Ada", null, true, "EKIM20", AcceptSalesContract: true), "create-contract-001");
        transfer!.SalesContractVersion.Should().Be(CoachingSalesDocumentTexts.Version);
        var kept = await s.Sales.GetTransferDocumentsAsync(s.User, transfer.Id, default);
        kept.DistanceSales.Should().Be(documents.DistanceSales);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => s.Sales.GetTransferDocumentsAsync(Guid.NewGuid(), transfer.Id, default));
        typeof(CoachingBankTransferRequestCreate).GetConstructors().Single().GetParameters().Single(x => x.Name == nameof(CoachingBankTransferRequestCreate.AcceptSalesContract))
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), true).Should().ContainSingle(
                "the API refuses a purchase without the contract and the pre-information form");
    }

    [Fact]
    public async Task TheBuyerCanAskToWithdrawWithin14DaysAndAnApprovedWithdrawalEndsTheAccess()
    {
        await using var s = await SalesSetup.CreateAsync();
        var plan = (await s.Subscriptions.CreatePlanAsync(Plan("withdraw", "Bireysel yıllık", "Individual", 1200m, null), s.Admin))!.Value;
        var transfer = await Approve(s.Subscriptions, s.User, plan, "EFT-WITHDRAW-1", s.Admin, "withdraw-first-1");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => s.Sales.RequestWithdrawalAsync(Guid.NewGuid(), transfer, "Vazgeçtim", default));
        (await s.Sales.RequestWithdrawalAsync(s.User, transfer, "Sınav tarihim değişti", default)).WithdrawalStatus.Should().Be("Requested");
        (await s.Sales.ListWithdrawalsAsync(default)).Should().ContainSingle(x => x.TransferId == transfer && x.Reason == "Sınav tarihim değişti");
        await Assert.ThrowsAsync<ArgumentException>(() => s.Sales.ReviewWithdrawalAsync(s.Admin, transfer, false, " ", default));

        await s.Sales.ReviewWithdrawalAsync(s.Admin, transfer, true, "İade 28.10.2026 tarihinde yapıldı", default);

        (await s.Db.CoachingBankTransferRequests.SingleAsync()).Status.Should().Be("Cancelled");
        (await s.Db.CoachingPaymentRecords.SingleAsync()).Status.Should().Be("Cancelled");
        (await s.Subscriptions.GetMyAccessAsync(s.User)).HasAccess.Should().BeFalse();
        (await s.Sales.ListMyPaymentsAsync(s.User, default)).Single().WithdrawalStatus.Should().Be("Approved");

        var late = await Approve(s.Subscriptions, s.User, plan, "EFT-WITHDRAW-2", s.Admin, "withdraw-second");
        var row = await s.Db.CoachingBankTransferRequests.SingleAsync(x => x.Id == late);
        row.ReviewedAt = DateTime.UtcNow.AddDays(-15); await s.Db.SaveChangesAsync();
        (await Assert.ThrowsAsync<BusinessRuleException>(() => s.Sales.RequestWithdrawalAsync(s.User, late, "Geç", default)))
            .Code.Should().Be("Sales.WithdrawalPeriodOver");
    }

    [Fact]
    public async Task AnInvoiceIsRecordedForAPaymentAndOnlyItsBuyerDownloadsIt()
    {
        await using var s = await SalesSetup.CreateAsync();
        var plan = (await s.Subscriptions.CreatePlanAsync(Plan("invoice", "Bireysel yıllık", "Individual", 1200m, null), s.Admin))!.Value;
        await Approve(s.Subscriptions, s.User, plan, "EFT-INVOICE-01", s.Admin, "invoice-first-1");
        var payment = await s.Db.CoachingPaymentRecords.SingleAsync();

        var invoice = await s.Sales.AttachInvoiceAsync(s.Admin, payment.Id, " GIB2026000000123 ", new DateOnly(2026, 10, 28), "fatura.pdf", new MemoryStream(InvoicePdf), default);

        invoice.InvoiceNumber.Should().Be("GIB2026000000123");
        var mine = (await s.Sales.ListMyPaymentsAsync(s.User, default)).Single();
        (mine.InvoiceNumber, mine.InvoiceIssuedOn, mine.Amount).Should().Be(("GIB2026000000123", new DateOnly(2026, 10, 28), 1200m));
        await using (var file = await s.Sales.OpenInvoiceAsync(s.User, payment.Id, default))
        {
            using var copy = new MemoryStream(); await file.Content.CopyToAsync(copy);
            copy.ToArray().Should().Equal(InvoicePdf); file.FileName.Should().Be("fatura.pdf");
        }
        await Assert.ThrowsAsync<KeyNotFoundException>(() => s.Sales.OpenInvoiceAsync(Guid.NewGuid(), payment.Id, default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Sales.AttachInvoiceAsync(s.Admin, payment.Id, "GIB2026000000124", new DateOnly(2026, 10, 28),
            "fatura.pdf", new MemoryStream(InvoicePdf), default));
        await s.Sales.RemoveInvoiceAsync(s.Admin, payment.Id, default);
        (await s.Sales.ListMyPaymentsAsync(s.User, default)).Single().InvoiceNumber.Should().BeNull();
        await Assert.ThrowsAsync<ArgumentException>(() => s.Sales.AttachInvoiceAsync(s.Admin, payment.Id, "GIB2026000000125", new DateOnly(2026, 10, 28),
            "fatura.exe", new MemoryStream([1, 2, 3]), default));
    }

    [Fact]
    public void SalesAdministrationIsForGlobalSubscriptionManagers()
    {
        var controller = typeof(CoachingSalesAdminController);
        controller.GetCustomAttributes(typeof(HasPermissionAttribute), true).Should().NotBeEmpty();
        controller.GetCustomAttributes(typeof(Coaching.API.Security.RequireGlobalCoachingAdminAttribute), true).Should().NotBeEmpty();
        typeof(CoachingSalesController).GetMethod(nameof(CoachingSalesController.StartTrial))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), true).Should().NotBeEmpty();
    }

    private sealed class SalesSetup : IAsyncDisposable
    {
        public CoachingDbContext Db { get; } = CreateDbContext();
        public CoachingSubscriptionService Subscriptions => new(Db, new TestIdentityAuthorizationClient());
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "coaching-invoices-" + Guid.NewGuid().ToString("N"));
        public CoachingSalesService Sales => new(Db, Subscriptions,
            new LocalAssignmentAttachmentStorage(Options.Create(new AssignmentAttachmentOptions { RootPath = Root })), new DevelopmentAttachmentScanner(), TimeProvider.System);
        public Guid Admin { get; } = Guid.NewGuid();
        public Guid User { get; } = Guid.NewGuid();

        public static async Task<SalesSetup> CreateAsync()
        {
            var s = new SalesSetup();
            await Settings(s.Subscriptions, s.Admin, "settings-sales-" + Guid.NewGuid().ToString("N")[..10]);
            return s;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
