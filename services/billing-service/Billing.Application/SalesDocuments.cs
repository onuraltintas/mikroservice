using System.Globalization;
using Billing.Domain;

namespace Billing.Application;

/// <summary>
/// The two texts a buyer reads and accepts before an order is opened. For a business sale (<see cref="Business"/>) they
/// are the business subscription information form and the business subscription contract.
/// </summary>
public sealed record SalesDocuments(string Version, string PreInformation, string DistanceSales, bool Business = false);

public sealed record SalesTerms(
    Seller Seller,
    string BuyerName,
    string? BuyerEmail,
    string PlanName,
    IReadOnlyList<string> ProductNames,
    int DurationDays,
    /// <summary>What the buyer pays: the net amount plus <see cref="VatAmount"/>.</summary>
    decimal Amount,
    string Currency,
    DateTime Date,
    /// <summary>Null when the price includes taxes.</summary>
    decimal? VatRate = null,
    decimal VatAmount = 0,
    /// <summary>Sold to a business: business contract, no withdrawal.</summary>
    bool Business = false);

/// <summary>
/// One pre-information form and one distance-sales contract for every product (billing-service-plan.md, section 13);
/// the product and plan names are the variables. The wording follows the Koçluk documents of 13.11.2026 and the Hızlı
/// Okuma documents of 14.11.2026 (Mesafeli Sözleşmeler Yönetmeliği) and is reviewed by the lawyer before the release.
/// A new wording must get a new <see cref="Version"/>. What a buyer accepted is kept with the order.
/// </summary>
public static class SalesDocumentTexts
{
    /// <summary>
    /// Consumer documents (pre-information form and distance-sales contract). Their wording is unchanged for a price that
    /// includes taxes; only a plan with a net price (VAT added) gets the net / VAT / total sentence.
    /// </summary>
    public const string Version = "2026-11-15";
    /// <summary>Business documents (maxkampüs): information form and subscription contract, no withdrawal.</summary>
    public const string BusinessVersion = "2026-11-20-kurumsal";

    public static string VersionFor(bool business) => business ? BusinessVersion : Version;

    /// <summary>The price sentence of both kinds of documents.</summary>
    public static string PriceLine(SalesTerms terms)
    {
        if (terms.VatRate is not { } rate) return $"Ödenecek toplam tutar {Money(terms.Amount, terms.Currency)} (vergiler dahil).";
        var net = terms.Amount - terms.VatAmount;
        return $"Net tutar {Money(net, terms.Currency)}, KDV (%{rate.ToString("0.##", Turkish)}) {Money(terms.VatAmount, terms.Currency)}, ödenecek toplam tutar {Money(terms.Amount, terms.Currency)}.";
    }

