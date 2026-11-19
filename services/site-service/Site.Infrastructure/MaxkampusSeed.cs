namespace Site.Infrastructure;

/// <summary>
/// maxkampus.com's first content: the words of the approved design (19.11.2026). Customer reviews, blog posts, plan notes
/// and panel notices are not written here: they are real content the admin adds in "maxkampüs (Kurs Yönetimi) → Site içeriği".
/// </summary>
public static class MaxkampusSeed
{
    public static IEnumerable<SiteEntry> Entries()
    {
        yield return new SiteEntry
        {
            Kind = SiteKinds.Site, Slug = "site", Title = "maxkampüs", Subtitle = "bir eduivme markası",
            Body = "Kurslar, etüt merkezleri ve özel ders verenler için ders programı, yoklama, ödev ve tahsilat.",
            SeoTitle = "maxkampüs: kurs ve özel ders yönetimi",
            SeoDescription = "Kurslar, etüt merkezleri ve özel ders veren öğretmenler için ders programı, yoklama, ödev, tahsilat ve velilere e-posta tek yerde."
        };
        yield return new SiteEntry
        {
            Kind = SiteKinds.Hero, Slug = "hero", Title = "Kursunuzun bütün haftası tek ekranda", Subtitle = "Kurs ve özel ders yönetimi",
            Body = "Ders programı, yoklama, ödev ve tahsilat bir arada. Veliler devamsızlığı ve taksiti e-postayla öğrenir, siz telefonla tek tek aramazsınız.",
            LinkLabel = "Kurumunuzu açın", LinkUrl = "/kayit",
            SecondaryLinkLabel = "Fiyatları görün", SecondaryLinkUrl = "/fiyatlar"
        };

        yield return Section("features", "Kursun günlük işleri için gereken her şey", "Ayrı ayrı defterlere, tablolara ve mesaj gruplarına gerek kalmadan.", 10);
        yield return Section("steps", "Nasıl başlanır", "Kayıt birkaç dakika sürer.", 20);
        yield return Section("reviews", "Kurumlar ne diyor", string.Empty, 30);
        yield return Section("pricing", "Öğrenci sayınıza göre fiyat",
            "Ödeme havale ya da EFT ile; ödemeniz onaylanınca panel açılır. Deneme süresi yoktur.", 40);
        yield return Section("faq", "Sık sorulanlar", string.Empty, 50);
        yield return Section("blog", "Blog", "Kurs yönetimi, veli iletişimi ve ders planlama üzerine yazılar.", 60);
        yield return new SiteEntry
        {
            Kind = SiteKinds.Section, Slug = "cta", Title = "Kurumunuzu bugün açın", SortOrder = 70,
            Body = "Kayıt birkaç dakika sürer. Planınızı seçip ödemeniz onaylanınca panel açılır.",
            LinkLabel = "Kurumunuzu açın", LinkUrl = "/kayit"
        };

        yield return Feature("Takvim", "Boş saate tıklayıp ders ekleyin; bir yıla kadar tekrarlayan dersler, çakışmalar kaydetmeden önce görünür.", "calendar", 10);
        yield return Feature("Yoklama", "Öğretmen telefondan tek dokunuşla alır. Gelmeyen öğrencinin velisine e-posta gider.", "check", 20);
        yield return Feature("Ödev", "Ödevi derste verin, \"yaptı, eksik, yapmadı\" diye işaretleyin. Yapılmayan ödev veliye bildirilir.", "assignment", 30);
        yield return Feature("Tahsilat", "Taksitler, ödemeler ve geciken borçlar tek listede. Vadeden önce hatırlatma e-postası.", "payment", 40);
        yield return Feature("Velilere e-posta", "Kurumunuzun adıyla gider, yanıtlar sizin adresinize gelir. Kime ne gideceğini siz seçersiniz.", "mail", 50);
        yield return Feature("Öğretmenler", "Davetle katılırlar, yalnız kendi derslerini görürler. Çalışma saatleri ve izinler takvimde.", "users", 60);

        yield return Step("Kurumunuzu açın", "Adınız, telefonunuz ve kurumunuzun adı. Google hesabınızla da açabilirsiniz.", 10);
        yield return Step("Planınızı seçin", "Öğrenci sayınıza uyan planı seçip havaleyle ödersiniz; onaydan sonra panel açılır.", 20);
        yield return Step("Öğretmenlerinizi çağırın", "Öğretmenler e-postayla gelen davetle katılır, yalnız kendi derslerini görür.", 30);

        yield return Faq("Öğrenciler ve veliler bir uygulama kuracak mı?",
            "Hayır. Veliler devamsızlık, yapılmayan ödev, taksit ve ders değişikliklerini e-postayla öğrenir. Hangi e-postanın gideceğini siz seçersiniz.", 10);
        yield return Faq("Öğretmenler ne görür?",
            "Yalnız kendi dersleri, takvimi, yoklaması ve grubundaki öğrenciler. Tahsilatı ve iletişim bilgilerini görmez.", 20);
        yield return Faq("Verilerimiz kimde?",
            "Kayıtlar kurumunuzundur. eduivme destek ekibi yalnız sorun çözmek için bakar; her bakış ve değişiklik panelinizde listelenir.", 30);
        yield return Faq("Deneme süresi var mı?", "Hayır. Planınızı seçip ödemeniz onaylanınca panel açılır.", 40);
        yield return Faq("Özel ders veriyorum, kurumum yok. Kullanabilir miyim?",
            "Evet. Kayıtta \"Özel ders veriyorum\" seçeneğini seçin; tek kişilik bir çalışma alanı açılır.", 50);

        yield return Menu("header", "Özellikler", "/#ozellikler", 10);
        yield return Menu("header", "Fiyatlar", "/fiyatlar", 20);
        yield return Menu("header", "Blog", "/blog", 30);
        yield return Menu("header", "İletişim", "/iletisim", 40);
        yield return Menu("footer", "Fiyatlar", "/fiyatlar", 10);
        yield return Menu("footer", "Blog", "/blog", 20);
        yield return Menu("footer", "İletişim", "/iletisim", 30);
        yield return Menu("footer", "eduivme.com", "https://eduivme.com", 40);

        yield return new SiteEntry
        {
            Kind = SiteKinds.Page, Slug = "iletisim", Title = "İletişim", SortOrder = 10,
            Subtitle = "Kurumunuza uygun planı birlikte bulalım ya da sorunuzu yanıtlayalım.",
            Body = "Mesajınıza bir iş günü içinde dönüyoruz. Hesabınız varsa sorunuzu panelden de gönderebilirsiniz.",
            SeoTitle = "İletişim | maxkampüs",
            SeoDescription = "maxkampüs ekibine yazın: plan seçimi, kurulum ve sorularınız için."
        };
        yield return new SiteEntry
        {
            Kind = SiteKinds.Page, Slug = "fiyatlar", Title = "Fiyatlar", SortOrder = 20,
            Subtitle = "Öğrenci sayınıza göre fiyat",
            Body = "Ödeme havale ya da EFT ile; ödemeniz onaylanınca panel açılır. Daha fazla öğrenciniz varsa bize yazın, kurumunuza uygun planı birlikte belirleyelim.",
            SeoTitle = "Fiyatlar | maxkampüs",
            SeoDescription = "maxkampüs planları ve fiyatları: öğrenci sayınıza uyan planı seçin, havale ya da EFT ile ödeyin."
        };
    }

    private static SiteEntry Section(string slug, string title, string lead, int order) =>
        new() { Kind = SiteKinds.Section, Slug = slug, Title = title, Body = lead, SortOrder = order };

    private static SiteEntry Feature(string title, string text, string icon, int order) =>
        new() { Kind = SiteKinds.Feature, Title = title, Body = text, Icon = icon, SortOrder = order };

    private static SiteEntry Step(string title, string text, int order) =>
        new() { Kind = SiteKinds.Step, Title = title, Body = text, SortOrder = order };

    private static SiteEntry Faq(string question, string answer, int order) =>
        new() { Kind = SiteKinds.Faq, Title = question, Body = answer, SortOrder = order };

    private static SiteEntry Menu(string place, string label, string url, int order) =>
        new() { Kind = SiteKinds.Menu, Category = place, Title = label, LinkUrl = url, SortOrder = order };
}
