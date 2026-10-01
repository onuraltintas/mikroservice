using System.Globalization;
using System.Security.Claims;
using System.Text;
using Asp.Versioning;
using Coaching.Application.Newsletters;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching-admin/cms/newsletter/subscribers")]
[Authorize]
[HasPermission(PlatformPermissions.Coaching.ContentManage)]
[MfaCategory(MfaOperationCategories.Coaching)]
public sealed class CoachingNewsletterAdminController(ICoachingNewsletter newsletter) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSubscribers(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(new
            {
                success = true,
                data = await newsletter.GetSubscribersAsync(search, status, pageNumber, pageSize, cancellationToken)
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await newsletter.ExportSubscribersAsync(status, search, cancellationToken);
            var csv = new StringBuilder("Email,Status,Source,ConsentTextVersion,ConsentedAt,ConfirmedAt,UnsubscribedAt,CreatedAt\r\n");
            foreach (var row in rows)
            {
                csv.AppendJoin(',', new[]
                {
                    EscapeCsv(row.Email), EscapeCsv(row.Status), EscapeCsv(row.Source), EscapeCsv(row.ConsentTextVersion),
                    EscapeCsv(row.ConsentedAt.ToString("O", CultureInfo.InvariantCulture)),
                    EscapeCsv(row.ConfirmedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
                    EscapeCsv(row.UnsubscribedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
                    EscapeCsv(row.CreatedAt.ToString("O", CultureInfo.InvariantCulture))
                });
                csv.Append("\r\n");
            }

            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
                "text/csv; charset=utf-8", "coaching-newsletter-subscribers.csv");
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPost("{id:guid}/unsubscribe")]
    public async Task<IActionResult> Unsubscribe(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        return await newsletter.UnsubscribeSubscriberAsync(id, actorId, cancellationToken)
            ? Ok(new { success = true, message = "Abonenin kaydı iptal edildi." })
            : NotFound(new { success = false, message = "Abone bulunamadı." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        return await newsletter.DeleteSubscriberAsync(id, actorId, cancellationToken)
            ? Ok(new { success = true, message = "Abone kaydı kalıcı olarak silindi." })
            : NotFound(new { success = false, message = "Abone bulunamadı." });
    }

    private bool TryGetActor(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out actorId);

    private static string EscapeCsv(string value)
    {
        var safeValue = value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
            ? $"'{value}"
            : value;
        return $"\"{safeValue.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
