using System.Text;
using Billing.Application;
using Billing.Domain;
using Billing.Infrastructure;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Billing.IntegrationTests;

/// <summary>A made-up statement in the layout Halkbank exports (semicolons, Windows-1254, direction in "B/A").</summary>
internal static class HalkbankSample
{
    public const string Iban = "TR330006100519786457841326";

    public static byte[] File(string iban, params (string Date, string Reference, string Amount, string Direction, string Description)[] lines)
    {
        var text = new StringBuilder()
            .Append("Şube Kodu:                    ;0000                         ;;;;;\r\n")
            .Append("Şube Adı:                     ;ÖRNEK ŞB.                    ;;;;;\r\n")
            .Append("Hesap No:                     ;00000000                     ;;;;;\r\n")
            .Append($"IBAN No:                      ;{iban}                      ;;;;;\r\n")
            .Append("Sorgu Başlangıç Tarihi:       ;2026-08-17                   ;;;;;\r\n")
            .Append("Sorgu Bitiş Tarihi:           ;2026-11-15                   ;;;;;\r\n")
            .Append(";;;;;;;;;;\r\n")
            .Append("İŞLEM TARİHİ;BİM REF                   ;TUTAR                        ;B/A;BAKİYE                   ;AÇIKLAMA                      ;VALOR       ;REFERANS        ;MENÜ ;\r\n");
        foreach (var line in lines)
            text.Append($"{line.Date}  ;{line.Reference};        {line.Amount}    ;{line.Direction}  ;                1.000,00;{line.Description}                    ;{line.Date}  ;20260000HV000001;HVLE ;\r\n");
        text.Append(";;;;;;;;;;\r\n")
            .Append("\"Söz konusu hesap hareket bilgilerinin değiştirilmesi ihtimaline istinaden;taraflar arasındaki uyuşmazlıklarda Banka kayıtları esas alınır.\";;;;;\r\n")
            .Append("İnternet Sitesi Adresi: www.example.invalid;;;;;\r\n");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1254).GetBytes(text.ToString());
    }
}

public sealed class StatementRulesTests
{
    [Theory]
    [InlineData("HAVALE - TEST KİŞİ TARAFINDAN GÖNDERİLEN,EDU-7K4Q2M,TR33", "EDU-7K4Q2M")]
    [InlineData("edu 7k4q2m koçluk ödemesi", "EDU-7K4Q2M")]
    [InlineData("EDU7K4Q2M", "EDU-7K4Q2M")]
    [InlineData("ödeme EDU_7K4Q2M.", "EDU-7K4Q2M")]
    // Not a code: a letter outside the alphabet (O, I, L, 0, 1), too short, or glued to other letters.
    [InlineData("EDU-7K4Q2O", null)]
    [InlineData("EDU-7K4Q2", null)]
    [InlineData("XEDU-7K4Q2M", null)]
    [InlineData("EDU-7K4Q2M9", null)]
    [InlineData("EDUCATION FEE", null)]
    [InlineData("", null)]
    public void Payment_code_is_read_from_a_description_however_it_was_typed(string description, string? expected) =>
        Assert.Equal(expected, StatementRules.FindPaymentCode(description));

