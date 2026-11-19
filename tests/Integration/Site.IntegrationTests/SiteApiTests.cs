using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared.IntegrationTests.Fixtures;
using Site.Infrastructure;

namespace Site.IntegrationTests;

/// <summary>eduivme.com content through the real host: what visitors see and what only the system admin may change.</summary>
public sealed class SiteApiTests : IAsyncLifetime
{
    private const string Secret = "site-tests-secret-0123456789-abcdefghijklmnopqrstuvwxyz";
    private const string Issuer = "eduplatform.identity";
    private const string Audience = "eduplatform.api";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly PostgresFixture _postgres = new();
    private WebApplicationFactory<Program> _factory = null!;

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
        Assert.Equal(["maxkoç", "Master Hızlı Okuma", "Kurs Yönetimi"], home.Products.Select(item => item.Title).ToArray());
        Assert.Equal("/urunler/kurs-yonetimi", home.Products[2].LinkUrl);
        Assert.Equal(4, home.Audiences.Count);
        Assert.Equal(4, home.Faqs.Count);
        Assert.All(home.Faqs, faq => Assert.False(string.IsNullOrWhiteSpace(faq.Body)));
        Assert.Equal(["products", "audiences", "faq"], home.Sections.Select(item => item.Slug!).ToArray());
    }

    [Fact]
    public async Task The_course_page_has_its_own_sections_and_unknown_pages_are_not_found()
    {
        var client = Client(null);
        var page = await client.GetFromJsonAsync<SitePageView>("/api/site/pages/kurs-yonetimi", Json);
        Assert.Equal("Kursunuzu tek ekrandan yönetin.", page!.Page.Subtitle);
        Assert.Equal("Hemen başla", page.Page.LinkLabel);
        Assert.Equal(6, page.Features.Count);
        Assert.Equal(2, page.Audiences.Count);
        Assert.Equal(3, page.Steps.Count);
        Assert.Equal(3, page.Faqs.Count);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/site/pages/yok")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/site/pages/..%2Fsite")).StatusCode);

        var sitemap = await client.GetFromJsonAsync<List<SiteSitemapEntry>>("/api/site/sitemap", Json);
        Assert.Equal(["kurs-yonetimi"], sitemap!.Select(item => item.Slug).ToArray());
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

        var page = entries!.Single(item => item.Kind == SiteKinds.Page);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/site-admin/entries/{page.Id}")).StatusCode);
        await using var db = Db();
        Assert.False(await db.Entries.AnyAsync(item => item.Group == "kurs-yonetimi"));
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

    private static SiteEntryRequest Request(SiteEntryView entry, int version, string title) => new(
        entry.Kind, entry.Group, entry.Slug, title, entry.Subtitle, entry.Body, entry.Highlights, entry.Brand, entry.Icon,
        entry.LinkLabel, entry.LinkUrl, entry.SecondaryLinkLabel, entry.SecondaryLinkUrl, entry.ImageId, entry.CtaTitle, entry.CtaBody,
        entry.SeoTitle, entry.SeoDescription, entry.SortOrder, entry.IsPublished, entry.PublishAt, version);

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
}
