using Asp.Versioning;
using Coaching.Application.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching/cms")]
[AllowAnonymous]
public sealed class CoachingCmsController(ICoachingCms cms) : ControllerBase
{
    [HttpGet("blocks")]
    public async Task<IActionResult> GetBlocks([FromQuery] string group = "HomePage", CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await cms.GetPublishedBlocksAsync(group, cancellationToken) });

    [HttpGet("pages/{slug}")]
    public async Task<IActionResult> GetPage(string slug, CancellationToken cancellationToken = default)
    {
        var page = await cms.GetPublishedPageAsync(slug, cancellationToken);
        return page is null
            ? NotFound(new { success = false, message = "Koçluk içerik sayfası bulunamadı." })
            : Ok(new { success = true, data = page });
    }

    [HttpGet("blog")]
    public async Task<IActionResult> GetBlog(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await cms.GetPublishedBlogAsync(pageNumber, pageSize, cancellationToken) });

    [HttpGet("blog/{slug}")]
    public async Task<IActionResult> GetBlogPost(string slug, CancellationToken cancellationToken = default)
    {
        var post = await cms.GetPublishedBlogPostAsync(slug, cancellationToken);
        return post is null
            ? NotFound(new { success = false, message = "Koçluk yazısı bulunamadı." })
            : Ok(new { success = true, data = post });
    }

    [HttpGet("navigation")]
    public async Task<IActionResult> GetNavigation([FromQuery] string menu = "Main", CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await cms.GetNavigationAsync(menu, includeHidden: false, cancellationToken) });

    [HttpGet("media/{id:guid}")]
    public async Task<IActionResult> GetMedia(Guid id, CancellationToken cancellationToken = default)
    {
        var media = await cms.GetMediaDownloadAsync(id, cancellationToken);
        return media is null
            ? NotFound(new { success = false, message = "Koçluk CMS görseli bulunamadı." })
            : File(media.Content, media.ContentType, enableRangeProcessing: true);
    }
}