    [Theory]
    [InlineData("66.000,00", 66000.00)]
    [InlineData("                   801,74    ", 801.74)]
    [InlineData("342.393,55", 342393.55)]
    [InlineData("1.234", 1234)]
    [InlineData("1234.56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("200", 200)]
    [InlineData("+200,5 TL", 200.5)]
    [InlineData("-16,76", -16.76)]
    public void Amounts_are_read_in_turkish_and_plain_forms(string text, double expected) =>
        Assert.Equal((decimal)expected, StatementRules.ParseAmount(text));

    [Fact]
    public void What_is_not_an_amount_or_a_date_is_null()
    {
        Assert.Null(StatementRules.ParseAmount("A"));
        Assert.Null(StatementRules.ParseAmount(""));
        Assert.Null(StatementRules.ParseDate("Sorgu"));
        Assert.Null(StatementRules.ParseDate("32.13.2026"));
    }

    [Theory]
    [InlineData("08.09.2026  ", "2026-09-07T21:00:00Z")]
    [InlineData("08/09/2026 10:46", "2026-09-08T07:46:00Z")]
    [InlineData("2026-09-08", "2026-09-07T21:00:00Z")]
    [InlineData("46273", "2026-09-07T21:00:00Z")]
    public void Dates_are_turkey_time(string text, string expectedUtc) =>
        Assert.Equal(DateTime.Parse(expectedUtc).ToUniversalTime(), StatementRules.ParseDate(text));

    [Fact]
    public void The_halkbank_layout_is_recognised_and_only_money_coming_in_is_read()
    {
        var content = HalkbankSample.File(HalkbankSample.Iban,
            ("08.09.2026", "2026-09-08-10.46.56.075171", "200,00", "A", "HAVALE - TEST KİŞİ TARAFINDAN GÖNDERİLEN,EDU-7K4Q2M,TR33******"),
            ("08.09.2026", "2026-09-08-12.18.42.776000", "801,74", "B", "VERGİ TAHSİLATI"),
            ("09.09.2026", "2026-09-09-09.00.00.000001", "1.250,50", "A", "GELEN FAST - ÖRNEK ŞİRKET"));
        var rows = StatementRules.ReadCsv(StatementRules.DecodeText(content));

        var mapping = StatementRules.SuggestMapping(rows);
        // Empty rows are dropped when the file is read, so the header is the seventh row that has something in it.
        Assert.Equal(new StatementMapping(6, 0, 5, 2, null, 1, 3, "A"), mapping);
        Assert.Equal(HalkbankSample.Iban, StatementRules.StatementIban(rows, mapping!.HeaderRow));

        var lines = StatementRules.ReadLines(rows, mapping);
        Assert.Equal([200.00m, 1250.50m], lines.Select(line => line.Amount));
        Assert.Equal("2026-09-08-10.46.56.075171", lines[0].Reference);
        Assert.Contains("GÖNDERİLEN", lines[0].Description);
        Assert.Contains("ÖRNEK ŞİRKET", lines[1].Description);
    }

    [Fact]
    public void A_plain_csv_with_signed_amounts_and_commas_is_read_too()
    {
        var rows = StatementRules.ReadCsv("Tarih,Açıklama,Tutar\n15.11.2026,\"Ödeme, EDU-7K4Q2M\",\"200,00\"\n15.11.2026,Masraf,\"-5,00\"\n");
        var mapping = StatementRules.SuggestMapping(rows);
        Assert.Equal(new StatementMapping(0, 0, 1, 2, null, null), mapping);
        var line = Assert.Single(StatementRules.ReadLines(rows, mapping!));
        Assert.Equal(("Ödeme, EDU-7K4Q2M", 200m), (line.Description, line.Amount));
        Assert.Null(StatementRules.StatementIban(rows, 0));
    }

    [Fact]
    public void Twin_lines_get_different_fingerprints_and_the_same_line_the_same_one()
    {
        var line = new StatementLine(new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc), 200m, "EDU-7K4Q2M", null, null);
        var account = Guid.NewGuid();
        Assert.Equal(StatementRules.Fingerprint(account, line, 1), StatementRules.Fingerprint(account, line, 1));
        Assert.NotEqual(StatementRules.Fingerprint(account, line, 1), StatementRules.Fingerprint(account, line, 2));
        Assert.NotEqual(StatementRules.Fingerprint(account, line, 1), StatementRules.Fingerprint(Guid.NewGuid(), line, 1));
    }
}

/// <summary>Statement upload against a real PostgreSQL database: stored once, matched exactly, approved per product setting.</summary>
public sealed class BankStatementsPostgresTests : IAsyncLifetime
{
    private static readonly Guid Admin = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private readonly PostgresFixture _postgres = new();
    private DbContextOptions<BillingDbContext> _options = null!;
    private Guid _bankAccount;
    private Guid _monthly;
    private Guid _bundle;

