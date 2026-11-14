using System.Reflection;
using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Legal check Y1 (user decision 14.11.2026): a Hızlı Okuma purchase needs the pre-information form and the
/// distance-sales contract accepted, keeps what was accepted, and can be withdrawn from within 14 days of its approval.
/// </summary>
[Collection("Database")]
public sealed class SpeedReadingSalesDocumentsPostgresTests(PostgresFixture postgres)
{
    private static readonly Guid SettingsId = Guid.Parse("4c18b5a8-96e8-4af6-9195-0d4fdde3dcf2");

    [Fact]
    public void The_documents_name_the_seller_the_buyer_the_plan_the_price_and_the_withdrawal_right()
    {
        var documents = SpeedReadingSalesDocumentTexts.Render(new(
            new("ONAL LTD", "Merkez/Yozgat", "YOZGAT", "6421076848", "0642107684800001", "satici@example.invalid", "0505 000 00 00"),
            "Ayşe Veli", "ayse@example.invalid", "Yıllık plan", 365, 1499.5m, "TRY", new DateTime(2026, 11, 14, 22, 30, 0, DateTimeKind.Utc), "Açıklamaya referansı yazın."));

        Assert.Equal(SpeedReadingSalesDocumentTexts.Version, documents.Version);
        Assert.StartsWith("ÖN BİLGİLENDİRME FORMU", documents.PreInformation);
        Assert.StartsWith("MESAFELİ SATIŞ SÖZLEŞMESİ", documents.DistanceSales);
        foreach (var text in new[] { documents.PreInformation, documents.DistanceSales })
        {
            Assert.Contains("Unvan: ONAL LTD", text);
            Assert.Contains("Vergi dairesi / no: YOZGAT / 6421076848", text);
            Assert.Contains("MERSİS no: 0642107684800001", text);
            Assert.Contains("Ad soyad: Ayşe Veli", text);
            Assert.Contains("Yıllık plan", text);
            Assert.Contains("365 gün", text);
            Assert.Contains("1.499,50 TRY", text);
            Assert.Contains("14 gün içinde gerekçe göstermeden cayma hakkını kullanabilir", text);
            Assert.Contains("kesinti yapılmaz", text);
            // 22:30 UTC is already the next day in Turkey: the contract carries the Turkish calendar day.
            Assert.Contains("15.11.2026", text);
            Assert.DoesNotContain("girilmedi", text);
            Assert.DoesNotContain("test", text, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("Açıklamaya referansı yazın.", documents.PreInformation);
        Assert.Contains("6502 sayılı", documents.DistanceSales);

        Assert.False(new SpeedReadingSellerInfo("ONAL", "Yozgat", "YOZGAT", null, null, "a@example.invalid", "0505").IsComplete);
        Assert.True(new SpeedReadingSellerInfo("ONAL", "Yozgat", "YOZGAT", "6421076848", null, "a@example.invalid", "0505").IsComplete);
    }

    [Fact]
    public async Task A_purchase_needs_the_documents_keeps_them_and_can_be_withdrawn_within_fourteen_days()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var buyer = Guid.NewGuid();
            var stranger = Guid.NewGuid();
            var admin = Guid.NewGuid();
            var product = Guid.NewGuid();
            var plan = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var assembly = typeof(OwnedSpeedReadingDbContext).Assembly;
            Add("LegacyProduct", new { Id = product, Slug = "hizliokuma", Name = "Hızlı Okuma", IsActive = true, IsPublic = true, CreatedAt = now, UpdatedAt = now });
            Add("LegacySubscriptionPlan", new { Id = plan, ProductId = product, Slug = "annual", Name = "Yıllık plan", Price = 499m, IsActive = true, IsPublic = true, BillingPeriod = "Annual", DurationDays = (int?)365, CreatedAt = now });
            Add("LegacyBankTransferPaymentSettings", new { Id = SettingsId, AccountHolder = "Test", BankName = "Bank", Iban = "TR330006100519786457841326", IsEnabled = true, CreatedAt = now });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var directory = new Directory();
            var subscriptions = (ISpeedReadingSubscription)Activator.CreateInstance(
                assembly.GetType("SpeedReading.Infrastructure.Legacy.LegacySpeedReadingSubscription")!,
                BindingFlags.Instance | BindingFlags.NonPublic, null, [db, db, null, null, directory], null)!;
            var sales = (ISpeedReadingSales)Activator.CreateInstance(
                assembly.GetType("SpeedReading.Infrastructure.Legacy.SpeedReadingSalesService")!,
                BindingFlags.Instance | BindingFlags.NonPublic, null, [db, directory], null)!;
            CreateBankTransferPaymentRequest Notice(string reference, string? version) => new(plan, reference, "Veli", null, SalesDocumentsVersion: version);

            // Without the seller block there are no documents and no sale.
            Assert.False((await sales.GetSellerAsync()).IsComplete);
            Assert.Equal("Sales.SellerMissing", (await Assert.ThrowsAsync<BusinessRuleException>(() => sales.RenderAsync(buyer, plan))).Code);
            Assert.Equal("Sales.SellerMissing", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                subscriptions.CreateBankTransferPaymentRequestAsync(buyer, Notice("EFT-SALE-00", SpeedReadingSalesDocumentTexts.Version), "sales-create-key-0000"))).Code);

