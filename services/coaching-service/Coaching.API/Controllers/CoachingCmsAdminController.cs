using System.Security.Claims;
using Asp.Versioning;
using Coaching.Application.Content;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching-admin/cms")]
[Authorize]
[HasPermission(PlatformPermissions.Coaching.ContentManage)]
[MfaCategory(MfaOperationCategories.Coaching)]
public sealed class CoachingCmsAdminController(ICoachingCms cms) : ControllerBase
{
    [HttpGet("entries")]
    public async Task<IActionResult> GetEntries(
        [FromQuery] string kind = "Page",
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? group = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(new { success = true, data = await cms.GetEntriesAsync(kind, pageNumber, pageSize, search, group, cancellationToken) });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpGet("entries/{id:guid}")]
    public async Task<IActionResult> GetEntry(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await cms.GetEntryAsync(id, cancellationToken);
        return entry is null
            ? NotFound(new { success = false, message = "İçerik bulunamadı." })
            : Ok(new { success = true, data = entry });
    }

    [HttpPost("entries")]
    public async Task<IActionResult> CreateEntry([FromBody] CoachingCmsEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        try
        {
            var id = await cms.CreateEntryAsync(request, actorId, cancellationToken);
            return id is null
                ? Conflict(new { success = false, message = "Bu içerik adresi aynı türde zaten kullanılıyor." })
                : Ok(new { success = true, data = new { id } });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPut("entries/{id:guid}")]
    public async Task<IActionResult> UpdateEntry(Guid id, [FromBody] CoachingCmsEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        if (await cms.GetEntryAsync(id, cancellationToken) is null)
            return NotFound(new { success = false, message = "İçerik bulunamadı." });
        try
        {
            return await cms.UpdateEntryAsync(id, request, actorId, cancellationToken)
                ? Ok(new { success = true, message = "İçerik güncellendi." })
                : Conflict(new { success = false, message = "Bu içerik adresi aynı türde zaten kullanılıyor." });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpDelete("entries/{id:guid}")]
    public async Task<IActionResult> DeleteEntry(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        return await cms.DeleteEntryAsync(id, actorId, cancellationToken)
            ? Ok(new { success = true, message = "İçerik silindi." })
            : NotFound(new { success = false, message = "İçerik bulunamadı." });
    }

    [HttpGet("entries/{id:guid}/revisions")]
    public async Task<IActionResult> GetRevisions(Guid id, CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await cms.GetRevisionsAsync(id, cancellationToken) });

    [HttpPost("entries/{id:guid}/revisions/{revisionId:guid}/restore")]
    public async Task<IActionResult> RestoreRevision(Guid id, Guid revisionId, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        return await cms.RestoreRevisionAsync(id, revisionId, actorId, cancellationToken)
            ? Ok(new { success = true, message = "İçeriğin önceki sürümü geri yüklendi." })
            : NotFound(new { success = false, message = "İçerik veya sürüm bulunamadı." });
    }

    [HttpGet("navigation")]
    public async Task<IActionResult> GetNavigation(
        [FromQuery] string menu = "Main",
        [FromQuery] bool includeHidden = true,
        CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await cms.GetNavigationAsync(menu, includeHidden, cancellationToken) });

    [HttpPost("navigation")]
    public async Task<IActionResult> CreateNavigationItem([FromBody] CoachingCmsNavigationItemRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        try
        {
            var id = await cms.CreateNavigationItemAsync(request, actorId, cancellationToken);
            return Ok(new { success = true, data = new { id } });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPut("navigation/{id:guid}")]
    public async Task<IActionResult> UpdateNavigationItem(Guid id, [FromBody] CoachingCmsNavigationItemRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        try
        {
            return await cms.UpdateNavigationItemAsync(id, request, actorId, cancellationToken)
                ? Ok(new { success = true, message = "Menü öğesi güncellendi." })
                : NotFound(new { success = false, message = "Menü öğesi bulunamadı." });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpDelete("navigation/{id:guid}")]
    public async Task<IActionResult> DeleteNavigationItem(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        return await cms.DeleteNavigationItemAsync(id, actorId, cancellationToken)
            ? Ok(new { success = true, message = "Menü öğesi silindi." })
            : NotFound(new { success = false, message = "Menü öğesi bulunamadı." });
    }

    [HttpGet("media")]
    public async Task<IActionResult> GetMedia(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await cms.GetMediaAssetsAsync(pageNumber, pageSize, cancellationToken) });

    [HttpPost("media")]
    [RequestSizeLimit(CoachingCmsMediaPolicy.MaxFileSizeBytes + 1_024)]
    public async Task<IActionResult> UploadMedia(
        IFormFile? file,
        [FromForm] string? altText = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        if (file is null) return BadRequest(new { success = false, message = "Yüklenecek bir görsel seçin." });

        await using var content = file.OpenReadStream();
        try
        {
            var result = await cms.UploadMediaAsync(
                new CoachingCmsMediaUpload(file.FileName, file.ContentType, file.Length, content, altText),
                actorId,
                cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpDelete("media/{id:guid}")]
    public async Task<IActionResult> DeleteMedia(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        return await cms.DeleteMediaAsync(id, actorId, cancellationToken)
            ? Ok(new { success = true, message = "Görsel Koçluk CMS kütüphanesinden silindi." })
            : NotFound(new { success = false, message = "Görsel bulunamadı." });
    }

    private bool TryGetActor(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out actorId);
}