    private sealed class NoEvents : IBillingEvents
    {
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
    }

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        _options = new DbContextOptionsBuilder<BillingDbContext>().UseBillingDatabase(_postgres.ConnectionString).Options;
        await using var db = new BillingDbContext(_options);
        await db.Database.MigrateAsync();
        var catalog = new BillingCatalog(db, TimeProvider.System);
        await catalog.UpdateSellerAsync(new SellerRequest("TEST Satıcı Ltd.", "Bir adres", "Daire", "1234567890", null, "satici@example.invalid", "0212", null), Admin);
        _bankAccount = (await catalog.CreateBankAccountAsync(new BankAccountRequest("Test Bankası", "TEST Satıcı Ltd.", HalkbankSample.Iban, null, "Halkbank", true, 1), Admin)).Id;
        var entitlements = (await catalog.GetProductsAsync()).SelectMany(item => item.Entitlements).ToDictionary(item => item.Key, item => item.Id);
        _monthly = (await catalog.CreatePlanAsync(new PlanRequest("TEST Aylık", null, BuyerType.Individual, 200, 30, 0, null, null, false, true, true, 0, [],
            [new PlanEntitlementRequest(entitlements["speed-reading.student"], EntitlementHolder.Buyer, null)]), Admin)).Id;
        _bundle = (await catalog.CreatePlanAsync(new PlanRequest("TEST Paket", null, BuyerType.Individual, 500, 90, 0, null, null, false, true, true, 0, [],
            [new PlanEntitlementRequest(entitlements["speed-reading.student"], EntitlementHolder.Buyer, null),
             new PlanEntitlementRequest(entitlements["coaching.student"], EntitlementHolder.Buyer, null)]), Admin)).Id;
    }

    public Task DisposeAsync() => _postgres.DisposeAsync();

    private BillingDbContext Db() => new(_options);
    private static BillingOrders Orders(BillingDbContext db) => new(db, new NoEvents(), TimeProvider.System);
    private static BillingBankStatements Statements(BillingDbContext db) => new(db, Orders(db), TimeProvider.System);

    private async Task SetApprovalAsync(Guid product, BankTransferApproval approval)
    {
        await using var db = Db();
        await new BillingCatalog(db, TimeProvider.System).UpdateProductSettingsAsync(product, new ProductSettingsRequest(true, approval), Admin);
    }

    private async Task<OrderView> OrderAsync(Guid plan)
    {
        await using var db = Db();
        return await Orders(db).CreateOrderAsync(new BillingUser(Guid.NewGuid(), "ogrenci@example.invalid", ["Student"]),
            new CreateOrderRequest(plan, SalesDocumentTexts.Version, "Deneme Öğrenci"));
    }

    private async Task<StatementImportResult> UploadAsync(byte[] content)
    {
        await using var db = Db();
        var statements = Statements(db);
        var preview = statements.Preview("HESAPEKSTRE.CSV", content);
        return await statements.ImportAsync(_bankAccount, "HESAPEKSTRE.CSV", content, preview.Suggested!, Admin);
    }

    private static (string, string, string, string, string) Incoming(string code, string amount, string reference = "2026-11-15-10.00.00.000001") =>
        ("15.11.2026", reference, amount, "A", $"HAVALE - TEST KİŞİ TARAFINDAN GÖNDERİLEN,{code},TR33******");

    [Fact]
    public async Task With_automatic_approval_an_exact_match_pays_the_order_by_itself_and_a_second_upload_adds_nothing()
    {
        await SetApprovalAsync(BillingSeed.SpeedReading, BankTransferApproval.Automatic);
        var order = await OrderAsync(_monthly);
        var file = HalkbankSample.File(HalkbankSample.Iban,
            Incoming(order.PaymentCode.Replace("-", " ").ToLowerInvariant(), "200,00"),
            ("15.11.2026", "2026-11-15-11.00.00.000002", "16,76", "B", "GİDEN FAST MASRAFI"),
            Incoming("açıklama yok", "75,00", "2026-11-15-12.00.00.000003"));

        var result = await UploadAsync(file);

        Assert.Equal((2, 2, 0, 0, 1), (result.Import.RowCount, result.Import.AddedCount, result.Import.DuplicateCount, result.Import.MatchedCount, result.Import.AutoApprovedCount));
        var approved = result.Transactions.Single(item => item.Amount == 200m);
        Assert.Equal((BankTransactionStatus.Approved, order.PaymentCode, order.Id), (approved.Status, approved.PaymentCode, approved.OrderId));
        Assert.Contains("Deneme Öğrenci", approved.OrderSummary);
        var loose = result.Transactions.Single(item => item.Amount == 75m);
        Assert.Equal((BankTransactionStatus.Unmatched, "Açıklamada ödeme kodu yok."), (loose.Status, loose.Note));

        await using (var db = Db())
        {
            var paid = (await Orders(db).GetOrdersAsync(OrderStatus.Paid, null)).Single();
            Assert.Equal(order.Id, paid.Order.Id);
            // Nobody reviewed it: the payment stands on the bank line.
            Assert.Null(paid.ReviewedBy);
            var payment = await db.Payments.SingleAsync();
            Assert.Equal((null, approved.Id), (payment.ApprovedBy, payment.BankTransactionId));
            Assert.Equal(1, await db.EntitlementGrants.CountAsync());
        }

        var again = await UploadAsync(file);
        Assert.Equal((2, 0, 2, 0), (again.Import.RowCount, again.Import.AddedCount, again.Import.DuplicateCount, again.Import.AutoApprovedCount));
        Assert.Empty(again.Transactions);
        await using var check = Db();
        Assert.Equal(2, await check.BankTransactions.CountAsync());
        Assert.Equal(1, await check.Payments.CountAsync());
    }

