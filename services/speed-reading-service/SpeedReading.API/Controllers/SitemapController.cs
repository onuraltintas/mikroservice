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
public sealed class SitemapController(ISpeedReadingCms cms) : ControllerBase
{
    [HttpGet("sitemap.xml")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSitemap(CancellationToken cancellationToken = default)
    {
        var pages = await cms.GetPagesAsync(1, 100, cancellationToken);
        var posts = await cms.GetBlogPostsAsync(1, 100, cancellationToken);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        XNamespace sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var locations = new List<string> { baseUrl + "/" };
        locations.AddRange(pages.Items
            .Where(page => page.IsPublished && !page.SeoSettings.NoIndex)
            .Select(page => $"{baseUrl}/pages/{Uri.EscapeDataString(page.Slug)}"));
        locations.AddRange(posts.Items
            .Where(post => post.IsPublished && !post.SeoSettings.NoIndex)
            .Select(post => $"{baseUrl}/blog/{Uri.EscapeDataString(post.Slug)}"));

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(sitemap + "urlset", locations.Select(location =>
                new XElement(sitemap + "url", new XElement(sitemap + "loc", location)))));

        return Content(document.ToString(SaveOptions.DisableFormatting), "application/xml", Encoding.UTF8);
    }
}
