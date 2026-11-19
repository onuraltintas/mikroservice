using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Site.API;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared.IntegrationTests.Fixtures;
using Site.Infrastructure;

namespace Site.IntegrationTests;

/// <summary>eduivme.com and maxkampus.com content through the real host: what visitors see and what only the system admin may change.</summary>
public sealed class SiteApiTests : IAsyncLifetime
{
    private const string Secret = "site-tests-secret-0123456789-abcdefghijklmnopqrstuvwxyz";
    private const string Issuer = "eduplatform.identity";
    private const string Audience = "eduplatform.api";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly PostgresFixture _postgres = new();
    private WebApplicationFactory<Program> _factory = null!;
    private readonly FakeIntegrations _integrations = new();

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        await using (var db = Db())
        {
            await db.Database.MigrateAsync();
            await SiteSeed.EnsureAsync(db, TimeProvider.System);
        }
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("SITE_CONNECTION_STRING", _postgres.ConnectionString);
            builder.UseSetting("JWT_SECRET", Secret);
            builder.UseSetting("JWT_ISSUER", Issuer);
            builder.UseSetting("JWT_AUDIENCE", Audience);
            builder.UseSetting("JWT_KEY_ID", "tests");
            builder.UseSetting("AllowedHosts", "*");
            builder.ConfigureTestServices(services => services.AddSingleton<SiteIntegrations>(_integrations));
        });
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task The_seed_is_written_once_and_the_home_page_shows_it()
    {
        await using (var db = Db()) Assert.Equal(0, await SiteSeed.EnsureAsync(db, TimeProvider.System));

        var home = await Client(null).GetFromJsonAsync<SiteHomeView>("/api/site/home", Json);
        Assert.NotNull(home);
        Assert.Equal("Öğrenmenin her adımı için araçlar.", home!.Hero!.Title);
        Assert.Equal(["maxkoç", "Master Hızlı Okuma", "maxkampüs"], home.Products.Select(item => item.Title).ToArray());
        Assert.Equal("https://maxkampus.com", home.Products[2].LinkUrl);
        Assert.DoesNotContain(home.Faqs, faq => faq.Body.Contains("Kurs Yönetimi"));
        Assert.Equal(4, home.Audiences.Count);
        Assert.Equal(4, home.Faqs.Count);
        Assert.All(home.Faqs, faq => Assert.False(string.IsNullOrWhiteSpace(faq.Body)));
        Assert.Equal(["products", "audiences", "faq"], home.Sections.Select(item => item.Slug!).ToArray());
    }

    [Fact]
    public async Task The_live_course_page_moves_to_maxkampus_once_and_keeps_revisions()
    {
        // eduivme.com as it went live: the card led to its own product page.
        await using (var db = Db())
        {
            var card = await db.Entries.SingleAsync(item => item.Site == SiteRules.Eduivme && item.Kind == SiteKinds.Product && item.Slug == SiteSeed.CoursePage);
            card.Title = "Kurs Yönetimi";
            card.LinkUrl = "/urunler/" + SiteSeed.CoursePage;
            var faq = await db.Entries.FirstAsync(item => item.Site == SiteRules.Eduivme && item.Kind == SiteKinds.Faq);
            faq.Body = "eduivme; maxkoç, Master Hızlı Okuma ve Kurs Yönetimi ürünlerinin çatı markasıdır.";
            var now = DateTime.UtcNow;
            db.Entries.Add(new SiteEntry { Id = Guid.NewGuid(), Kind = SiteKinds.Page, Slug = SiteSeed.CoursePage, Title = "Kurs Yönetimi", Body = "Eski sayfa", IsPublished = true, Version = 1, CreatedAt = now, UpdatedAt = now });
            db.Entries.Add(new SiteEntry { Id = Guid.NewGuid(), Kind = SiteKinds.Feature, Group = SiteSeed.CoursePage, Title = "Ders programı", Body = "Eski", Icon = "calendar", IsPublished = true, Version = 1, CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();
            Assert.Equal(4, await SiteSeed.EnsureAsync(db, TimeProvider.System));
            Assert.Equal(0, await SiteSeed.EnsureAsync(db, TimeProvider.System));
            Assert.Equal(4, await db.Revisions.CountAsync());
        }

        var client = Client(null);
        var home = await client.GetFromJsonAsync<SiteHomeView>("/api/site/home", Json);
        var moved = home!.Products.Single(item => item.Slug == SiteSeed.CoursePage);
        Assert.Equal(("maxkampüs", "https://maxkampus.com"), (moved.Title, moved.LinkUrl));
        Assert.DoesNotContain(home.Faqs, faq => faq.Body.Contains("Kurs Yönetimi"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/site/pages/kurs-yonetimi")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/site/pages/..%2Fsite")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<SiteSitemapEntry>>("/api/site/sitemap", Json))!);
    }

    [Fact]
    public async Task Maxkampus_has_its_own_home_and_eduivme_entries_never_show_there()
    {
        var client = Client(null);
        var home = await client.GetFromJsonAsync<MaxkampusHomeView>("/api/site/sites/maxkampus/home", Json);
        Assert.Equal("Kursunuzun bütün haftası tek ekranda", home!.Hero!.Title);
        Assert.Equal("bir eduivme markası", home.Site!.Subtitle);
        Assert.Equal(["features", "steps", "reviews", "pricing", "faq", "blog", "cta"], home.Sections.Select(item => item.Slug!).ToArray());
        Assert.Equal(6, home.Features.Count);
        Assert.Equal(3, home.Steps.Count);
        Assert.Equal(5, home.Faqs.Count);
        Assert.Empty(home.Reviews);
        Assert.Equal(4, home.Menus.Count(item => item.Category == "header"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/site/sites/eduivme/home")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/site/sites/baska/home")).StatusCode);
        Assert.Equal("İletişim", (await client.GetFromJsonAsync<SitePublicEntry>("/api/site/sites/maxkampus/pages/iletisim", Json))!.Title);

        var eduivme = await Client("SystemAdmin").GetFromJsonAsync<List<SiteEntryView>>("/api/site-admin/entries", Json);
        Assert.All(eduivme!, item => Assert.Equal(SiteRules.Eduivme, item.Site));
        var maxkampus = await Client("SystemAdmin").GetFromJsonAsync<List<SiteEntryView>>("/api/site-admin/entries?site=maxkampus", Json);
        Assert.All(maxkampus!, item => Assert.Equal(SiteRules.Maxkampus, item.Site));
        Assert.Equal(HttpStatusCode.BadRequest, (await Client("SystemAdmin").GetAsync("/api/site-admin/entries?site=baska")).StatusCode);
    }

    [Fact]
    public async Task Blog_posts_are_listed_by_date_and_topic_and_drafts_stay_hidden()
    {
        var admin = Client("SystemAdmin");
        await Create(admin, Post("veliyle-iletisim", "Veliyle iletişim", "Veli", DateTime.UtcNow.AddDays(-2)));
        await Create(admin, Post("ders-programi", "Ders programı kurmak", "Planlama", DateTime.UtcNow.AddDays(-1)));
        await Create(admin, Post("taslak-yazi", "Taslak", "Veli", null) with { IsPublished = false });
        await Create(admin, Post("yarin", "Yarınki yazı", "Veli", DateTime.UtcNow.AddDays(1)));

        var client = Client(null);
        var all = await client.GetFromJsonAsync<SitePostPage>("/api/site/sites/maxkampus/posts", Json);
        Assert.Equal(["Ders programı kurmak", "Veliyle iletişim"], all!.Items.Select(item => item.Title).ToArray());
        Assert.Equal(["Planlama", "Veli"], all.Categories.ToArray());
        Assert.All(all.Items, item => Assert.Equal(string.Empty, item.Body));
        var veli = await client.GetFromJsonAsync<SitePostPage>("/api/site/sites/maxkampus/posts?category=Veli", Json);
        Assert.Equal(1, veli!.Total);

        var post = await client.GetFromJsonAsync<SitePublicEntry>("/api/site/sites/maxkampus/posts/veliyle-iletisim", Json);
        Assert.Equal(("Yazar Adı", "Uzun metin."), (post!.Author, post.Body));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/site/sites/maxkampus/posts/taslak-yazi")).StatusCode);

        var sitemap = await client.GetFromJsonAsync<List<SiteSitemapItem>>("/api/site/sites/maxkampus/sitemap", Json);
        Assert.Contains(sitemap!, item => item.Path == "/blog/veliyle-iletisim");
        Assert.Contains(sitemap!, item => item.Path == "/sayfa/iletisim");
        Assert.DoesNotContain(sitemap!, item => item.Path == "/blog/taslak-yazi");

        var xml = await client.GetStringAsync("/api/site/sites/maxkampus/sitemap.xml");
        Assert.Contains("<loc>https://maxkampus.com/blog/veliyle-iletisim</loc>", xml);
        Assert.Contains("<loc>https://maxkampus.com/fiyatlar</loc>", xml);
        Assert.DoesNotContain("/sayfa/iletisim", xml);
        Assert.DoesNotContain("taslak-yazi", xml);

        var duplicate = await admin.PostAsJsonAsync("/api/site-admin/entries", Post("veliyle-iletisim", "Aynı adres", "Veli", null));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task Panel_notices_reach_only_their_audience_until_they_end()
    {
        var admin = Client("SystemAdmin");
        await Create(admin, Notice("Herkese", "all", null));
        await Create(admin, Notice("Sahiplere", "owners", null));
        await Create(admin, Notice("Öğretmenlere", "teachers", null));
        await Create(admin, Notice("Bitti", "all", DateTime.UtcNow.AddMinutes(-1)) with { PublishAt = DateTime.UtcNow.AddDays(-1) });

        var client = Client(null);
        var owners = await client.GetFromJsonAsync<List<SitePublicEntry>>("/api/site/sites/maxkampus/notices?audience=owners", Json);
        Assert.Equal(["Herkese", "Sahiplere"], owners!.Select(item => item.Title).Order().ToArray());
        var teachers = await client.GetFromJsonAsync<List<SitePublicEntry>>("/api/site/sites/maxkampus/notices?audience=teachers", Json);
        Assert.Equal(["Herkese", "Öğretmenlere"], teachers!.Select(item => item.Title).Order().ToArray());

        var noAudience = await admin.PostAsJsonAsync("/api/site-admin/entries", Notice("Kime?", null, null));
        Assert.Equal(HttpStatusCode.BadRequest, noAudience.StatusCode);
    }

    [Fact]
    public async Task Maxkampus_refuses_eduivme_kinds_groups_and_menus_without_a_place()
    {
        var admin = Client("SystemAdmin");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/site-admin/entries", Product("Ürün") with { Site = SiteRules.Maxkampus })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/site-admin/entries",
            Post("gruplu", "Gruplu", "Veli", null) with { Group = "kurs-yonetimi" })).StatusCode);
        var menu = new SiteEntryRequest(SiteKinds.Menu, string.Empty, null, "Menü", null, null, null, null, null, null, "/blog",
            null, null, null, null, null, null, null, 10, true, null, Site: SiteRules.Maxkampus);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/site-admin/entries", menu)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/site-admin/entries", menu with { Category = "footer" })).StatusCode);
        var plan = new SiteEntryRequest(SiteKinds.Plan, string.Empty, "plan-adi", "Plan", null, "Açıklama", null, null, null, null, null,
            null, null, null, null, null, null, null, 10, true, null, Site: SiteRules.Maxkampus);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/site-admin/entries", plan)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/site-admin/entries", plan with { Slug = Guid.NewGuid().ToString(), Category = "featured" })).StatusCode);
    }

    [Fact]
    public async Task A_contact_message_is_kept_for_the_admin_and_a_flood_is_refused()
    {
        var client = Client(null);
        var bad = await client.PostAsJsonAsync("/api/site/sites/maxkampus/contact", new ContactRequest("", null, "eposta-degil", null, "kısa", false, "t"));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var body = await bad.Content.ReadAsStringAsync();
        foreach (var field in new[] { "name", "email", "message", "consent" }) Assert.Contains(field, body);

        _integrations.CaptchaOk = false;
        var robot = await client.PostAsJsonAsync("/api/site/sites/maxkampus/contact", Contact("ilk"));
        Assert.Equal(HttpStatusCode.BadRequest, robot.StatusCode);
        Assert.Equal(SiteIntegrations.ContactAction, _integrations.LastAction);
        _integrations.CaptchaOk = true;

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/site/sites/maxkampus/contact", Contact($"mesaj {i}"))).StatusCode);
        Assert.Equal((HttpStatusCode)429, (await client.PostAsJsonAsync("/api/site/sites/maxkampus/contact", Contact("dördüncü"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/site/sites/eduivme/contact", Contact("başka site"))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await Client("Teacher").GetAsync("/api/site-admin/sites/maxkampus/messages")).StatusCode);
        var admin = Client("SystemAdmin");
        var page = await admin.GetFromJsonAsync<SitePage<ContactMessageView>>("/api/site-admin/sites/maxkampus/messages", Json);
        Assert.Equal(3, page!.Total);
        var first = page.Items[0];
        Assert.Equal("test-iletisim@example.com", first.Email);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/site-admin/sites/maxkampus/messages/{first.Id}/answered", new AnsweredRequest(true))).StatusCode);
        var open = await admin.GetFromJsonAsync<SitePage<ContactMessageView>>("/api/site-admin/sites/maxkampus/messages?status=open", Json);
        Assert.Equal(2, open!.Total);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/site-admin/sites/maxkampus/messages/{first.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/site-admin/sites/maxkampus/messages/{first.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_newsletter_address_counts_only_after_confirmation_and_can_leave()
    {
        var client = Client(null);
        const string email = "Test-Bulten@Example.com";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/site/sites/maxkampus/newsletter", new SubscribeRequest(email, false, "t", "footer"))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/site/sites/maxkampus/newsletter", new SubscribeRequest(email, true, "t", "footer"))).StatusCode);
        Assert.Equal(SiteIntegrations.NewsletterAction, _integrations.LastAction);
        // A second click within minutes sends no second e-mail.
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/site/sites/maxkampus/newsletter", new SubscribeRequest(email, true, "t", "footer"))).StatusCode);
        var sent = Assert.Single(_integrations.Emails);
        Assert.Equal("test-bulten@example.com", sent.Recipient);
        var token = System.Text.RegularExpressions.Regex.Match(sent.Html, "/bulten/onay\\?t=([0-9a-f]{64})").Groups[1].Value;
        Assert.Equal(64, token.Length);

        var admin = Client("SystemAdmin");
        Assert.DoesNotContain("test-bulten", await admin.GetStringAsync("/api/site-admin/sites/maxkampus/subscribers.csv"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/site/sites/maxkampus/newsletter/confirm", new NewsletterTokenRequest(new string('0', 64)))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/site/sites/maxkampus/newsletter/confirm", new NewsletterTokenRequest(token))).StatusCode);
        var csv = await admin.GetStringAsync("/api/site-admin/sites/maxkampus/subscribers.csv");
        Assert.Contains("test-bulten@example.com", csv);
        Assert.Contains("/bulten/ayril?t=" + token, csv);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/site/sites/maxkampus/newsletter/unsubscribe", new NewsletterTokenRequest(token))).StatusCode);
        Assert.DoesNotContain("test-bulten", await admin.GetStringAsync("/api/site-admin/sites/maxkampus/subscribers.csv"));
        // Leaving cannot be undone by the old confirmation link.
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/site/sites/maxkampus/newsletter/confirm", new NewsletterTokenRequest(token))).StatusCode);
        var list = await admin.GetFromJsonAsync<SitePage<SubscriberView>>("/api/site-admin/sites/maxkampus/subscribers?status=unsubscribed", Json);
        Assert.Equal("Unsubscribed", Assert.Single(list!.Items).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/site-admin/sites/maxkampus/subscribers/{list.Items[0].Id}")).StatusCode);
    }

    [Fact]
    public async Task Retention_deletes_old_messages_and_unopened_confirmations_only()
    {
        var now = DateTime.UtcNow;
        await using var db = Db();
        SiteContactMessage Message(DateTime created, DateTime? answered) => new()
        {
            Id = Guid.NewGuid(), Site = SiteRules.Maxkampus, Name = "TEST", Email = "test@example.com", Message = "TEST mesajı",
            ConsentAt = created, CreatedAt = created, AnsweredAt = answered
        };
        SiteSubscriber Subscriber(string email, SiteSubscriberStatus status, DateTime created) => new()
        {
            Id = Guid.NewGuid(), Site = SiteRules.Maxkampus, Email = email, Token = Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()) + Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()),
            Status = status, CreatedAt = created
        };
        db.ContactMessages.AddRange(
            Message(now.AddDays(-200), now.AddDays(-190)),   // answered long ago: goes
            Message(now.AddDays(-200), null),                 // still open: stays
            Message(now.AddDays(-400), null),                 // over a year: goes
            Message(now.AddDays(-10), now.AddDays(-9)));      // answered recently: stays
        db.Subscribers.AddRange(
            Subscriber("test-a@example.com", SiteSubscriberStatus.Pending, now.AddDays(-8)),     // goes
            Subscriber("test-b@example.com", SiteSubscriberStatus.Pending, now.AddDays(-1)),     // stays
            Subscriber("test-c@example.com", SiteSubscriberStatus.Confirmed, now.AddDays(-900))); // stays
        await db.SaveChangesAsync();

        Assert.Equal(3, await SiteRetention.RunAsync(db, now, CancellationToken.None));
        Assert.Equal(2, await db.ContactMessages.CountAsync());
        Assert.Equal(["test-b@example.com", "test-c@example.com"], await db.Subscribers.OrderBy(item => item.Email).Select(item => item.Email).ToArrayAsync());
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Teacher", HttpStatusCode.Forbidden)]
    [InlineData("SystemAdmin", HttpStatusCode.OK)]
    public async Task Only_the_system_admin_reads_the_admin_list(string? role, HttpStatusCode expected) =>
        Assert.Equal(expected, (await Client(role).GetAsync("/api/site-admin/entries")).StatusCode);

    [Fact]
    public async Task Unpublished_and_scheduled_entries_stay_hidden_until_their_time()
    {
        var admin = Client("SystemAdmin");
        var draft = await Create(admin, Product("Taslak ürün", isPublished: false));
        var later = await Create(admin, Product("Yarınki ürün", isPublished: true, publishAt: DateTime.UtcNow.AddDays(1)));
        var now = await Create(admin, Product("Bugünkü ürün", isPublished: true));

        var titles = (await Client(null).GetFromJsonAsync<SiteHomeView>("/api/site/home", Json))!.Products.Select(item => item.Title).ToList();
        Assert.DoesNotContain(draft.Title, titles);
        Assert.DoesNotContain(later.Title, titles);
        Assert.Contains(now.Title, titles);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("//kotu.example.invalid")]
    [InlineData("http://eduivme.com")]
    public async Task Unsafe_links_are_refused(string url)
    {
        var response = await Client("SystemAdmin").PostAsJsonAsync("/api/site-admin/entries", Product("Kötü bağlantı") with { LinkUrl = url });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("linkUrl", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_page_entry_needs_an_existing_page_and_a_section_must_be_known()
    {
        var admin = Client("SystemAdmin");
        var orphan = await admin.PostAsJsonAsync("/api/site-admin/entries", new SiteEntryRequest(
            SiteKinds.Feature, "olmayan-sayfa", null, "Özellik", null, "Metin", null, null, "calendar", null, null, null, null, null, null, null, null, null, 10, true, null));
        Assert.Equal(HttpStatusCode.BadRequest, orphan.StatusCode);

        var section = await admin.PostAsJsonAsync("/api/site-admin/entries", new SiteEntryRequest(
            SiteKinds.Section, string.Empty, "bilinmeyen", "Bölüm", null, null, null, null, null, null, null, null, null, null, null, null, null, null, 10, true, null));
        Assert.Equal(HttpStatusCode.BadRequest, section.StatusCode);

        var hero = await admin.PostAsJsonAsync("/api/site-admin/entries", new SiteEntryRequest(
            SiteKinds.Hero, string.Empty, null, "İkinci giriş", null, null, null, null, null, null, null, null, null, null, null, null, null, null, 10, true, null));
        Assert.Equal(HttpStatusCode.BadRequest, hero.StatusCode);
    }

    [Fact]
    public async Task An_edit_keeps_a_revision_a_stale_edit_is_refused_and_an_old_version_comes_back()
    {
        var admin = Client("SystemAdmin");
        var entries = await admin.GetFromJsonAsync<List<SiteEntryView>>("/api/site-admin/entries?group=", Json);
        var hero = entries!.Single(item => item.Kind == SiteKinds.Hero);

        var changed = await Update(admin, hero, hero.Version, "Yeni başlık");
        Assert.Equal(hero.Version + 1, changed.Version);

        var stale = await admin.PutAsJsonAsync($"/api/site-admin/entries/{hero.Id}", Request(hero, hero.Version, "Eski sürümden"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var revisions = await admin.GetFromJsonAsync<List<SiteRevisionView>>($"/api/site-admin/entries/{hero.Id}/revisions", Json);
        var first = Assert.Single(revisions!);
        Assert.Equal(hero.Title, first.Title);
        var restored = await (await admin.PostAsync($"/api/site-admin/entries/{hero.Id}/revisions/{first.Id}/restore", null)).Content.ReadFromJsonAsync<SiteEntryView>(Json);
        Assert.Equal(hero.Title, restored!.Title);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/site-admin/entries/{hero.Id}")).StatusCode);
    }

    [Fact]
    public async Task Products_can_be_reordered_and_a_page_is_deleted_with_its_own_entries()
    {
        var admin = Client("SystemAdmin");
        var entries = await admin.GetFromJsonAsync<List<SiteEntryView>>("/api/site-admin/entries", Json);
        var products = entries!.Where(item => item.Kind == SiteKinds.Product).OrderBy(item => item.SortOrder).ToList();
        var reversed = products.Select(item => item.Id).Reverse().ToList();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/site-admin/entries/order", new SiteOrderRequest(reversed))).StatusCode);
        var home = await Client(null).GetFromJsonAsync<SiteHomeView>("/api/site/home", Json);
        Assert.Equal(reversed, home!.Products.Select(item => item.Id).ToList());

        var mixed = new SiteOrderRequest([products[0].Id, entries!.First(item => item.Kind == SiteKinds.Faq).Id]);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/site-admin/entries/order", mixed)).StatusCode);

        var page = await Create(admin, new SiteEntryRequest(SiteKinds.Page, string.Empty, "ornek-urun", "Örnek ürün", null, "Açıklama", null, "eduivme",
            null, null, null, null, null, null, null, null, null, null, 40, true, null));
        await Create(admin, new SiteEntryRequest(SiteKinds.Feature, "ornek-urun", null, "Özellik", null, "Metin", null, null, "calendar",
            null, null, null, null, null, null, null, null, null, 10, true, null));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/site-admin/entries/{page.Id}")).StatusCode);
        await using var db = Db();
        Assert.False(await db.Entries.AnyAsync(item => item.Group == "ornek-urun"));
    }

    [Fact]
    public async Task An_image_is_served_to_visitors_and_other_files_are_refused()
    {
        var admin = Client("SystemAdmin");
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];
        using var form = new MultipartFormDataContent { { new ByteArrayContent(png), "file", "kapak.png" }, { new StringContent("Kapak"), "altText" } };
        var uploaded = await (await admin.PostAsync("/api/site-admin/media", form)).Content.ReadFromJsonAsync<SiteMediaView>(Json);
        Assert.Equal("image/png", uploaded!.ContentType);

        var served = await Client(null).GetAsync(uploaded.Url);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/png", served.Content.Headers.ContentType!.MediaType);
        Assert.Equal(png, await served.Content.ReadAsByteArrayAsync());

        using var script = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes("<svg onload=alert(1)>")), "file", "kotu.svg" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/site-admin/media", script)).StatusCode);

        var withImage = await Create(admin, Product("Görselli ürün") with { ImageId = uploaded.Id });
        Assert.Equal(uploaded.Url, withImage.ImageUrl);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/site-admin/media/{uploaded.Id}")).StatusCode);
    }

    private static SiteEntryRequest Product(string title, bool isPublished = true, DateTime? publishAt = null) => new(
        SiteKinds.Product, string.Empty, null, title, "Tür", "Açıklama", ["Bir", "İki"], "eduivme", null, "Git", "https://example.com",
        null, null, null, null, null, null, null, 90, isPublished, publishAt);

    private static SiteEntryRequest Post(string slug, string title, string category, DateTime? publishAt) => new(
        SiteKinds.Post, string.Empty, slug, title, "Kısa özet", "Uzun metin.", null, null, null, null, null, null, null, null, null, null,
        null, null, 10, true, publishAt, Site: SiteRules.Maxkampus, Category: category, Author: "Yazar Adı");

    private static SiteEntryRequest Notice(string title, string? audience, DateTime? endsAt) => new(
        SiteKinds.Notice, string.Empty, null, title, null, "TEST duyuru metni", null, null, null, null, null, null, null, null, null, null,
        null, null, 10, true, null, Site: SiteRules.Maxkampus, Audience: audience, EndsAt: endsAt);

    private static ContactRequest Contact(string message) =>
        new("TEST Kişi", "TEST Kurum", "test-iletisim@example.com", null, $"TEST {message}: bir soru yazıyorum.", true, "t");

    private static SiteEntryRequest Request(SiteEntryView entry, int version, string title) => new(
        entry.Kind, entry.Group, entry.Slug, title, entry.Subtitle, entry.Body, entry.Highlights, entry.Brand, entry.Icon,
        entry.LinkLabel, entry.LinkUrl, entry.SecondaryLinkLabel, entry.SecondaryLinkUrl, entry.ImageId, entry.CtaTitle, entry.CtaBody,
        entry.SeoTitle, entry.SeoDescription, entry.SortOrder, entry.IsPublished, entry.PublishAt, version, entry.Site, entry.Category,
        entry.Audience, entry.EndsAt, entry.Author);

    private static async Task<SiteEntryView> Create(HttpClient admin, SiteEntryRequest request)
    {
        var response = await admin.PostAsJsonAsync("/api/site-admin/entries", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SiteEntryView>(Json))!;
    }

    private static async Task<SiteEntryView> Update(HttpClient admin, SiteEntryView entry, int version, string title)
    {
        var response = await admin.PutAsJsonAsync($"/api/site-admin/entries/{entry.Id}", Request(entry, version, title));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SiteEntryView>(Json))!;
    }

    private SiteDbContext Db() => new(new DbContextOptionsBuilder<SiteDbContext>().UseSiteDatabase(_postgres.ConnectionString).Options);

    private HttpClient Client(string? role)
    {
        var client = _factory.CreateClient();
        if (role is null) return client;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)) { KeyId = "tests" };
        var token = new JwtSecurityToken(Issuer, Audience,
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    /// <summary>Identity's robot check and the notification queue, stood in for.</summary>
    private sealed class FakeIntegrations() : SiteIntegrations(new HttpClient(), new ConfigurationBuilder().Build(), null!, NullLogger<SiteIntegrations>.Instance)
    {
        public bool CaptchaOk { get; set; } = true;
        public string? LastAction { get; private set; }
        public List<(string Recipient, string Html)> Emails { get; } = [];

        public override Task<bool> CaptchaAcceptedAsync(string? token, string action, CancellationToken cancellationToken)
        {
            LastAction = action;
            return Task.FromResult(CaptchaOk);
        }

        public override Task<bool> QueueEmailAsync(Guid messageId, string recipient, string subject, string html, CancellationToken cancellationToken)
        {
            lock (Emails) Emails.Add((recipient, html));
            return Task.FromResult(true);
        }
    }
}