    [Fact]
    public async Task With_manual_approval_the_match_waits_for_the_admin_and_the_approval_takes_the_line_along()
    {
        await SetApprovalAsync(BillingSeed.SpeedReading, BankTransferApproval.Manual);
        var order = await OrderAsync(_monthly);

        var result = await UploadAsync(HalkbankSample.File(HalkbankSample.Iban, Incoming(order.PaymentCode, "200,00")));

        var matched = Assert.Single(result.Transactions);
        Assert.Equal((BankTransactionStatus.Matched, 1, 0), (matched.Status, result.Import.MatchedCount, result.Import.AutoApprovedCount));
        await using var db = Db();
        Assert.Equal(0, await db.Payments.CountAsync());
        await Orders(db).ApproveOrderAsync(order.Id, Admin, null);
        var line = await db.BankTransactions.AsNoTracking().SingleAsync();
        var payment = await db.Payments.AsNoTracking().SingleAsync();
        Assert.Equal((BankTransactionStatus.Approved, payment.Id, line.Id, Admin), (line.Status, line.PaymentId!.Value, payment.BankTransactionId!.Value, payment.ApprovedBy!.Value));
    }

    [Fact]
    public async Task A_bundle_follows_its_strictest_product()
    {
        await SetApprovalAsync(BillingSeed.SpeedReading, BankTransferApproval.Automatic);
        var order = await OrderAsync(_bundle);

        // Koçluk is still approved by hand, so the bundle waits.
        var waiting = await UploadAsync(HalkbankSample.File(HalkbankSample.Iban, Incoming(order.PaymentCode, "500,00")));
        Assert.Equal(BankTransactionStatus.Matched, Assert.Single(waiting.Transactions).Status);

        await SetApprovalAsync(BillingSeed.Coaching, BankTransferApproval.Automatic);
        var second = await OrderAsync(_bundle);
        var automatic = await UploadAsync(HalkbankSample.File(HalkbankSample.Iban, Incoming(second.PaymentCode, "500,00", "2026-11-15-13.00.00.000009")));
        Assert.Equal(BankTransactionStatus.Approved, Assert.Single(automatic.Transactions).Status);
    }

    [Fact]
    public async Task Anything_but_an_exact_match_stays_for_the_admin_with_the_reason()
    {
        await SetApprovalAsync(BillingSeed.SpeedReading, BankTransferApproval.Automatic);
        var underpaid = await OrderAsync(_monthly);
        var paidTwice = await OrderAsync(_monthly);
        var result = await UploadAsync(HalkbankSample.File(HalkbankSample.Iban,
            Incoming(underpaid.PaymentCode, "150,00", "2026-11-15-10.00.00.000001"),
            Incoming("EDU-ZZZZZZ", "200,00", "2026-11-15-10.00.00.000002"),
            Incoming(paidTwice.PaymentCode, "200,00", "2026-11-15-10.00.00.000003"),
            Incoming(paidTwice.PaymentCode, "200,00", "2026-11-15-10.00.00.000004")));

        Assert.Equal(1, result.Import.AutoApprovedCount);
        var notes = result.Transactions.Where(item => item.Status == BankTransactionStatus.Unmatched).Select(item => item.Note).ToArray();
        Assert.Equal(3, notes.Length);
        Assert.Contains("Tutar farklı: sipariş 200,00 TRY.", notes);
        Assert.Contains("EDU-ZZZZZZ kodlu sipariş bulunamadı.", notes);
        Assert.Contains("Bu sipariş zaten onaylanmış; ikinci ödeme olabilir.", notes);
        await using var db = Db();
        Assert.Equal(1, await db.Payments.CountAsync());
    }