            Assert.Equal("Sales.SellerInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sales.UpdateSellerAsync(new("ONAL LTD", "Yozgat", "YOZGAT", "12AB", null, "satici@example.invalid", "0505"), admin))).Code);
            db.ChangeTracker.Clear();
            var seller = await sales.UpdateSellerAsync(new(" ONAL LTD ", "Merkez/Yozgat", "YOZGAT", "6421076848", null, "satici@example.invalid", "0505 000 00 00"), admin);
            Assert.True(seller.IsComplete);
            Assert.Equal("ONAL LTD", seller.Title);
            db.ChangeTracker.Clear();
            // The bank details were not touched by saving the seller.
            Assert.Equal("TR330006100519786457841326", (await subscriptions.GetPublicBankTransferSettingsAsync())!.Iban);

            var shown = await sales.RenderAsync(buyer, plan);
            Assert.Contains("Unvan: ONAL LTD", shown!.DistanceSales);
            Assert.Contains("Ad soyad: Test Veli", shown.DistanceSales);
            Assert.Null(await sales.RenderAsync(buyer, Guid.NewGuid()));

            // The notice is refused until the version in force is accepted.
            foreach (var version in new[] { null, "2026-01-01" })
                Assert.Equal("Sales.DocumentsNotAccepted", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    subscriptions.CreateBankTransferPaymentRequestAsync(buyer, Notice("EFT-SALE-01", version), $"sales-create-key-1{version?.Length ?? 0:D3}"))).Code);
            Assert.Empty(await subscriptions.GetMyBankTransferPaymentRequestsAsync(buyer));

            var created = await subscriptions.CreateBankTransferPaymentRequestAsync(buyer, Notice("EFT-SALE-01", SpeedReadingSalesDocumentTexts.Version), "sales-create-key-0001");
            Assert.Equal(SpeedReadingSalesDocumentTexts.Version, created!.SalesDocumentsVersion);
            Assert.NotNull(created.SalesDocumentsAcceptedAt);
            Assert.Null(created.WithdrawableUntil);
            db.ChangeTracker.Clear();

            // What was accepted stays as accepted, even after the seller block changes; only the buyer reads it.
            await sales.UpdateSellerAsync(new("YENİ UNVAN", "Merkez/Yozgat", "YOZGAT", "6421076848", null, "satici@example.invalid", "0505 000 00 00"), admin);
            db.ChangeTracker.Clear();
            var accepted = await sales.GetAcceptedAsync(buyer, created.Id);
            Assert.Contains("Unvan: ONAL LTD", accepted!.PreInformation);
            Assert.DoesNotContain("YENİ UNVAN", accepted.DistanceSales);
            Assert.Null(await sales.GetAcceptedAsync(stranger, created.Id));

            // No withdrawal before the payment is approved, and none by somebody else.
            Assert.Equal("Sales.NotWithdrawable", (await Assert.ThrowsAsync<BusinessRuleException>(() => sales.RequestWithdrawalAsync(buyer, created.Id, null))).Code);
            db.ChangeTracker.Clear();
            var approved = await subscriptions.ReviewBankTransferPaymentRequestAsync(created.Id, new ReviewBankTransferPaymentRequest("Approved", null), admin, "sales-review-key-0001");
            Assert.NotNull(approved!.SubscriptionId);
            Assert.Equal(approved.ReviewedAt!.Value.AddDays(14), approved.WithdrawableUntil);
            db.ChangeTracker.Clear();
            Assert.Null(await sales.RequestWithdrawalAsync(stranger, created.Id, "Benim değil"));
            Assert.True((await subscriptions.GetMyAccessAsync(buyer)).HasSpeedReading);
            db.ChangeTracker.Clear();

            // A rejected withdrawal needs the admin's words and leaves the access open; the buyer may ask again.
            var asked = await sales.RequestWithdrawalAsync(buyer, created.Id, "  ");
            Assert.Equal("Requested", asked!.WithdrawalStatus);
            Assert.Equal("Gerekçe belirtilmedi", asked.WithdrawalReason);
            db.ChangeTracker.Clear();
            Assert.Equal("Sales.WithdrawalExists", (await Assert.ThrowsAsync<BusinessRuleException>(() => sales.RequestWithdrawalAsync(buyer, created.Id, null))).Code);
            db.ChangeTracker.Clear();
            Assert.Equal("Sales.WithdrawalNoteRequired", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sales.ReviewWithdrawalAsync(created.Id, new(false, " "), admin))).Code);
            var rejected = await sales.ReviewWithdrawalAsync(created.Id, new(false, "Dekont eşleşmedi"), admin);
            Assert.Equal("Rejected", rejected!.WithdrawalStatus);
            Assert.Equal("Dekont eşleşmedi", rejected.WithdrawalReviewNote);
            db.ChangeTracker.Clear();
            Assert.True((await subscriptions.GetMyAccessAsync(buyer)).HasSpeedReading);
            Assert.Null(await sales.ReviewWithdrawalAsync(created.Id, new(true, null), admin));
            db.ChangeTracker.Clear();

            // An approved withdrawal ends the access at once and keeps the payment as the financial record.
            Assert.Equal("Requested", (await sales.RequestWithdrawalAsync(buyer, created.Id, "Vazgeçtim"))!.WithdrawalStatus);
            db.ChangeTracker.Clear();
            var withdrawn = await sales.ReviewWithdrawalAsync(created.Id, new(true, null), admin);
            Assert.Equal("Approved", withdrawn!.WithdrawalStatus);
            Assert.Equal("Approved", withdrawn.Status);
            Assert.Equal(SpeedReadingSalesDocumentTexts.ApprovedWithdrawalNote, withdrawn.WithdrawalReviewNote);
            db.ChangeTracker.Clear();
            Assert.False((await subscriptions.GetMyAccessAsync(buyer)).HasSpeedReading);
            var mine = Assert.Single(await subscriptions.GetMyBankTransferPaymentRequestsAsync(buyer));
            Assert.Equal("Approved", mine.WithdrawalStatus);
            Assert.Equal("Sales.WithdrawalExists", (await Assert.ThrowsAsync<BusinessRuleException>(() => sales.RequestWithdrawalAsync(buyer, created.Id, null))).Code);
            db.ChangeTracker.Clear();

            // After 14 days the right is over.
            var late = await subscriptions.CreateBankTransferPaymentRequestAsync(buyer, Notice("EFT-SALE-02", SpeedReadingSalesDocumentTexts.Version), "sales-create-key-0002");
            db.ChangeTracker.Clear();
            await subscriptions.ReviewBankTransferPaymentRequestAsync(late!.Id, new ReviewBankTransferPaymentRequest("Approved", null), admin, "sales-review-key-0002");
            db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE speed_reading.bank_transfer_payment_requests SET \"ReviewedAt\" = now() - interval '15 days' WHERE \"Id\" = {late.Id}");
            Assert.Equal("Sales.WithdrawalPeriodOver", (await Assert.ThrowsAsync<BusinessRuleException>(() => sales.RequestWithdrawalAsync(buyer, late.Id, null))).Code);

            void Add(string name, object values)
            {
                var type = assembly.GetType($"SpeedReading.Infrastructure.Legacy.{name}")!;
                var entity = Activator.CreateInstance(type)!;
                foreach (var property in values.GetType().GetProperties())
                    type.GetProperty(property.Name)!.SetValue(entity, property.GetValue(values));
                db.Add(entity);
            }
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class Directory : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse(userIds.Select(id => new SpeedReadingUserDirectoryItem(id, "Test", "Veli", true) { Email = $"{id:N}@example.invalid" }).ToArray()));
        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(string? role, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
