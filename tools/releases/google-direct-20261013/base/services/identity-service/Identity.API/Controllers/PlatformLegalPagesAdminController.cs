using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Identity.Application.LegalPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/platform/admin/legal-pages")]
[Authorize(Roles = "SystemAdmin")]
[Authorize(Policy = "MfaRequired")]
[HasPermission(PlatformPermissions.Privacy.Manage)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlatformLegalPagesAdminController(IPlatformLegalPages legalPages) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await legalPages.GetAllAsync(cancellationToken) });

    [HttpPost("starter-drafts")]
    public async Task<IActionResult> CreateStarterDrafts(CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        try
        {
            var createdCount = await legalPages.CreateStarterDraftsAsync(actorId, cancellationToken);
            return Ok(new { success = true, data = new { createdCount } });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await legalPages.GetAsync(slug, cancellationToken);
            return page is null
                ? NotFound(new { success = false, message = "Yasal sayfa bulunamadı." })
                : Ok(new { success = true, data = page });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpDelete("{slug}")]
    public async Task<IActionResult> Archive(string slug, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        try
        {
            var page = await legalPages.ArchiveAsync(slug, actorId, cancellationToken);
            return page is null
                ? NotFound(new { success = false, message = "Yasal sayfa bulunamadı." })
                : Ok(new { success = true, data = page });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPost("{slug}/restore")]
    public async Task<IActionResult> Restore(string slug, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        try
        {
            var page = await legalPages.RestoreAsync(slug, actorId, cancellationToken);
            return page is null
                ? NotFound(new { success = false, message = "Yasal sayfa bulunamadı." })
                : Ok(new { success = true, data = page });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPut("{slug}")]
    public async Task<IActionResult> Upsert(
        string slug,
        [FromBody] UpdateLegalPageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        try
        {
            var page = await legalPages.UpsertAsync(
                slug,
                new PlatformLegalPageUpdateRequest(request.Title, request.Content, request.IsPublished),
                actorId,
                cancellationToken);
            return Ok(new { success = true, data = page });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { success = false, message = exception.Message });
        }
    }

    [HttpGet("{slug}/revisions")]
    public async Task<IActionResult> GetRevisions(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(new { success = true, data = await legalPages.GetRevisionsAsync(slug, cancellationToken) });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    private bool TryGetActor(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out actorId);

    public sealed record UpdateLegalPageRequest(
        [Required, StringLength(200, MinimumLength = 1)] string Title,
        [Required, StringLength(200_000, MinimumLength = 1)] string Content,
        bool IsPublished);
}
