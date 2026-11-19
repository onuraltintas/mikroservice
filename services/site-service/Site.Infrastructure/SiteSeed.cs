using Microsoft.EntityFrameworkCore;

namespace Site.Infrastructure;

/// <summary>
/// The first content of the sites (user decisions 19.11.2026). Each site is written once, when it has no entries; after
/// that everything is edited in the admin panel. <see cref="MoveCourseManagementAsync"/> is the one change written into
/// content that already exists: Kurs Yönetimi became maxkampüs on its own domain.
/// </summary>
public static class SiteSeed
{
    public const string CoursePage = "kurs-yonetimi";

    public const string MaxkampusUrl = "https://maxkampus.com";

    public static async Task<int> EnsureAsync(SiteDbContext db, TimeProvider clock, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var written = 0;
        if (!await db.Entries.AnyAsync(item => item.Site == SiteRules.Eduivme, cancellationToken))
            written += Add(db, Entries(), SiteRules.Eduivme, now);
        else
            written += await MoveCourseManagementAsync(db, now, cancellationToken);
        if (!await db.Entries.AnyAsync(item => item.Site == SiteRules.Maxkampus, cancellationToken))
            written += Add(db, MaxkampusSeed.Entries(), SiteRules.Maxkampus, now);
        await db.SaveChangesAsync(cancellationToken);
        return written;
    }

    private static int Add(SiteDbContext db, IEnumerable<SiteEntry> source, string site, DateTime now)
    {
        var entries = source.ToList();
        foreach (var entry in entries)
        {
            entry.Id = Guid.NewGuid();
            entry.Site = site;
            entry.IsPublished = true;
            entry.Version = 1;
            entry.CreatedAt = now;
            entry.UpdatedAt = now;
        }
        db.Entries.AddRange(entries);
        return entries.Count;
    }

    /// <summary>
    /// eduivme.com as it went live on 19.11.2026 had a Kurs Yönetimi product page. The card now leads to maxkampus.com and
    /// the old page is taken down (kept unpublished, so nothing is lost). Runs only while the card still has its old address,
    /// so an admin's later edit is never overwritten. A revision is kept for every change.
    /// </summary>
    public static async Task<int> MoveCourseManagementAsync(SiteDbContext db, DateTime now, CancellationToken cancellationToken)
    {
        var card = await db.Entries.FirstOrDefaultAsync(item => item.Site == SiteRules.Eduivme && item.Kind == SiteKinds.Product
            && item.Group == string.Empty && item.Slug == CoursePage, cancellationToken);
        if (card is null || card.LinkUrl != "/urunler/" + CoursePage) return 0;
        var changed = new List<SiteEntry> { card };
        db.Revisions.Add(SiteContent.RevisionOf(card, now));
        var fresh = CourseCard();
        card.Title = fresh.Title;
        card.Subtitle = fresh.Subtitle;
        card.Body = fresh.Body;
        card.Highlights = fresh.Highlights;
        card.LinkLabel = fresh.LinkLabel;
        card.LinkUrl = fresh.LinkUrl;
        var page = await db.Entries.Where(item => item.Site == SiteRules.Eduivme && item.IsPublished
            && ((item.Kind == SiteKinds.Page && item.Group == string.Empty && item.Slug == CoursePage) || item.Group == CoursePage))
            .ToListAsync(cancellationToken);
        foreach (var entry in page)
        {
            db.Revisions.Add(SiteContent.RevisionOf(entry, now));
            entry.IsPublished = false;
            changed.Add(entry);
        }
        // The home page's own words that name the old product (site description, two questions).
        var named = await db.Entries.Where(item => item.Site == SiteRules.Eduivme && item.Group == string.Empty
            && (item.Kind == SiteKinds.Faq || item.Kind == SiteKinds.Site)
            && (item.Body.Contains("Kurs Yönetimi") || (item.SeoDescription != null && item.SeoDescription.Contains("Kurs Yönetimi"))))
            .ToListAsync(cancellationToken);
        foreach (var entry in named)
        {
            db.Revisions.Add(SiteContent.RevisionOf(entry, now));
            entry.Body = entry.Body.Replace("Kurs Yönetimi", "maxkampüs");
            entry.SeoDescription = entry.SeoDescription?.Replace("Kurs Yönetimi", "maxkampüs");
            changed.Add(entry);
        }
        foreach (var entry in changed)
        {
            entry.Version++;
            entry.UpdatedAt = now;
        }
        return changed.Count;
    }