    [Fact]
    public async Task The_admin_ties_a_line_without_a_code_to_an_order_of_the_same_amount_or_sets_it_aside()
    {
        await SetApprovalAsync(BillingSeed.SpeedReading, BankTransferApproval.Automatic);
        var order = await OrderAsync(_monthly);
        var other = await OrderAsync(_bundle);
        var result = await UploadAsync(HalkbankSample.File(HalkbankSample.Iban,
            Incoming("kod yazmayı unuttum", "200,00", "2026-11-15-10.00.00.000001"),
            Incoming("kira", "9.000,00", "2026-11-15-10.00.00.000002")));
        var line = result.Transactions.Single(item => item.Amount == 200m);
        var rent = result.Transactions.Single(item => item.Amount == 9000m);

        await using var db = Db();
        var statements = Statements(db);
        Assert.Equal("Billing.AmountMismatch", (await Assert.ThrowsAsync<BusinessRuleException>(() => statements.ApproveWithOrderAsync(line.Id, other.Id, Admin))).Code);
        Assert.Null(await statements.ApproveWithOrderAsync(Guid.NewGuid(), order.Id, Admin));
        var tied = await statements.ApproveWithOrderAsync(line.Id, order.Id, Admin);
        Assert.Equal((BankTransactionStatus.Approved, order.Id), (tied!.Status, tied.OrderId));
        // A line pays one order; a used line cannot be set aside.
        var third = await OrderAsync(_monthly);
        Assert.Equal("Billing.BankTransactionUsed", (await Assert.ThrowsAsync<BusinessRuleException>(() => statements.ApproveWithOrderAsync(line.Id, third.Id, Admin))).Code);
        Assert.Equal("Billing.BankTransactionUsed", (await Assert.ThrowsAsync<BusinessRuleException>(() => statements.SetIgnoredAsync(line.Id, true, null, Admin))).Code);

        Assert.Equal(BankTransactionStatus.Ignored, (await statements.SetIgnoredAsync(rent.Id, true, "Satış değil.", Admin))!.Status);
        Assert.Equal([rent.Id], (await statements.GetTransactionsAsync(BankTransactionStatus.Ignored)).Select(item => item.Id));
        Assert.Equal(BankTransactionStatus.Unmatched, (await statements.SetIgnoredAsync(rent.Id, false, null, Admin))!.Status);
        Assert.Single(await statements.GetImportsAsync());
    }

    [Fact]
    public async Task A_statement_of_another_account_an_empty_one_and_an_old_excel_file_are_refused_and_a_used_account_cannot_be_removed()
    {
        await using var db = Db();
        var statements = Statements(db);
        var foreign = HalkbankSample.File("TR320010009999901234567890", Incoming("EDU-7K4Q2M", "200,00"));
        var mapping = statements.Preview("x.csv", foreign).Suggested!;
        Assert.Equal("Billing.StatementWrongAccount", (await Assert.ThrowsAsync<BusinessRuleException>(() => statements.ImportAsync(_bankAccount, "x.csv", foreign, mapping, Admin))).Code);
        var outgoingOnly = HalkbankSample.File(HalkbankSample.Iban, ("15.11.2026", "2026-11-15-11.00.00.000002", "16,76", "B", "MASRAF"));
        Assert.Equal("Billing.StatementEmpty", (await Assert.ThrowsAsync<BusinessRuleException>(() => statements.ImportAsync(_bankAccount, "x.csv", outgoingOnly, mapping, Admin))).Code);
        Assert.Equal("Billing.StatementInvalid", Assert.Throws<BusinessRuleException>(() => statements.Preview("x.xls", [0xD0, 0xCF, 0x11, 0xE0, 0, 0])).Code);
        Assert.Equal("Billing.StatementInvalid", Assert.Throws<BusinessRuleException>(() => statements.Preview("x.xlsx", "PK not a real workbook"u8.ToArray())).Code);
        Assert.Equal("Billing.BankAccountNotFound", (await Assert.ThrowsAsync<BusinessRuleException>(() => statements.ImportAsync(Guid.NewGuid(), "x.csv", foreign, mapping, Admin))).Code);

        await UploadAsync(HalkbankSample.File(HalkbankSample.Iban, Incoming("kod yok", "10,00")));
        await using var after = Db();
        Assert.Equal("Billing.BankAccountInUse",
            (await Assert.ThrowsAsync<BusinessRuleException>(() => new BillingCatalog(after, TimeProvider.System).RemoveBankAccountAsync(_bankAccount))).Code);
    }
}
