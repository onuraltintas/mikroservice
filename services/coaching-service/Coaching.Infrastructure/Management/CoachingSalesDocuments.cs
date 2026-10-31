using System.Globalization;
using Coaching.Domain.Entities;

namespace Coaching.Infrastructure.Management;

public sealed record CoachingSalesDocuments(string Version, string PreInformation, string DistanceSales);

/// <summary>What the documents are filled with: who sells, who buys, what and for how much.</summary>
public sealed record CoachingSalesTerms(CoachingSubscriptionSettings Seller, string BuyerName, string? BuyerEmail, string PlanName,
    int DurationDays, decimal ListPrice, decimal Discount, decimal Amount, string Currency, string? CouponCode, DateTime Date,
    string PaymentInstructions);

/// <summary>
/// Roadmap O10: the pre-information form and the distance-sales contract shown before a bank-transfer purchase, after the
/// Mesafeli Sözleşmeler Yönetmeliği. These are DRAFTS prepared for the platform; a lawyer should review them before use.
/// A new wording must get a new <see cref="Version"/>; what a buyer accepted is kept with their transfer.
/// </summary>
public static class CoachingSalesDocumentTexts
{
    public const string Version = "2026-10-31";
    private static readonly CultureInfo Turkish = new("tr-TR");

    public static CoachingSalesDocuments Render(CoachingSalesTerms terms)
    {
        var seller = terms.Seller;
        string Value(string? value, string missing) => string.IsNullOrWhiteSpace(value) ? $"[{missing} girilmedi]" : value.Trim();
        var sellerBlock = string.Join("\n",
            $"Unvan: {Value(seller.SellerTitle, "Satıcı unvanı")}",
            $"Adres: {Value(seller.SellerAddress, "Satıcı adresi")}",
            $"Vergi dairesi / no: {Value(seller.SellerTaxOffice, "Vergi dairesi")} / {Value(seller.SellerTaxNumber, "Vergi numarası")}",
            string.IsNullOrWhiteSpace(seller.SellerMersisNumber) ? null : $"MERSİS no: {seller.SellerMersisNumber.Trim()}",
            $"E-posta: {Value(seller.SellerEmail, "Satıcı e-postası")}",
            $"Telefon: {Value(seller.SellerPhone, "Satıcı telefonu")}").Replace("\n\n", "\n");
        string Money(decimal amount) => $"{amount.ToString("N2", Turkish)} {terms.Currency}";
        var price = terms.Discount > 0
            ? $"Liste fiyatı {Money(terms.ListPrice)}, {terms.CouponCode} indirim kodu ile {Money(terms.Discount)} indirim; ödenecek toplam tutar {Money(terms.Amount)} (vergiler dahil)."
            : $"Ödenecek toplam tutar {Money(terms.Amount)} (vergiler dahil).";
        // Coaching audit K1: the contract is dated on the Turkish calendar day, not the UTC one.
        var date = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(terms.Date, DateTimeKind.Utc), Coaching.Domain.CoachingTime.Turkey)
            .ToString("dd.MM.yyyy", Turkish);
        var buyer = $"Ad soyad: {terms.BuyerName}\nE-posta: {(string.IsNullOrWhiteSpace(terms.BuyerEmail) ? "-" : terms.BuyerEmail)}";

