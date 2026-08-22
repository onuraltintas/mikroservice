using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notification.Infrastructure.Persistence;

namespace Notification.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin-audit/notification")]
[Authorize(Roles = "SystemAdmin")]
[HasPermission(PlatformPermissions.Operations.View)]
public sealed class AdminAuditController(NotificationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminAuditPage>> GetAsync(
        [FromQuery] AdminAuditQueryParameters request,
        CancellationToken cancellationToken)
    {
        if (!IsValid(request))
        {
            return ValidationProblem("Page must be 1-1000, pageSize 1-100, search at most 100 characters, and from must not exceed to.");
        }

        var query = ApplyFilters(dbContext.AdminAuditRecords.AsNoTracking(), request);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(record => record.OccurredAt)
            .ThenByDescending(record => record.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return Ok(new AdminAuditPage(items, totalCount, request.Page, request.PageSize));
    }

    private static IQueryable<AdminAuditRecord> ApplyFilters(
        IQueryable<AdminAuditRecord> query,
        AdminAuditQueryParameters request)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(record =>
                record.ActorUserId.Contains(search)
                || record.Path.Contains(search)
                || record.CorrelationId.Contains(search)
                || (record.Action != null && record.Action.Contains(search))
                || (record.ResourceType != null && record.ResourceType.Contains(search))
                || (record.ResourceId != null && record.ResourceId.Contains(search))
                || (record.ChangedFieldsJson != null && record.ChangedFieldsJson.Contains(search)));
        }

        if (request.StatusCode.HasValue)
        {
            query = query.Where(record => record.StatusCode == request.StatusCode.Value);
        }

        if (request.From.HasValue)
        {
            query = query.Where(record => record.OccurredAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(record => record.OccurredAt <= request.To.Value);
        }

        return query;
    }

    private static bool IsValid(AdminAuditQueryParameters request) =>
        request.Page is >= 1 and <= 1000
        && request.PageSize is >= 1 and <= 100
        && (request.Search?.Length ?? 0) <= 100
        && (!request.StatusCode.HasValue || request.StatusCode is >= 100 and <= 599)
        && (!request.From.HasValue || !request.To.HasValue || request.From <= request.To);
}