    /// <summary>"600,00 TL": the contract names the lira as people write it, not by its ISO code (20.11.2026 live test).</summary>
    private static string Money(decimal amount, string currency) =>
        $"{amount.ToString("N2", Turkish)} {(currency == "TRY" ? "TL" : currency)}";
    public const int WithdrawalDays = 14;
    private static readonly CultureInfo Turkish = new("tr-TR");
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    public static SalesDocuments Render(SalesTerms terms)
    {
        if (terms.Business) return RenderBusiness(terms);
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
        var price = PriceLine(terms);
        var date = terms.Date.Add(TurkeyOffset).ToString("dd.MM.yyyy", Turkish);
        var products = string.Join(", ", terms.ProductNames);
        var service = terms.ProductNames.Count > 1
            ? $"{terms.PlanName} paketi ({products})"
            : $"{products} {terms.PlanName} aboneliği";
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
            $"{service}: satıcının çevrim içi eğitim platformuna {terms.DurationDays} gün süreyle erişim. Hizmet, ödemenin onaylandığı gün başlar. Süren bir abonelik bitmeden yapılan yenilemede yeni dönem, mevcut dönemin bittiği gün başlar.",
            "",
            "4. Fiyat ve ödeme",
            price,
            "Ödeme banka havalesi / EFT ile yapılır. Sipariş açıldığında gösterilen ödeme kodu havale açıklamasına yazılır.",
            "Ödeme onaylanmadan hizmet başlamaz. Abonelik kendiliğinden yenilenmez ve otomatik ödeme alınmaz.",
            "",
            "5. Cayma hakkı",
            $"Alıcı, ödemenin onaylandığı günden başlayarak {WithdrawalDays} gün içinde gerekçe göstermeden cayma hakkını kullanabilir. Talep, platformdaki \"{withdrawalPlace}\" bölümünden ya da satıcının e-posta adresine yazılı olarak iletilir. Cayma halinde ödenen tutar, talebin satıcıya ulaştığı günden başlayarak en geç {WithdrawalDays} gün içinde alıcının ödeme yaptığı hesaba iade edilir. İade, ödenen tutarın tamamıdır; hizmetin kullanıldığı günler için kesinti yapılmaz.",
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
            $"Bu sözleşmenin konusu, alıcının satıcıya ait çevrim içi eğitim platformunda {service} hizmetini {terms.DurationDays} gün süreyle satın almasına ilişkin tarafların hak ve yükümlülükleridir. Sözleşme, 6502 sayılı Tüketicinin Korunması Hakkında Kanun ve Mesafeli Sözleşmeler Yönetmeliği hükümlerine tabidir.",
            "",
            "Madde 3 – Bedel ve ödeme",
            price,
            "Ödeme banka havalesi / EFT ile yapılır ve satıcının onayıyla kesinleşir.",
            "",
            "Madde 4 – İfa",
            $"Abonelik, ödemenin onaylandığı gün başlar ve {terms.DurationDays} gün sürer. Süren bir abonelik bitmeden yapılan yenilemede yeni dönem, mevcut dönemin bittiği gün başlar ve {terms.DurationDays} gün sürer. Satıcı hizmeti platform üzerinden sunar; bakım ve zorunlu kesintiler dışında erişimi açık tutar. Hizmetten elde edilecek sonuçlar kullanıcıya göre değişir; belirli bir sonuç taahhüt edilmez.",
            "",
            "Madde 5 – Cayma hakkı",
            $"Alıcı, ödemenin onaylandığı günden başlayarak {WithdrawalDays} gün içinde gerekçe göstermeden cayma hakkını kullanabilir. Talep platformdaki \"{withdrawalPlace}\" bölümünden ya da satıcının e-posta adresine yazılı olarak iletilir. Satıcı, talebin kendisine ulaştığı günden başlayarak en geç {WithdrawalDays} gün içinde bedelin tamamını alıcının ödeme yaptığı hesaba iade eder; hizmetin kullanıldığı günler için kesinti yapılmaz. Cayma onaylandığında abonelik sona erer." +
                (terms.ProductNames.Count > 1 ? " Paket tek bir hizmettir; cayma paketin tamamını kapsar." : string.Empty),
            "",
            "Madde 6 – Fatura",
            "Fatura, ödemenin onaylanmasından sonra satıcı tarafından düzenlenir ve alıcının kayıtlı e-posta adresine gönderilir.",
            "",
            "Madde 7 – Kişisel veriler",
            "Alıcının kişisel verileri, platformun aydınlatma metni ve 6698 sayılı Kişisel Verilerin Korunması Kanunu kapsamında işlenir.",
            "",
            "Madde 8 – Uyuşmazlık",
            "Uyuşmazlıklarda, ilgili mevzuatta belirlenen parasal sınırlar içinde alıcının yerleşim yerindeki Tüketici Hakem Heyeti ya da Tüketici Mahkemesi yetkilidir.",
            "",
            "Madde 9 – Yürürlük",
            $"Alıcı, bu sözleşmeyi ve ön bilgilendirme formunu okuyup onayladığını kabul eder. Sözleşme, alıcının onayıyla {date} tarihinde elektronik ortamda kurulmuştur.");

