using System.Globalization;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Application.Subscription;

/// <summary>The two texts a buyer reads and accepts before sending a bank-transfer notice.</summary>
public sealed record SpeedReadingSalesDocuments(string Version, string PreInformation, string DistanceSales);

/// <summary>Who sells: shown at the top of both documents. Entered by an admin; a sale needs it complete.</summary>
public sealed record SpeedReadingSellerInfo(
    string? Title,
    string? Address,
    string? TaxOffice,
    string? TaxNumber,
    string? MersisNumber,
    string? Email,
    string? Phone)
{
    /// <summary>A contract with a missing seller line is not a contract: everything but MERSİS is needed.</summary>
    public bool IsComplete => !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Address)
        && !string.IsNullOrWhiteSpace(TaxOffice) && !string.IsNullOrWhiteSpace(TaxNumber)
        && !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Phone);
}

public sealed record SpeedReadingSalesTerms(
    SpeedReadingSellerInfo Seller,
    string BuyerName,
    string? BuyerEmail,
    string PlanName,
    int DurationDays,
    decimal Amount,
    string Currency,
    DateTime Date,
    string PaymentInstructions);

public sealed record BankTransferWithdrawalRequest(string? Reason);
public sealed record ReviewBankTransferWithdrawalRequest(bool Approve, string? Note);

/// <summary>
/// Legal check Y1 (user decision 14.11.2026): Hızlı Okuma gets the same pre-information form, distance-sales contract,
/// acceptance and withdrawal flow as Koçluk before it is put on sale. The wording follows the Koçluk documents of
/// 13.11.2026 (Mesafeli Sözleşmeler Yönetmeliği); a new wording must get a new <see cref="Version"/>. What a buyer
/// accepted is kept with the payment request.
/// </summary>
public static class SpeedReadingSalesDocumentTexts
{
    public const string Version = "2026-11-14";
    public const int WithdrawalDays = 14;
    public const string WithdrawalRequested = "Requested";
    public const string WithdrawalApproved = "Approved";
    public const string WithdrawalRejected = "Rejected";
    public const string ApprovedWithdrawalNote = "Cayma talebiniz onaylandı; ödemeniz ödeme yaptığınız hesaba iade edilecek.";
    private static readonly CultureInfo Turkish = new("tr-TR");

    public static SpeedReadingSalesDocuments Render(SpeedReadingSalesTerms terms)
    {
        var seller = terms.Seller;
        var sellerLines = new List<string>
        {
            $"Unvan: {seller.Title?.Trim()}",
            $"Adres: {seller.Address?.Trim()}",
            $"Vergi dairesi / no: {seller.TaxOffice?.Trim()} / {seller.TaxNumber?.Trim()}"
        };
        if (!string.IsNullOrWhiteSpace(seller.MersisNumber)) sellerLines.Add($"MERSİS no: {seller.MersisNumber.Trim()}");
        sellerLines.Add($"E-posta: {seller.Email?.Trim()}");
        sellerLines.Add($"Telefon: {seller.Phone?.Trim()}");
        var sellerBlock = string.Join("\n", sellerLines);
        var buyer = $"Ad soyad: {terms.BuyerName}\nE-posta: {(string.IsNullOrWhiteSpace(terms.BuyerEmail) ? "-" : terms.BuyerEmail.Trim())}";
        var price = $"Ödenecek toplam tutar {terms.Amount.ToString("N2", Turkish)} {terms.Currency} (vergiler dahil).";
        var date = SpeedReadingReportCalendar.LocalTime(terms.Date).ToString("dd.MM.yyyy", Turkish);
        var instructions = string.IsNullOrWhiteSpace(terms.PaymentInstructions) ? string.Empty : " " + terms.PaymentInstructions.Trim();
        const string withdrawalPlace = "Aboneliğim";

        var preInformation = string.Join("\n",
            "ÖN BİLGİLENDİRME FORMU",
            $"(Sürüm {Version} · {date})",
            "",
            "1. Satıcı",
            sellerBlock,
            "",
            "2. Alıcı",
            buyer,
            "",
            "3. Hizmet",
            $"{terms.PlanName} Hızlı Okuma aboneliği: çevrim içi hızlı okuma platformuna {terms.DurationDays} gün süreyle erişim. Hizmet, ödemenin satıcı tarafından onaylandığı gün başlar. Süren bir abonelik bitmeden yapılan yenilemede yeni dönem, mevcut dönemin bittiği gün başlar.",
            "",
            "4. Fiyat ve ödeme",
            price,
            $"Ödeme banka havalesi / EFT ile yapılır.{instructions}",
            "Ödeme onaylanmadan hizmet başlamaz. Abonelik kendiliğinden yenilenmez ve otomatik ödeme alınmaz.",
            "",
            "5. Cayma hakkı",
            $"Alıcı, ödemenin onaylandığı günden başlayarak {WithdrawalDays} gün içinde gerekçe göstermeden cayma hakkını kullanabilir. Talep, platformdaki \"{withdrawalPlace}\" sayfasından ya da satıcının e-posta adresine yazılı olarak iletilir. Cayma halinde ödenen tutar, talebin satıcıya ulaştığı günden başlayarak en geç {WithdrawalDays} gün içinde alıcının ödeme yaptığı hesaba iade edilir. İade, ödenen tutarın tamamıdır; hizmetin kullanıldığı günler için kesinti yapılmaz.",
            "",
            "6. Şikâyet ve itiraz",
            "Alıcı şikâyetlerini satıcının iletişim bilgilerine iletebilir; ayrıca ilgili mevzuat sınırları içinde Tüketici Hakem Heyetine ya da Tüketici Mahkemesine başvurabilir.");

        var distanceSales = string.Join("\n",
            "MESAFELİ SATIŞ SÖZLEŞMESİ",
            $"(Sürüm {Version} · {date})",
            "",
            "Madde 1 – Taraflar",
            "SATICI",
            sellerBlock,
            "",
            "ALICI",
            buyer,
            "",
            "Madde 2 – Konu",
            $"Bu sözleşmenin konusu, alıcının satıcıya ait çevrim içi hızlı okuma platformunda {terms.PlanName} aboneliğini {terms.DurationDays} gün süreyle satın almasına ilişkin tarafların hak ve yükümlülükleridir. Sözleşme, 6502 sayılı Tüketicinin Korunması Hakkında Kanun ve Mesafeli Sözleşmeler Yönetmeliği hükümlerine tabidir.",
            "",
            "Madde 3 – Bedel ve ödeme",
            price,
            "Ödeme banka havalesi / EFT ile yapılır ve satıcının onayıyla kesinleşir.",
            "",
            "Madde 4 – İfa",
            $"Abonelik, ödemenin onaylandığı gün başlar ve {terms.DurationDays} gün sürer. Süren bir abonelik bitmeden yapılan yenilemede yeni dönem, mevcut dönemin bittiği gün başlar ve {terms.DurationDays} gün sürer. Satıcı hizmeti platform üzerinden sunar; bakım ve zorunlu kesintiler dışında erişimi açık tutar. Okuma hızı ve anlama sonuçları kullanıcıya, metne ve cihaza göre değişir; belirli bir sonuç taahhüt edilmez.",
            "",
            "Madde 5 – Cayma hakkı",
            $"Alıcı, ödemenin onaylandığı günden başlayarak {WithdrawalDays} gün içinde gerekçe göstermeden cayma hakkını kullanabilir. Talep platformdaki \"{withdrawalPlace}\" sayfasından ya da satıcının e-posta adresine yazılı olarak iletilir. Satıcı, talebin kendisine ulaştığı günden başlayarak en geç {WithdrawalDays} gün içinde bedelin tamamını alıcının ödeme yaptığı hesaba iade eder; hizmetin kullanıldığı günler için kesinti yapılmaz. Cayma onaylandığında abonelik sona erer.",
            "",
            "Madde 6 – Fatura",
            $"Fatura, ödemenin onaylanmasından sonra satıcı tarafından düzenlenir ve alıcı tarafından platformdaki \"{withdrawalPlace}\" sayfasından indirilebilir.",
            "",
            "Madde 7 – Kişisel veriler",
            "Alıcının kişisel verileri, platformun aydınlatma metni ve 6698 sayılı Kişisel Verilerin Korunması Kanunu kapsamında işlenir.",
            "",
            "Madde 8 – Uyuşmazlık",
            "Uyuşmazlıklarda, ilgili mevzuatta belirlenen parasal sınırlar içinde alıcının yerleşim yerindeki Tüketici Hakem Heyeti ya da Tüketici Mahkemesi yetkilidir.",
            "",
            "Madde 9 – Yürürlük",
            $"Alıcı, bu sözleşmeyi ve ön bilgilendirme formunu okuyup onayladığını kabul eder. Sözleşme, alıcının onayıyla {date} tarihinde elektronik ortamda kurulmuştur.");

        return new SpeedReadingSalesDocuments(Version, preInformation, distanceSales);
    }
}

