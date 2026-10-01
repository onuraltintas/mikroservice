using System.Security.Claims;
using System.Text;
using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/admin/cms/newsletter")]
[Authorize]
[HasPermission(PlatformPermissions.SpeedReading.CommunicationsManage)]
[MfaCategory(MfaOperationCategories.Cms)]
public sealed class SpeedReadingNewsletterAdminController(ISpeedReadingNewsletter newsletter) : ControllerBase
{
    [HttpGet("subscribers")]
    public async Task<IActionResult> GetSubscribers(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await newsletter.GetSubscribersAsync(search, status, pageNumber, pageSize, cancellationToken);
        return Ok(new { success = true, data = ToPageResult(result), message = "Bülten aboneleri listelendi." });
    }

    [HttpGet("subscribers/export")]
    public async Task<IActionResult> ExportSubscribers(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var subscribers = await newsletter.ExportSubscribersAsync(search, status, cancellationToken);
        var csv = new StringBuilder("E-posta,Durum,Gizlilik Sürümü,Onay Tarihi,E-posta Doğrulama Tarihi,Abonelikten Çıkış Tarihi,Kaynak,Kayıt Tarihi\r\n");
        foreach (var subscriber in subscribers)
        {
            csv.Append(Csv(subscriber.Email)).Append(',')
                .Append(Csv(subscriber.Status)).Append(',')
                .Append(Csv(subscriber.PrivacyPolicyVersion?.ToString() ?? string.Empty)).Append(',')
                .Append(Csv(subscriber.ConsentedAt?.ToString("O") ?? string.Empty)).Append(',')
                .Append(Csv(subscriber.ConfirmedAt?.ToString("O") ?? string.Empty)).Append(',')
                .Append(Csv(subscriber.UnsubscribedAt?.ToString("O") ?? string.Empty)).Append(',')
                .Append(Csv(subscriber.Source ?? string.Empty)).Append(',')
                .Append(Csv(subscriber.CreatedAt.ToString("O"))).Append("\r\n");
        }

        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv; charset=utf-8",
            "hizli-okuma-bulten-aboneleri.csv");
    }

    [HttpDelete("subscribers/{id:guid}")]
    public async Task<IActionResult> DeleteSubscriber(
        Guid id,
        [FromQuery] bool hardDelete = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return await newsletter.DeleteSubscriberAsync(id, hardDelete, actorId, cancellationToken)
            ? Ok(new { success = true, message = hardDelete ? "Abone kaydı silindi." : "Abonelik iptal edildi." })
            : NotFound(new { success = false, message = "Abone kaydı bulunamadı." });
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId) && userId != Guid.Empty;
    }

    private static string Csv(string value)
    {
        var safe = value.TrimStart() is [var first, ..] && first is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n'
            ? $"'{value}"
            : value;
        return $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
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
