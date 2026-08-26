using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;
using System.Security.Claims;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/admin/cms")]
[Authorize]
[HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
public sealed class CmsAdminController(ISpeedReadingCms cms) : ControllerBase
{
    [HttpGet("blocks")]
    public async Task<IActionResult> GetBlocks([FromQuery] string? group = null, CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await cms.GetContentBlocksAsync(group, cancellationToken), message = "Content blocks retrieved" });

    [HttpGet("landing")]
    public async Task<IActionResult> GetLanding([FromQuery] string? group = null, CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await cms.GetContentBlocksAsync(group ?? "HomePage", cancellationToken), message = "Landing content retrieved" });

    [HttpPut("landing")]
    public async Task<IActionResult> UpdateLanding(
        [FromBody] CmsLandingUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await cms.UpsertLandingContentAsync(request.Group, request.Blocks, actorId, cancellationToken);
        return Ok(new { success = true, message = "Landing content updated" });
    }

    [HttpPost("blocks")]
    public async Task<IActionResult> CreateBlock(
        [FromBody] CmsContentBlockRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var id = await cms.CreateContentBlockAsync(request, actorId, cancellationToken);
        return Ok(new { success = true, data = new { id }, message = "Block created" });
    }

    [HttpPut("blocks/{id:guid}")]
    public async Task<IActionResult> UpdateBlock(
        Guid id,
        [FromBody] CmsContentBlockRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return await cms.UpdateContentBlockAsync(id, request, actorId, cancellationToken)
            ? Ok(new { success = true, message = "Block updated" })
            : NotFound(new { success = false, message = "Block not found" });
    }

    [HttpDelete("blocks/{id:guid}")]
    public Task<IActionResult> DeleteBlock(Guid id, CancellationToken cancellationToken = default) =>
        Delete(id, cms.DeleteContentBlockAsync, "Block not found", "Block deleted", cancellationToken);

    [HttpGet("pages")]
    public async Task<IActionResult> GetPages([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await cms.GetPagesAsync(pageNumber, pageSize, cancellationToken);
        return Ok(new { success = true, data = ToPageResult(result), message = "Pages retrieved" });
    }

    [HttpGet("pages/{id:guid}")]
    public async Task<IActionResult> GetPage(Guid id, CancellationToken cancellationToken = default)
    {
        var page = await cms.GetPageAsync(id, cancellationToken);
        return page is null
            ? NotFound(new { success = false, message = "Page not found" })
            : Ok(new { success = true, data = page, message = "Page retrieved" });
    }

    [HttpPost("pages")]
    public async Task<IActionResult> CreatePage([FromBody] CmsPageRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var id = await cms.CreatePageAsync(request, actorId, cancellationToken);
        return Ok(new { success = true, data = new { id }, message = "Page created" });
    }

    [HttpPut("pages/{id:guid}")]
    public async Task<IActionResult> UpdatePage(Guid id, [FromBody] CmsPageRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return await cms.UpdatePageAsync(id, request, actorId, cancellationToken)
            ? Ok(new { success = true, message = "Page updated" })
            : NotFound(new { success = false, message = "Page not found" });
    }

    [HttpDelete("pages/{id:guid}")]
    public Task<IActionResult> DeletePage(Guid id, CancellationToken cancellationToken = default) =>
        Delete(id, cms.DeletePageAsync, "Page not found", "Page deleted", cancellationToken);

    [HttpGet("blog")]
    public async Task<IActionResult> GetBlogPosts([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await cms.GetBlogPostsAsync(pageNumber, pageSize, cancellationToken);
        return Ok(new { success = true, data = ToPageResult(result), message = "Blog posts retrieved" });
    }

    [HttpGet("blog/{id:guid}")]
    public async Task<IActionResult> GetBlogPost(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await cms.GetBlogPostAsync(id, cancellationToken);
        return post is null
            ? NotFound(new { success = false, message = "Blog post not found" })
            : Ok(new { success = true, data = post, message = "Blog post retrieved" });
    }

    [HttpPost("blog")]
    public async Task<IActionResult> CreateBlogPost([FromBody] CmsBlogPostRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var id = await cms.CreateBlogPostAsync(request, actorId, cancellationToken);
        return Ok(new { success = true, data = new { id }, message = "Blog post created" });
    }

    [HttpPut("blog/{id:guid}")]
    public async Task<IActionResult> UpdateBlogPost(Guid id, [FromBody] CmsBlogPostRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return await cms.UpdateBlogPostAsync(id, request, actorId, cancellationToken)
            ? Ok(new { success = true, message = "Blog post updated" })
            : NotFound(new { success = false, message = "Blog post not found" });
    }

    [HttpDelete("blog/{id:guid}")]
    public Task<IActionResult> DeleteBlogPost(Guid id, CancellationToken cancellationToken = default) =>
        Delete(id, cms.DeleteBlogPostAsync, "Blog post not found", "Blog post deleted", cancellationToken);

    [HttpGet("newsletter/subscribers")]
    public async Task<IActionResult> GetSubscribers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await cms.GetSubscribersAsync(pageNumber, pageSize, cancellationToken);
        return Ok(new { success = true, data = ToPageResult(result), message = "Subscribers retrieved" });
    }

    [HttpDelete("newsletter/subscribers/{id:guid}")]
    public async Task<IActionResult> DeleteSubscriber(Guid id, [FromQuery] bool hardDelete = false, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return await cms.DeleteSubscriberAsync(id, hardDelete, actorId, cancellationToken)
            ? Ok(new { success = true, message = "Subscriber deleted" })
            : NotFound(new { success = false, message = "Subscriber not found" });
    }

    [HttpGet("contact-messages")]
    public async Task<IActionResult> GetContactMessages(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? isRead = null,
        [FromQuery] bool? isReplied = null,
        CancellationToken cancellationToken = default)
    {
        var result = await cms.GetContactMessagesAsync(pageNumber, pageSize, isRead, isReplied, cancellationToken);
        return Ok(new { success = true, data = ToPageResult(result), message = "Contact messages retrieved" });
    }

    [HttpGet("contact-messages/unread-count")]
    public async Task<IActionResult> GetUnreadContactMessageCount(CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await cms.GetUnreadContactMessageCountAsync(cancellationToken), message = "Unread messages count" });

    [HttpPost("contact-messages/reply")]
    public async Task<IActionResult> ReplyToContactMessage([FromBody] CmsContactReplyRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return await cms.ReplyToContactMessageAsync(request, actorId, cancellationToken)
            ? Ok(new { success = true, message = "Reply sent" })
            : NotFound(new { success = false, message = "Message not found" });
    }

    [HttpPut("contact-messages/{id:guid}/read")]
    public async Task<IActionResult> MarkContactMessageAsRead(Guid id, [FromBody] CmsMarkReadRequest? request, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return await cms.MarkContactMessageAsReadAsync(id, request?.IsRead ?? true, actorId, cancellationToken)
            ? Ok(new { success = true, message = "Message read state updated" })
            : NotFound(new { success = false, message = "Message not found" });
    }

    [HttpDelete("contact-messages/{id:guid}")]
    public Task<IActionResult> DeleteContactMessage(Guid id, CancellationToken cancellationToken = default) =>
        Delete(id, cms.DeleteContactMessageAsync, "Message not found", "Message deleted", cancellationToken);

    private async Task<IActionResult> Delete(
        Guid id,
        Func<Guid, Guid, CancellationToken, Task<bool>> delete,
        string notFoundMessage,
        string successMessage,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return await delete(id, actorId, cancellationToken)
            ? Ok(new { success = true, message = successMessage })
            : NotFound(new { success = false, message = notFoundMessage });
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }

    private static object ToPageResult<T>(SpeedReadingPage<T> result) => new
    {
        items = result.Items,
        totalCount = result.TotalCount,
        pageNumber = result.PageNumber,
        pageSize = result.PageSize,
        totalPages = result.TotalCount == 0 ? 0 : (int)Math.Ceiling(result.TotalCount / (double)result.PageSize)
    };
}

public sealed record CmsLandingUpdateRequest(
    string Group,
    Dictionary<string, string> Blocks);

public sealed record CmsMarkReadRequest(bool? IsRead);
