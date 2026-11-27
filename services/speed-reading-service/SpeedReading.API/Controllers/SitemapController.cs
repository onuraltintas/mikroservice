using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;
using System.Text;
using System.Xml.Linq;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading")]
public sealed class SitemapController(ISpeedReadingCms cms, IConfiguration configuration) : ControllerBase
{
    [HttpGet("sitemap.xml")]
    [HttpHead("sitemap.xml")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSitemap(CancellationToken cancellationToken = default)
    {
        var pages = await cms.GetPagesAsync(1, 100, cancellationToken);
        var posts = await cms.GetBlogPostsAsync(1, 100, cancellationToken);
        var baseUrl = ResolvePublicBaseUrl(configuration).TrimEnd('/');
        XNamespace sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var locations = BuildLocations(
            baseUrl,
            pages.Items.Where(page => page.IsPublished && !page.SeoSettings.NoIndex).Select(page => page.Slug),
            posts.Items.Where(post => post.IsPublished && !post.SeoSettings.NoIndex).Select(post => post.Slug));

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(sitemap + "urlset", locations.Select(location =>
                new XElement(sitemap + "url", new XElement(sitemap + "loc", location)))));

        return Content(document.ToString(SaveOptions.DisableFormatting), "application/xml", Encoding.UTF8);
    }

    // Public pages of the standalone site that are not CMS content (27.11.2026).
    // Keep in step with clients/speed-reading public and legal routes.
    public static readonly string[] StaticPaths =
    [
        "/",
        "/hakkimizda",
        "/iletisim",
        "/fiyatlandirma",
        "/blog",
        "/sss",
        "/legal/privacy",
        "/legal/kvkk",
        "/legal/cookies",
        "/legal/terms",
        "/legal/speed-reading-newsletter-consent",
    ];

    // First path segments that belong to app routes; a CMS page with such a slug is
    // not reachable at /{slug} and is left out.
    private static readonly HashSet<string> ReservedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "auth", "student", "teacher", "editor", "coaching", "legal", "error", "no-access",
        "blog", "sss", "hakkimizda", "iletisim", "fiyatlandirma", "api", "pages",
    };

    // CMS pages are served at /{slug} by the client (public.routes ':slug'), blog posts at /blog/{slug}.
    public static IReadOnlyList<string> BuildLocations(
        string baseUrl,
        IEnumerable<string> pageSlugs,
        IEnumerable<string> postSlugs)
    {
        var root = baseUrl.TrimEnd('/');
        var paths = new List<string>(StaticPaths);
        paths.AddRange(pageSlugs
            .Select(slug => slug.Trim().Trim('/'))
            .Where(slug => slug.Length > 0 && !slug.Contains('/') && !ReservedSegments.Contains(slug))
            .Select(slug => "/" + Uri.EscapeDataString(slug)));
        paths.AddRange(postSlugs
            .Select(slug => slug.Trim().Trim('/'))
            .Where(slug => slug.Length > 0 && !slug.Contains('/'))
            .Select(slug => "/blog/" + Uri.EscapeDataString(slug)));
        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => root + path)
            .ToList();
    }

    private string ResolvePublicBaseUrl(IConfiguration configuration)
    {
        var configuredBaseUrl = configuration["PublicApp:BaseUrl"];
        if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var publicUri)
            && (publicUri.Scheme == Uri.UriSchemeHttp || publicUri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrWhiteSpace(publicUri.UserInfo)
            && string.IsNullOrWhiteSpace(publicUri.Query)
            && string.IsNullOrWhiteSpace(publicUri.Fragment))
        {
            return configuredBaseUrl!;
        }

        return $"{Request.Scheme}://{Request.Host}";
    }
}