        var preInformation = $"""
            ÖN BİLGİLENDİRME FORMU
            (Sürüm {Version} · {date})

            1. Satıcı
            {sellerBlock}

            2. Alıcı
            {buyer}

            3. Hizmet
            "{terms.PlanName}" koçluk aboneliği: çevrim içi koçluk platformuna {terms.DurationDays} gün süreyle erişim. Hizmet, ödemenin satıcı tarafından onaylandığı gün başlar.
            Süren bir abonelik bitmeden yapılan yenilemede (ya da aynı türde yeni bir planda) yeni dönem, mevcut dönemin bittiği gün başlar; öğretmen
            planında daha büyük plana geçişte yeni plan onay günü geçerli olur ve ödenen gün sayısı mevcut dönemin sonuna eklenir.

            4. Fiyat ve ödeme
            {price}
            Ödeme banka havalesi / EFT ile yapılır. {terms.PaymentInstructions}
            Ödeme onaylanmadan hizmet başlamaz. Onaylanmayan ya da 14 gün içinde sonuçlanmayan bildirimler kendiliğinden düşer.

            5. Cayma hakkı
            Alıcı, ödemenin onaylandığı günden başlayarak 14 gün içinde gerekçe göstermeden cayma hakkını kullanabilir. Talep, platformdaki
            "Ödemelerim" bölümünden ya da satıcının e-posta adresine yazılı olarak iletilir. Cayma halinde ödenen tutar, talebin satıcıya
            ulaştığı günden başlayarak en geç 14 gün içinde alıcının ödeme yaptığı hesaba iade edilir. Cayma süresi içinde hizmetin
            kullanıldığı günlere düşen tutar iadeden düşülebilir.

            6. Şikâyet ve itiraz
            Alıcı şikâyetlerini satıcının iletişim bilgilerine iletebilir; ayrıca ilgili mevzuat sınırları içinde Tüketici Hakem Heyetine
            ya da Tüketici Mahkemesine başvurabilir.
            """;

        var distanceSales = $"""
            MESAFELİ SATIŞ SÖZLEŞMESİ
            (Sürüm {Version} · {date})

            Madde 1 – Taraflar
            SATICI
            {sellerBlock}

            ALICI
            {buyer}

            Madde 2 – Konu
            Bu sözleşmenin konusu, alıcının satıcıya ait çevrim içi koçluk platformunda "{terms.PlanName}" aboneliğini {terms.DurationDays} gün
            süreyle satın almasına ilişkin tarafların hak ve yükümlülükleridir. Sözleşme, 6502 sayılı Tüketicinin Korunması Hakkında Kanun ve
            Mesafeli Sözleşmeler Yönetmeliği hükümlerine tabidir.

            Madde 3 – Bedel ve ödeme
            {price}
            Ödeme banka havalesi / EFT ile yapılır ve satıcının onayıyla kesinleşir.

            Madde 4 – İfa
            Abonelik, ödemenin onaylandığı gün başlar ve {terms.DurationDays} gün sürer. Süren bir abonelik bitmeden yapılan yenilemede (ya da aynı
            türde yeni bir planda) yeni dönem, mevcut dönemin bittiği gün başlar ve {terms.DurationDays} gün sürer; öğretmen planında daha büyük plana
            geçişte yeni plan onay günü geçerli olur ve ödenen gün sayısı mevcut dönemin sonuna eklenir. Satıcı hizmeti platform üzerinden sunar;
            bakım ve zorunlu kesintiler dışında erişimi açık tutar.

            Madde 5 – Cayma hakkı
            Alıcı, ödemenin onaylandığı günden başlayarak 14 gün içinde gerekçe göstermeden cayma hakkını kullanabilir. Talep platformdaki
            "Ödemelerim" bölümünden ya da satıcının e-posta adresine yazılı olarak iletilir. Satıcı, talebin kendisine ulaştığı günden
            başlayarak en geç 14 gün içinde bedeli alıcının ödeme yaptığı hesaba iade eder. Cayma süresi içinde hizmetin kullanıldığı günlere
            düşen tutar iadeden düşülebilir. Cayma onaylandığında abonelik sona erer.

            Madde 6 – Fatura
            Fatura, ödemenin onaylanmasından sonra satıcı tarafından düzenlenir ve platformdaki "Ödemelerim" bölümünden indirilebilir.

            Madde 7 – Kişisel veriler
            Alıcının kişisel verileri, platformun aydınlatma metni ve 6698 sayılı Kişisel Verilerin Korunması Kanunu kapsamında işlenir.

            Madde 8 – Uyuşmazlık
            Uyuşmazlıklarda, ilgili mevzuatta belirlenen parasal sınırlar içinde alıcının yerleşim yerindeki Tüketici Hakem Heyeti ya da
            Tüketici Mahkemesi yetkilidir.

            Madde 9 – Yürürlük
            Alıcı, bu sözleşmeyi ve ön bilgilendirme formunu okuyup onayladığını kabul eder. Sözleşme, alıcının onayıyla {date} tarihinde
            elektronik ortamda kurulmuştur.
            """;
        return new(Version, preInformation, distanceSales);
    }
}
