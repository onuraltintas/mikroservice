using System.Security.Claims;
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
    string? UserAgent);

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
            var record = CreateRecord(context, statusCode);

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

    private AdminAuditRecord CreateRecord(HttpContext context, int statusCode)
    {
        var roles = context.User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);
        var actorUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub")
            ?? "unknown";

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
            Truncate(context.Request.Headers.UserAgent.ToString(), 256));
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