/// <summary>Sales documents, seller details and withdrawal (cayma) of Hızlı Okuma bank-transfer purchases.</summary>
public interface ISpeedReadingSales
{
    Task<SpeedReadingSellerInfo> GetSellerAsync(CancellationToken cancellationToken = default);
    Task<SpeedReadingSellerInfo> UpdateSellerAsync(SpeedReadingSellerInfo request, Guid actorId, CancellationToken cancellationToken = default);
    /// <summary>The documents for a plan the buyer is about to pay for; null when the plan is not on sale.</summary>
    Task<SpeedReadingSalesDocuments?> RenderAsync(Guid userId, Guid planId, CancellationToken cancellationToken = default);
    /// <summary>The documents accepted with a request; null when it is not the viewer's or predates the documents.</summary>
    Task<SpeedReadingSalesDocuments?> GetAcceptedAsync(Guid userId, Guid requestId, CancellationToken cancellationToken = default);
    Task<BankTransferPaymentRequestSummary?> RequestWithdrawalAsync(Guid userId, Guid requestId, string? reason, CancellationToken cancellationToken = default);
    Task<BankTransferPaymentRequestSummary?> ReviewWithdrawalAsync(Guid requestId, ReviewBankTransferWithdrawalRequest request, Guid actorId, CancellationToken cancellationToken = default);
    /// <summary>Records the manually issued invoice (a PDF) of an approved payment; null when the payment is unknown.</summary>
    Task<BankTransferPaymentRequestSummary?> AttachInvoiceAsync(Guid actorId, Guid requestId, string invoiceNumber, DateOnly issuedOn, string fileName, Stream content, CancellationToken cancellationToken = default);
    Task<BankTransferPaymentRequestSummary?> RemoveInvoiceAsync(Guid requestId, CancellationToken cancellationToken = default);
    /// <summary>The invoice of one of the viewer's payments, or of any payment for an admin; null when there is none.</summary>
    Task<SpeedReadingInvoiceFile?> OpenInvoiceAsync(Guid viewerId, Guid requestId, bool asAdmin, CancellationToken cancellationToken = default);
}

public sealed record SpeedReadingInvoiceFile(byte[] Content, string FileName);

/// <summary>Sends the expiry reminders of individual subscriptions; returns how many notices were staged.</summary>
public interface ISpeedReadingSubscriptionLifecycle
{
    Task<int> ProcessAsync(CancellationToken cancellationToken = default);
}
