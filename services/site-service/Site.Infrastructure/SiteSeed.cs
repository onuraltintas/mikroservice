using Microsoft.EntityFrameworkCore;

namespace Site.Infrastructure;

/// <summary>
/// The first content of eduivme.com (user decisions 19.11.2026). Written once, when the database is empty; after that
/// everything is edited in the admin panel ("eduivme sitesi").
/// </summary>
public static class SiteSeed
{
    public const string CoursePage = "kurs-yonetimi";

    public static async Task<int> EnsureAsync(SiteDbContext db, TimeProvider clock, CancellationToken cancellationToken = default)
    {
        if (await db.Entries.AnyAsync(cancellationToken)) return 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var entries = Entries().ToList();
        foreach (var entry in entries)
        {
            entry.Id = Guid.NewGuid();
            entry.IsPublished = true;
            entry.Version = 1;
            entry.CreatedAt = now;
            entry.UpdatedAt = now;
        }
        db.Entries.AddRange(entries);
        await db.SaveChangesAsync(cancellationToken);
        return entries.Count;
    }

    private static IEnumerable<SiteEntry> Entries()
    {
        yield return new SiteEntry
        {
            Kind = SiteKinds.Site, Slug = "site", Title = "eduivme",
            Body = "Öğrenciler, veliler, öğretmenler ve kurumlar için eğitim ürünleri.",
            SeoTitle = "eduivme: öğrenmenin her adımı için eğitim ürünleri",
            SeoDescription = "maxkoç öğrenci koçluğu, Master Hızlı Okuma ve Kurs Yönetimi: öğrenciler, veliler, öğretmenler ve kurumlar için eduivme ürünleri."
        };
        yield return new SiteEntry
        {
            Kind = SiteKinds.Hero, Slug = "hero", Title = "Öğrenmenin her adımı için araçlar.",
            Body = "eduivme; öğrenciler, veliler, öğretmenler ve kurumlar için eğitim ürünleri geliştirir. Her ürün tek bir işi iyi yapar.",
            LinkLabel = "Ürünleri incele", LinkUrl = "#urunler",
            SecondaryLinkLabel = "Öğretmen ya da kurum olarak başla", SecondaryLinkUrl = "#kimler"
        };
        yield return Section(string.Empty, "products", "Ürünler", "Her ürünün kendi sayfası var. Karta tıklayarak ürünü yakından tanıyın.", 10);
        yield return Section(string.Empty, "audiences", "Kimler için", "İlkokuldan üniversiteye, her öğrenci ve onun yanındaki herkes için.", 20);
        yield return Section(string.Empty, "faq", "Sık sorulanlar", string.Empty, 30);

        yield return new SiteEntry
        {
            Kind = SiteKinds.Product, Slug = "maxkoc", Brand = "maxkoc", Title = "maxkoç", Subtitle = "Öğrenci koçluğu", SortOrder = 10,
            Body = "Kişiye göre haftalık çalışma planı, ödevler, deneme sonuçları ve koçla düzenli görüşmeler. Veli, öğrencinin izin verdiği kadarını kendi ekranından izler.",
            Highlights = ["Boş saatlere göre haftalık plan", "Öğrenci, veli ve koç aynı planı görür", "Ödev ve deneme takibi"],
            LinkLabel = "maxkoc.com", LinkUrl = "https://maxkoc.com"
        };
        yield return new SiteEntry
        {
            Kind = SiteKinds.Product, Slug = "master-hizli-okuma", Brand = "ho", Title = "Master Hızlı Okuma", Subtitle = "Okuma hızı ve anlama", SortOrder = 20,
            Body = "Seviye belirleme sınavıyla başlayan, her gün kısa egzersizlerle ilerleyen bir okuma programı. Hız ve anlama birlikte ölçülür.",
            Highlights = ["İlkokuldan yetişkine her seviye için", "Günde birkaç dakikalık egzersizler", "İlerleme grafikleri ve rozetler"],
            LinkLabel = "masterhizliokuma.com", LinkUrl = "https://masterhizliokuma.com"
        };
        yield return new SiteEntry
        {
            Kind = SiteKinds.Product, Slug = CoursePage, Brand = "km", Title = "Kurs Yönetimi", Subtitle = "Kurs ve özel ders yönetimi", SortOrder = 30,
            Body = "Dersler, gruplar, yoklama, ödevler ve öğrenci ödemeleri tek yerde. Öğrenciler derslerini ve ödevlerini kendi alanından izler.",
            Highlights = ["Haftalık ders programı ve yoklama", "Ödev verme ve değerlendirme", "Taksit ve paket takibi"],
            LinkLabel = "Ürün sayfasına git", LinkUrl = "/urunler/" + CoursePage
        };

        yield return Audience(string.Empty, "Öğrenci", "Planını, ödevlerini ve egzersizlerini tek yerden takip et.", "Öğrenci girişi", "https://maxkoc.com/auth/login", "school", 10);
        yield return Audience(string.Empty, "Veli", "Çocuğunun çalışmasını, onun izin verdiği kadarıyla izle.", "Veli girişi", "https://veli.maxkoc.com/auth/login", "users", 20);
        yield return Audience(string.Empty, "Öğretmen", "Öğrencilerinizi davet edin, ödev verin, ilerlemeyi görün.", "Öğretmen girişi", "https://ogretmen.eduivme.com/auth/login", "person", 30);
        yield return Audience(string.Empty, "Kurum", "Öğretmenlerinizi ve öğrencilerinizi tek panelden yönetin.", "Kurum girişi", "https://kurum.eduivme.com/auth/login", "building", 40);

        yield return Faq(string.Empty, "eduivme nedir?",
            "eduivme; maxkoç, Master Hızlı Okuma ve Kurs Yönetimi ürünlerinin çatı markasıdır. Her ürünün kendi sitesi ve kendi girişi vardır.", 10);
        yield return Faq(string.Empty, "Hangi ürün bana uygun?",
            "Düzenli bir çalışma planı ve koç desteği istiyorsanız maxkoç, okuma hızınızı ve anlamanızı geliştirmek istiyorsanız Master Hızlı Okuma, kurs ya da özel ders veriyorsanız Kurs Yönetimi size göre. Ürünleri birlikte de kullanabilirsiniz.", 20);
        yield return Faq(string.Empty, "Öğretmen ya da kurum olarak nasıl başlarım?",
            "Öğretmen girişinden ya da kurum girişinden hesap açın. Hesabınız açıldıktan sonra öğrencilerinizi davet eder, ödev ve programlarını oradan yönetirsiniz.", 30);
        yield return Faq(string.Empty, "Kişisel verilerim nasıl korunuyor?",
            "Kişisel verilerin hangi amaçla ve nasıl işlendiği KVKK aydınlatma metninde ve gizlilik politikasında anlatılır. maxkoç'ta veli, öğrencinin yalnız izin verdiği bilgileri görür.", 40);

        yield return new SiteEntry
        {
            Kind = SiteKinds.Page, Slug = CoursePage, Brand = "km", Title = "Kurs Yönetimi", SortOrder = 10,
            Subtitle = "Kursunuzu tek ekrandan yönetin.",
            Body = "Ders programı, yoklama, ödevler ve öğrenci ödemeleri bir arada. Öğretmenleriniz derslerine, öğrencileriniz kendi alanlarına odaklanır.",
            LinkLabel = "Hemen başla", LinkUrl = "https://ogretmen.eduivme.com/auth/register/teacher",
            SecondaryLinkLabel = "Giriş yap", SecondaryLinkUrl = "https://ogretmen.eduivme.com/auth/login",
            CtaTitle = "Kursunuzu bugün kurun.", CtaBody = "Hesap açmak ücretsiz.",
            SeoTitle = "Kurs Yönetimi: ders programı, yoklama, ödev ve ödeme takibi | eduivme",
            SeoDescription = "Kurslar, dershaneler ve özel ders veren öğretmenler için ders programı, yoklama, ödev ve öğrenci ödemeleri tek yerde."
        };
        yield return Section(CoursePage, "features", "Neler yapabilirsiniz", "Kursun günlük işleri için gereken her şey, ayrı ayrı araçlara gerek kalmadan.", 10);
        yield return Section(CoursePage, "audiences", "Kimler için", string.Empty, 20);
        yield return Section(CoursePage, "steps", "Nasıl başlanır", "Kurulum birkaç dakika sürer.", 30);
        yield return Section(CoursePage, "faq", "Sık sorulanlar", string.Empty, 40);
        yield return Feature("Ders programı", "Tek seferlik dersler ve haftalık seriler. Öğretmen ve derslik çakışmaları kayıttan önce yakalanır.", "calendar", 10);
        yield return Feature("Yoklama ve telafi", "Her derste yoklama alın; kaçan dersler için telafi hakkı tanımlayın.", "check", 20);
        yield return Feature("Ödevler", "Bir öğrenciye ya da gruba ödev verin, dosya ekleyin, teslimleri değerlendirin.", "assignment", 30);
        yield return Feature("Öğrenci ödemeleri", "Nakit ve havale tahsilatı, taksit ve ders paketi takibi; gecikenler tek listede.", "payment", 40);
        yield return Feature("Şube ve derslikler", "Birden çok şube ve derslik; kapasite derse göre denetlenir.", "building", 50);
        yield return Feature("Öğrenci alanı", "Öğrenci derslerini, yoklamasını, ödevlerini ve bakiyesini kendi ekranından görür.", "person", 60);
        yield return Audience(CoursePage, "Kurslar ve dershaneler", "Birden çok öğretmen, grup ve şube. Kurum yöneticisi bütün dersleri, yoklamaları ve tahsilatları görür.", null, null, "building", 10);
        yield return Audience(CoursePage, "Özel ders veren öğretmenler", "Bire bir ve küçük grup dersleri, ders paketleri ve ödevler; tek kişilik bir çalışma alanı.", null, null, "person", 20);
        yield return Step("Hesap açın", "Kurum ya da özel ders veren öğretmen olarak ücretsiz kaydolun.", 10);
        yield return Step("Kursunuzu kurun", "Derslerinizi, gruplarınızı ve ders programınızı ekleyin.", 20);
        yield return Step("Öğrencilerinizi davet edin", "Öğrenciler davetle katılır; derslerini ve ödevlerini kendi alanından izler.", 30);
        yield return Faq(CoursePage, "Kurs Yönetimi kimler için?",
            "Kurslar, dershaneler, etüt merkezleri ve özel ders veren öğretmenler için. Tek öğretmenle de, birden çok şubeyle de kullanılabilir.", 10);
        yield return Faq(CoursePage, "Öğrencilerin hesap açması gerekiyor mu?",
            "Hayır. Öğrencilerinizi hesap açmadan da kaydedebilir, derslerini ve yoklamalarını tutabilirsiniz. Ödevlerini ve programlarını kendileri görmek isteyen öğrenciler, gönderdiğiniz davetle hesap açar.", 20);
        yield return Faq(CoursePage, "Ödemeleri sistem üzerinden alabilir miyim?",
            "Nakit ya da havale ile aldığınız ödemeleri kaydedersiniz; taksitler, ders paketleri ve geciken ödemeler otomatik izlenir. Öğrenci kendi bakiyesini görür. Kartla çevrim içi ödeme şu an yok.", 30);
    }

    private static SiteEntry Section(string group, string slug, string title, string lead, int order) =>
        new() { Kind = SiteKinds.Section, Group = group, Slug = slug, Title = title, Body = lead, SortOrder = order };

    private static SiteEntry Audience(string group, string title, string text, string? label, string? url, string icon, int order) =>
        new() { Kind = SiteKinds.Audience, Group = group, Title = title, Body = text, LinkLabel = label, LinkUrl = url, Icon = icon, SortOrder = order };

    private static SiteEntry Faq(string group, string question, string answer, int order) =>
        new() { Kind = SiteKinds.Faq, Group = group, Title = question, Body = answer, SortOrder = order };

    private static SiteEntry Feature(string title, string text, string icon, int order) =>
        new() { Kind = SiteKinds.Feature, Group = CoursePage, Title = title, Body = text, Icon = icon, SortOrder = order };

    private static SiteEntry Step(string title, string text, int order) =>
        new() { Kind = SiteKinds.Step, Group = CoursePage, Title = title, Body = text, SortOrder = order };
}
