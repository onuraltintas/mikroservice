using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EduPlatform.Shared.Infrastructure.Middleware;

/// <summary>
/// Carries the current inbound correlation identifier to an internal HTTP call.
/// Explicit request headers are preserved so callers can intentionally continue
/// a different safe correlation chain.
/// </summary>
public sealed class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var hasSafeExplicitCorrelationId = false;
        if (request.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values))
        {
            var explicitValues = values.ToArray();
            hasSafeExplicitCorrelationId = explicitValues.Length == 1
                && CorrelationIdMiddleware.IsSafe(explicitValues[0]);

            if (!hasSafeExplicitCorrelationId)
            {
                request.Headers.Remove(CorrelationIdMiddleware.HeaderName);
            }
        }

        if (!hasSafeExplicitCorrelationId)
        {
            var correlationId = _httpContextAccessor.HttpContext?.TraceIdentifier;
            if (CorrelationIdMiddleware.IsSafe(correlationId))
            {
                request.Headers.TryAddWithoutValidation(
                    CorrelationIdMiddleware.HeaderName,
                    correlationId);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}

public static class CorrelationIdHttpClientBuilderExtensions
{
    public static IHttpClientBuilder AddCorrelationIdPropagation(this IHttpClientBuilder builder)
    {
        builder.Services.AddTransient<CorrelationIdDelegatingHandler>();
        return builder.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
    }
}
