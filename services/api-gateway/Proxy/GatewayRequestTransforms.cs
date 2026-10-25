using EduPlatform.Shared.Infrastructure.Middleware;
using Yarp.ReverseProxy.Transforms;

namespace EduPlatform.Gateway.Proxy;

/// <summary>
/// Request headers the gateway owns on every proxied request.
/// </summary>
public static class GatewayRequestTransforms
{
    /// <summary>
    /// Client address resolved by the gateway's trusted-proxy handling. Coaching and
    /// Speed Reading partition their public write limits by this value, so a caller-supplied
    /// copy must never reach a service.
    /// </summary>
    public const string ClientIpHeader = "X-EduPlatform-Client-IP";

    public static IReverseProxyBuilder AddGatewayRequestTransforms(this IReverseProxyBuilder builder) =>
        builder.AddTransforms(context => context.AddRequestTransform(transform =>
        {
            var headers = transform.ProxyRequest.Headers;

            headers.Remove(ClientIpHeader);
            var clientAddress = transform.HttpContext.Connection.RemoteIpAddress;
            if (clientAddress is not null)
            {
                if (clientAddress.IsIPv4MappedToIPv6)
                {
                    clientAddress = clientAddress.MapToIPv4();
                }

                headers.TryAddWithoutValidation(ClientIpHeader, clientAddress.ToString());
            }

            // CorrelationIdMiddleware has already replaced an unsafe caller value, so the
            // service logs the same id the gateway returned to the caller.
            headers.Remove(CorrelationIdMiddleware.HeaderName);
            headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, transform.HttpContext.TraceIdentifier);

            return ValueTask.CompletedTask;
        }));
}
