using EduPlatform.Gateway.Configuration;
using EduPlatform.Gateway.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace EduPlatform.Gateway.Middlewares;

/// <summary>
/// Applies the path's body limit before anything reads the body. A declared Content-Length
/// over the limit is rejected immediately with 413; chunked bodies are capped by Kestrel.
/// </summary>
public sealed class RequestBodyLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly GatewayRequestLimitOptions _options;

    public RequestBodyLimitMiddleware(RequestDelegate next, IOptions<GatewayRequestLimitOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var limit = _options.ResolveLimit(context.Request.Path);

        if (context.Request.ContentLength is { } length && length > limit)
        {
            return GatewayProblemDetails.WriteAsync(
                context,
                StatusCodes.Status413PayloadTooLarge,
                GatewayProblemDetails.PayloadTooLargeType,
                "Payload Too Large",
                "İstek gövdesi bu uç nokta için izin verilen boyutu aşıyor.",
                new Dictionary<string, object?> { ["maxRequestBodySizeBytes"] = limit });
        }

        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = limit;
        }

        return _next(context);
    }
}
