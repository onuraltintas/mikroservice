using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EduPlatform.Shared.Infrastructure.Middleware;

public sealed record AdminAuditRecord(
    Guid Id,
    DateTimeOffset OccurredAt,
    string ServiceName,
    string ActorUserId,
    string ActorRoles,
    string? TenantId,
    string HttpMethod,
    string Path,
    int StatusCode,
    string CorrelationId,
    string? ClientIp,
    string? UserAgent,
    string? Action = null,
    string? ResourceType = null,
    string? ResourceId = null,
    string? ChangedFieldsJson = null);

public interface IAdminAuditWriter
{
    Task WriteAsync(AdminAuditRecord record, CancellationToken cancellationToken);
}

public sealed class AdminAuditMiddleware
{
    private static readonly HashSet<string> AdministrativeRoles = new(
        ["SystemAdmin", "InstitutionOwner", "InstitutionAdmin"],
        StringComparer.OrdinalIgnoreCase);

    private readonly RequestDelegate _next;
    private readonly string _serviceName;
    private readonly ILogger<AdminAuditMiddleware> _logger;

    public AdminAuditMiddleware(
        RequestDelegate next,
        IHostEnvironment environment,
        ILogger<AdminAuditMiddleware> logger)
    {
        _next = next;
        _serviceName = environment.ApplicationName;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAdminAuditWriter writer)
    {
        if (!ShouldAudit(context))
        {
            await _next(context);
            return;
        }

        var changedFieldsJson = await CaptureChangedFieldsAsync(context);
        Exception? requestFailure = null;
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            requestFailure = exception;
            throw;
        }
        finally
        {
            var statusCode = requestFailure is null
                ? context.Response.StatusCode
                : StatusCodes.Status500InternalServerError;
            var record = CreateRecord(context, statusCode, changedFieldsJson);

            try
            {
                await writer.WriteAsync(record, CancellationToken.None);
            }
            catch (Exception auditException)
            {
                _logger.LogError(
                    auditException,
                    "Failed to persist admin audit record {AuditId} for {Method} {Path}",
                    record.Id,
                    record.HttpMethod,
                    record.Path);
            }
        }
    }

    private AdminAuditRecord CreateRecord(
        HttpContext context,
        int statusCode,
        string? changedFieldsJson)
    {
        var roles = context.User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);
        var actorUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub")
            ?? "unknown";
        var (resourceType, resourceId) = ResolveResource(context.Request.Path);

        return new AdminAuditRecord(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            _serviceName,
            actorUserId,
            string.Join(',', roles),
            context.User.FindFirstValue("institution_id"),
            context.Request.Method,
            context.Request.Path.Value ?? string.Empty,
            statusCode,
            context.TraceIdentifier,
            context.Connection.RemoteIpAddress?.ToString(),
            Truncate(context.Request.Headers.UserAgent.ToString(), 256),
            ResolveAction(context.Request),
            resourceType,
            resourceId,
            changedFieldsJson);
    }

    private static async Task<string?> CaptureChangedFieldsAsync(HttpContext context)
    {
        if (!IsJsonMutation(context)
            || context.Request.ContentLength is null or 0 or > 64_000
            || context.Request.Body is null)
        {
            return null;
        }

        try
        {
            context.Request.EnableBuffering();
            if (context.Request.Body.CanSeek)
                context.Request.Body.Position = 0;

            using var document = await JsonDocument.ParseAsync(
                context.Request.Body,
                cancellationToken: CancellationToken.None);
            var fields = new HashSet<string>(StringComparer.Ordinal);
            CollectFieldNames(document.RootElement, null, fields);
            var selectedFields = fields
                .Order(StringComparer.Ordinal)
                .Select(field => field.Length > 128 ? field[..128] : field)
                .Take(100)
                .ToList();
            while (selectedFields.Count > 0)
            {
                var json = JsonSerializer.Serialize(selectedFields);
                if (json.Length <= 2_000)
                    return json;

                selectedFields.RemoveAt(selectedFields.Count - 1);
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            if (context.Request.Body.CanSeek)
                context.Request.Body.Position = 0;
        }
    }

    private static bool IsJsonMutation(HttpContext context) =>
        (HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method))
        && context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true;

    private static void CollectFieldNames(
        JsonElement element,
        string? prefix,
        ISet<string> fields)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (IsSensitive(property.Name))
                    continue;

                var name = string.IsNullOrWhiteSpace(prefix)
                    ? property.Name
                    : $"{prefix}.{property.Name}";
                fields.Add(name);
                CollectFieldNames(property.Value, name, fields);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                CollectFieldNames(item, prefix, fields);
        }
    }

    private static bool IsSensitive(string propertyName) =>
        propertyName.Contains("password", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("token", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("authorization", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("accesskey", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("privatekey", StringComparison.OrdinalIgnoreCase);

    private static string ResolveAction(HttpRequest request)
    {
        var terminalSegment = request.Path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        if (terminalSegment is not null)
        {
            var operation = terminalSegment.ToLowerInvariant() switch
            {
                "attendance" => "attendance",
                "cancel" => "cancel",
                "complete" => "complete",
                "content" => "upload-attachment",
                "grade" => "grade",
                "progress" => "progress",
                "results" when HttpMethods.IsPost(request.Method) => "add-result",
                "student-note" => "student-note",
                "submit" => "submit",
                _ => null
            };

            if (operation is not null)
                return operation;
        }

        return request.Method.ToUpperInvariant() switch
        {
            "POST" => "create",
            "PUT" or "PATCH" => "update",
            "DELETE" => "delete",
            _ => request.Method.ToLowerInvariant()
        };
    }

    private static (string? ResourceType, string? ResourceId) ResolveResource(PathString path)
    {
        var segments = path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];
        if (segments.Length == 0)
            return (null, null);

        var start = Array.FindIndex(segments, segment =>
            segment.Equals("api", StringComparison.OrdinalIgnoreCase));
        start = start >= 0 ? start + 1 : 0;
        if (start < segments.Length && segments[start].EndsWith("-admin", StringComparison.OrdinalIgnoreCase))
            start++;
        if (start >= segments.Length)
            return (null, null);

        return (
            segments[start],
            start + 1 < segments.Length ? segments[start + 1] : null);
    }

    private static bool ShouldAudit(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || HttpMethods.IsGet(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method))
        {
            return false;
        }

        return context.User.FindAll(ClaimTypes.Role)
            .Any(claim => AdministrativeRoles.Contains(claim.Value));
    }

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value[..Math.Min(value.Length, maximumLength)];
}
