using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Infrastructure;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/admin-audit/speed-reading")]
[Authorize(Roles = "SystemAdmin")]
[HasPermission(PlatformPermissions.Operations.View)]
public sealed class AdminAuditController(
    IServiceProvider services,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminAuditPage>> GetAsync(
        [FromQuery] AdminAuditQueryParameters request,
        CancellationToken cancellationToken)
    {
        if (!IsValid(request))
        {
            return ValidationProblem("Page must be 1-1000, pageSize 1-100, search, action, and resourceType at most 100 characters, and from must not exceed to.");
        }

        var query = ApplyFilters(GetRecords().AsNoTracking(), request);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(record => record.OccurredAt)
            .ThenByDescending(record => record.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return Ok(new AdminAuditPage(items, totalCount, request.Page, request.PageSize));
    }

    [HttpGet("facets")]
    public async Task<ActionResult<AdminAuditFacets>> GetFacetsAsync(CancellationToken cancellationToken)
    {
        var records = GetRecords();
        var actions = await records
            .AsNoTracking()
            .Select(record => record.Action ?? record.HttpMethod)
            .Distinct()
            .OrderBy(action => action)
            .ToListAsync(cancellationToken);
        var resourceTypes = await records
            .AsNoTracking()
            .Where(record => record.ResourceType != null)
            .Select(record => record.ResourceType!)
            .Distinct()
            .OrderBy(resourceType => resourceType)
            .ToListAsync(cancellationToken);

        return Ok(new AdminAuditFacets(actions, resourceTypes));
    }

    private DbSet<AdminAuditRecord> GetRecords()
    {
        if (configuration.GetValue<bool>("SpeedReading:OwnedDataEnabled"))
        {
            return services.GetRequiredService<OwnedSpeedReadingDbContext>().AdminAuditRecords;
        }

        return services.GetRequiredService<SpeedReadingDbContext>().AdminAuditRecords;
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

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim().ToLowerInvariant();
            query = query.Where(record => (record.Action ?? record.HttpMethod).ToLower() == action);
        }

        if (!string.IsNullOrWhiteSpace(request.ResourceType))
        {
            var resourceType = request.ResourceType.Trim().ToLowerInvariant();
            query = query.Where(record => record.ResourceType != null && record.ResourceType.ToLower() == resourceType);
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
        && (request.Action?.Length ?? 0) <= 100
        && (request.ResourceType?.Length ?? 0) <= 100
        && (!request.StatusCode.HasValue || request.StatusCode is >= 100 and <= 599)
        && (!request.From.HasValue || !request.To.HasValue || request.From <= request.To);
}
