using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Vocabulary;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/vocabulary")]
[Authorize]
public sealed class VocabularyController(ISpeedReadingVocabulary vocabulary) : ControllerBase
{
    [HttpGet]
    public Task<VocabularyPage> GetItems(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] int? difficultyLevel,
        [FromQuery] Guid? ageGroupId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        vocabulary.GetItemsAsync(search, category, difficultyLevel, ageGroupId, pageNumber, pageSize, cancellationToken);

    [HttpGet("categories")]
    public Task<IReadOnlyList<string>> GetCategories(CancellationToken cancellationToken = default) =>
        vocabulary.GetCategoriesAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetItem(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await vocabulary.GetItemAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> CreateItem(
        [FromBody] VocabularyItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await vocabulary.CreateItemAsync(request, actorId, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { success = false, message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> UpdateItem(
        Guid id,
        [FromBody] VocabularyItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        try
        {
            var item = await vocabulary.UpdateItemAsync(id, request, actorId, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { success = false, message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return await vocabulary.DeleteItemAsync(id, actorId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpGet("user")]
    public async Task<IActionResult> GetUserVocabulary(
        [FromQuery] int? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (status is < 1 or > 5)
        {
            return BadRequest(new { success = false, message = "Status must be between 1 and 5." });
        }

        return Ok(await vocabulary.GetUserVocabularyAsync(userId, status, cancellationToken));
    }

    [HttpPost("user")]
    public async Task<IActionResult> AddToUserVocabulary(
        [FromBody] AddUserVocabularyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var progress = await vocabulary.AddToUserVocabularyAsync(userId, request.VocabularyItemId, cancellationToken);
        return progress is null ? NotFound() : Ok(progress);
    }

    [HttpPut("user/{id:guid}")]
    public async Task<IActionResult> UpdateUserVocabulary(
        Guid id,
        [FromBody] UpdateUserVocabularyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return await vocabulary.UpdateUserVocabularyAsync(userId, id, request.IsCorrect, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpGet("user/due")]
    public async Task<IActionResult> GetDueForReview(CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await vocabulary.GetDueForReviewAsync(userId, cancellationToken));
    }

    [HttpPost("import")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Import(
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "A non-empty CSV file is required." });
        }

        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "Only CSV files are supported." });
        }

        await using var stream = file.OpenReadStream();
        return Ok(await vocabulary.ImportCsvAsync(stream, actorId, cancellationToken));
    }

    [HttpGet("export")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> Export(
        [FromQuery] string? category = null,
        [FromQuery] int? difficultyLevel = null,
        [FromQuery] Guid? ageGroupId = null,
        CancellationToken cancellationToken = default)
    {
        var csv = await vocabulary.ExportCsvAsync(category, difficultyLevel, ageGroupId, cancellationToken);
        return File(csv, "text/csv", $"vocabulary_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }

    [HttpGet("download-template")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public IActionResult DownloadTemplate()
    {
        const string csv = "Word,Definition,ExampleSentence,Synonyms,Antonyms,Category,DifficultyLevel,TargetAgeGroup\n" +
                           "kelime,tanım,örnek cümle,eş anlamlılar,zıt anlamlılar,Kategori,1,Child\n" +
                           "sözcük,açıklama,örnek kullanım,benzer kelimeler,karşıt kelimeler,Akademik,3,Teen";
        return File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv", "vocabulary_import_template.csv");
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}

public sealed record AddUserVocabularyRequest(Guid VocabularyItemId);

public sealed record UpdateUserVocabularyRequest(bool IsCorrect);
