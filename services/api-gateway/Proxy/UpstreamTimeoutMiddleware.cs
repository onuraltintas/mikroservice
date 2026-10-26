using EduPlatform.Gateway.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Yarp.ReverseProxy.Forwarder;

namespace EduPlatform.Gateway.Proxy;

/// <summary>
/// The request-timeout middleware cancels the request token; YARP then reports the
/// forward as cancelled by the client and answers 400. This step recognises that case and
/// answers 504 with a problem body instead, so callers can tell a slow service from a bad
/// request. A genuine client disconnect is left alone.
/// </summary>
public sealed class UpstreamTimeoutMiddleware
{
    private readonly RequestDelegate _next;

    public UpstreamTimeoutMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        var timedOut = context.Features.Get<IHttpRequestTimeoutFeature>()?.RequestTimeoutToken.IsCancellationRequested == true;
        if (!timedOut || context.GetForwarderErrorFeature() is null || context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        await GatewayProblemDetails.WriteAsync(
            context,
            StatusCodes.Status504GatewayTimeout,
            GatewayProblemDetails.GatewayTimeoutType,
            "Gateway Timeout",
            GatewayProblemDetails.GatewayTimeoutDetail);
    }
}