    private static SiteEntry CourseCard() => new()
    {
        Kind = SiteKinds.Product, Slug = CoursePage, Brand = "km", Title = "maxkampüs", Subtitle = "Kurs ve özel ders yönetimi", SortOrder = 30,
        Body = "Kurslar, dershaneler ve özel ders veren öğretmenler için ders programı, yoklama, ödevler ve öğrenci ödemeleri tek yerde.",
        Highlights = ["Haftalık ders programı ve yoklama", "Ödev verme ve değerlendirme", "Taksit ve paket takibi"],
        LinkLabel = "maxkampus.com", LinkUrl = MaxkampusUrl
    };

    private static IEnumerable<SiteEntry> Entries()
    {
        yield return new SiteEntry
        {
            Kind = SiteKinds.Site, Slug = "site", Title = "eduivme",
            Body = "Öğrenciler, veliler, öğretmenler ve kurumlar için eğitim ürünleri.",
            SeoTitle = "eduivme: öğrenmenin her adımı için eğitim ürünleri",
            SeoDescription = "maxkoç öğrenci koçluğu, Master Hızlı Okuma ve maxkampüs kurs yönetimi: öğrenciler, veliler, öğretmenler ve kurumlar için eduivme ürünleri."
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
        yield return CourseCard();

        yield return Audience(string.Empty, "Öğrenci", "Planını, ödevlerini ve egzersizlerini tek yerden takip et.", "Öğrenci girişi", "https://maxkoc.com/auth/login", "school", 10);
        yield return Audience(string.Empty, "Veli", "Çocuğunun çalışmasını, onun izin verdiği kadarıyla izle.", "Veli girişi", "https://veli.maxkoc.com/auth/login", "users", 20);
        yield return Audience(string.Empty, "Öğretmen", "Öğrencilerinizi davet edin, ödev verin, ilerlemeyi görün.", "Öğretmen girişi", "https://ogretmen.eduivme.com/auth/login", "person", 30);
        yield return Audience(string.Empty, "Kurum", "Öğretmenlerinizi ve öğrencilerinizi tek panelden yönetin.", "Kurum girişi", "https://kurum.eduivme.com/auth/login", "building", 40);

        yield return Faq(string.Empty, "eduivme nedir?",
            "eduivme; maxkoç, Master Hızlı Okuma ve maxkampüs ürünlerinin çatı markasıdır. Her ürünün kendi sitesi ve kendi girişi vardır.", 10);
        yield return Faq(string.Empty, "Hangi ürün bana uygun?",
            "Düzenli bir çalışma planı ve koç desteği istiyorsanız maxkoç, okuma hızınızı ve anlamanızı geliştirmek istiyorsanız Master Hızlı Okuma, kurs ya da özel ders veriyorsanız maxkampüs size göre. Ürünleri birlikte de kullanabilirsiniz.", 20);
        yield return Faq(string.Empty, "Öğretmen ya da kurum olarak nasıl başlarım?",
            "Öğretmen girişinden ya da kurum girişinden hesap açın. Hesabınız açıldıktan sonra öğrencilerinizi davet eder, ödev ve programlarını oradan yönetirsiniz.", 30);
        yield return Faq(string.Empty, "Kişisel verilerim nasıl korunuyor?",
            "Kişisel verilerin hangi amaçla ve nasıl işlendiği KVKK aydınlatma metninde ve gizlilik politikasında anlatılır. maxkoç'ta veli, öğrencinin yalnız izin verdiği bilgileri görür.", 40);
    }

    private static SiteEntry Section(string group, string slug, string title, string lead, int order) =>
        new() { Kind = SiteKinds.Section, Group = group, Slug = slug, Title = title, Body = lead, SortOrder = order };

    private static SiteEntry Audience(string group, string title, string text, string? label, string? url, string icon, int order) =>
        new() { Kind = SiteKinds.Audience, Group = group, Title = title, Body = text, LinkLabel = label, LinkUrl = url, Icon = icon, SortOrder = order };

    private static SiteEntry Faq(string group, string question, string answer, int order) =>
        new() { Kind = SiteKinds.Faq, Group = group, Title = question, Body = answer, SortOrder = order };

}
