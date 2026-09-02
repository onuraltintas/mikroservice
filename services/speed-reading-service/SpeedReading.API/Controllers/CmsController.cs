using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/cms")]
public sealed class CmsController(ISpeedReadingCms cms) : ControllerBase
{
    [HttpGet("landing")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLanding(
        [FromQuery] string? group = null,
        [FromQuery] string? language = null,
        CancellationToken cancellationToken = default)
    {
        var blocks = await cms.GetLandingContentAsync(group ?? "HomePage", language, cancellationToken);
        return Ok(new { success = true, data = blocks, message = "Landing content retrieved" });
    }

    [HttpGet("pages/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPage(string slug, CancellationToken cancellationToken = default)
    {
        var page = await cms.GetPublishedPageAsync(slug.Trim(), cancellationToken);
        return page is null
            ? NotFound(new { success = false, message = "Page not found" })
            : Ok(new { success = true, data = page, message = "Page retrieved" });
    }

    [HttpGet("media/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMedia(Guid id, CancellationToken cancellationToken = default)
    {
        var media = await cms.GetMediaDownloadAsync(id, cancellationToken);
        return media is null
            ? NotFound(new { success = false, message = "Media not found" })
            : File(media.Content, media.ContentType, enableRangeProcessing: true);
    }

    [HttpGet("navigation")]
    [AllowAnonymous]
    public async Task<IActionResult> GetNavigation(
        [FromQuery] string menu = "Main",
        CancellationToken cancellationToken = default)
    {
        var items = await cms.GetNavigationAsync(menu, includeHidden: false, cancellationToken);
        return Ok(new { success = true, data = items, message = "Navigation retrieved" });
    }

    [HttpGet("blog")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBlogPosts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? page = null,
        [FromQuery] string? tag = null,
        CancellationToken cancellationToken = default)
    {
        var result = await cms.GetPublishedBlogPostsAsync(page ?? pageNumber, pageSize, tag, cancellationToken);
        return Ok(new
        {
            success = true,
            data = new
            {
                items = result.Items,
                totalCount = result.TotalCount,
                pageNumber = result.PageNumber,
                pageSize = result.PageSize,
                totalPages = GetTotalPages(result.TotalCount, result.PageSize)
            },
            message = "Blog posts retrieved"
        });
    }

    [HttpGet("blog/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBlogPost(string slug, CancellationToken cancellationToken = default)
    {
        var post = await cms.GetPublishedBlogPostAsync(slug.Trim(), cancellationToken);
        return post is null
            ? NotFound(new { success = false, message = "Blog post not found" })
            : Ok(new { success = true, data = post, message = "Blog post retrieved" });
    }

    [HttpPost("contact")]
    [AllowAnonymous]
    public async Task<IActionResult> SubmitContact(
        [FromBody] CmsContactMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Subject)
            || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { success = false, message = "All contact fields are required" });
        }

        var id = await cms.SubmitContactMessageAsync(request, cancellationToken);
        return Ok(new { success = true, data = new { id }, message = "Message sent successfully" });
    }

    [HttpPost("newsletter/subscribe")]
    [AllowAnonymous]
    public async Task<IActionResult> Subscribe(
        [FromBody] CmsNewsletterSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { success = false, message = "Email is required" });
        }

        var created = await cms.SubscribeAsync(request, cancellationToken);
        return Ok(new
        {
            success = true,
            data = new { created },
            message = created ? "Subscribed successfully" : "Already subscribed"
        });
    }

    [HttpPost("newsletter/unsubscribe")]
    [AllowAnonymous]
    public async Task<IActionResult> Unsubscribe(
        [FromBody] CmsNewsletterUnsubscribeRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { success = false, message = "Unsubscribe token is required" });
        }

        var unsubscribed = await cms.UnsubscribeAsync(request.Token, cancellationToken);
        return unsubscribed
            ? Ok(new { success = true, message = "Unsubscribed successfully" })
            : BadRequest(new { success = false, message = "Invalid unsubscribe token" });
    }

    private static int GetTotalPages(int totalCount, int pageSize) =>
        totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
}
