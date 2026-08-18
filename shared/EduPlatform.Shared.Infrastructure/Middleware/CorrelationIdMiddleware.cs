using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace EduPlatform.Shared.Infrastructure.Middleware;

/// <summary>
/// Creates a bounded, non-PII request correlation identifier and places it in
/// both the response and Serilog LogContext.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    private const int MaximumLength = 128;
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context.Request.Headers[HeaderName].ToString());
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        context.Response.OnStarting(static state =>
        {
            var (httpContext, id) = ((HttpContext Context, string Id))state;
            httpContext.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        }, (context, correlationId));

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    public static string GetOrCreateCorrelationId(string? value)
    {
        return IsSafe(value) ? value! : Guid.NewGuid().ToString("N");
    }

    public static bool IsSafe(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaximumLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!(char.IsAsciiLetterOrDigit(character)
                  || character is '-' or '_' or '.' or ':'))
            {
                return false;
            }
        }

        return true;
    }
}

public static class CorrelationIdApplicationBuilderExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