        return new SalesDocuments(Version, preInformation, distanceSales);
    }

    private static string SellerBlock(Seller seller)
    {
        var lines = new List<string>
        {
            $"Unvan: {seller.Title?.Trim()}",
            $"Adres: {seller.Address?.Trim()}",
            $"Vergi dairesi / no: {seller.TaxOffice?.Trim()} / {seller.TaxNumber?.Trim()}"
        };
        if (!string.IsNullOrWhiteSpace(seller.MersisNumber)) lines.Add($"MERSİS no: {seller.MersisNumber.Trim()}");
        lines.Add($"E-posta: {seller.Email?.Trim()}");
        lines.Add($"Telefon: {seller.Phone?.Trim()}");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// A sale to a business (maxkampüs; user decisions 20.11.2026: VAT added to a net price, no refund). The buyer states
    /// that it buys for its trade or profession, so the consumer withdrawal right does not apply. Reviewed by the lawyer
    /// before the release, like the consumer texts.
    /// </summary>
    private static SalesDocuments RenderBusiness(SalesTerms terms)
    {
        var sellerBlock = SellerBlock(terms.Seller);
        var buyer = $"Alıcı: {terms.BuyerName}\nE-posta: {(string.IsNullOrWhiteSpace(terms.BuyerEmail) ? "-" : terms.BuyerEmail.Trim())}";
        var price = PriceLine(terms);
        var date = terms.Date.Add(TurkeyOffset).ToString("dd.MM.yyyy", Turkish);
        var service = $"{string.Join(", ", terms.ProductNames)} {terms.PlanName} aboneliği";

        var information = string.Join("\n",
            "KURUMSAL ABONELİK BİLGİ FORMU",
            $"(Sürüm {BusinessVersion} · {date})",
            "",
            "1. Satıcı",
            sellerBlock,
            "",
            "2. Alıcı",
            buyer,
            "",
            "3. Hizmet",
            $"{service}: satıcının çevrim içi kurs ve özel ders yönetimi yazılımına {terms.DurationDays} gün süreyle erişim. Hizmet, ödemenin onaylandığı gün başlar. Süren bir abonelik bitmeden yapılan yenilemede yeni dönem, mevcut dönemin bittiği gün başlar.",
            "",
            "4. Fiyat ve ödeme",
            price,
            "Ödeme banka havalesi / EFT ile yapılır. Sipariş açıldığında gösterilen ödeme kodu havale açıklamasına yazılır.",
            "Ödeme onaylanmadan hizmet başlamaz. Abonelik kendiliğinden yenilenmez ve otomatik ödeme alınmaz.",
            "",
            "5. İptal ve iade",
            "Ödenen abonelik bedeli iade edilmez; hizmet abonelik süresinin sonuna kadar kullanılabilir. Satıcıdan kaynaklanan ve hizmetin uzun süre kullanılamamasına yol açan durumlar ile kanundan doğan haklar saklıdır.",
            "",
            "6. Alıcının beyanı",
            "Alıcı, hizmeti ticari veya mesleki faaliyeti için satın aldığını beyan eder. Bu nedenle tüketici mevzuatındaki cayma hakkı bu satışa uygulanmaz.");

        var contract = string.Join("\n",
            "KURUMSAL ABONELİK SÖZLEŞMESİ",
            $"(Sürüm {BusinessVersion} · {date})",
            "",
            "Madde 1 – Taraflar",
            "SATICI",
            sellerBlock,
            "",
            "ALICI",
            buyer,
            "",
            "Madde 2 – Konu",
            $"Bu sözleşmenin konusu, alıcının ticari veya mesleki faaliyeti için {service} hizmetini {terms.DurationDays} gün süreyle satın almasına ilişkin tarafların hak ve yükümlülükleridir. Hizmetin kullanımı, maxkampus.com'da yayımlanan kullanım ve abonelik koşullarına tabidir.",
            "",
            "Madde 3 – Bedel ve ödeme",
            price,
            "Ödeme banka havalesi / EFT ile yapılır ve satıcının onayıyla kesinleşir.",
            "",
            "Madde 4 – İfa",
            $"Abonelik, ödemenin onaylandığı gün başlar ve {terms.DurationDays} gün sürer. Süren bir abonelik bitmeden yapılan yenilemede yeni dönem, mevcut dönemin bittiği gün başlar. Satıcı hizmeti çevrim içi sunar; bakım ve zorunlu kesintiler dışında erişimi açık tutar.",
            "",
            "Madde 5 – İptal ve iade",
            "Ödenen abonelik bedeli iade edilmez; alıcı hizmeti abonelik süresinin sonuna kadar kullanabilir. Abonelik kendiliğinden yenilenmez. Satıcıdan kaynaklanan ve hizmetin uzun süre kullanılamamasına yol açan durumlar ile kanundan doğan haklar saklıdır.",
            "",
            "Madde 6 – Süre sonu ve veriler",
            "Abonelik süresi bittiğinde ve yenilenmediğinde panel salt okunur olur. Süre bitiminden bir yıl sonra yenileme yapılmamışsa alıcının panel verileri silinir; silmeden 30 gün önce alıcıya e-postayla bilgi verilir.",
            "",
            "Madde 7 – Fatura",
            "Fatura, ödemenin onaylanmasından sonra satıcı tarafından düzenlenir ve alıcının kayıtlı e-posta adresine gönderilir.",
            "",
            "Madde 8 – Kişisel veriler",
            "Alıcının panele girdiği öğrenci, veli ve öğretmen bilgilerinde alıcı veri sorumlusudur; satıcı bu verileri yalnız hizmeti sunmak için alıcı adına işler. Satıcının kendi işlediği veriler aydınlatma metninde açıklanır.",
            "",
            "Madde 9 – Uyuşmazlık",
            "Bu sözleşmeye Türk hukuku uygulanır. Uyuşmazlıklarda Yozgat mahkemeleri ve icra daireleri yetkilidir.",
            "",
            "Madde 10 – Yürürlük",
            $"Alıcı, bu sözleşmeyi ve kurumsal abonelik bilgi formunu okuyup onayladığını kabul eder. Sözleşme, alıcının onayıyla {date} tarihinde elektronik ortamda kurulmuştur.");

        return new SalesDocuments(BusinessVersion, information, contract, Business: true);
    }
}
