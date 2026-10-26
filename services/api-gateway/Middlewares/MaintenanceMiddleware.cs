using System.Security.Claims;
using EduPlatform.Gateway.Configuration;
using EduPlatform.Gateway.Http;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Model;

namespace EduPlatform.Gateway.Middlewares;

/// <summary>
/// Blocks proxied requests while the platform or the target service is in maintenance.
/// Only verified tokens are considered for the role bypass (the middleware runs after
/// authentication); sign-in and the public configuration stay reachable.
/// </summary>
public sealed class MaintenanceMiddleware
{
    private const string GlobalKey = "system.maintenancemode";

    private readonly RequestDelegate _next;
    private readonly MaintenanceFlagStore _flags;
    private readonly ILogger<MaintenanceMiddleware> _logger;
    private readonly HashSet<string> _bypassRoles;
    private readonly string[] _bypassPathPrefixes;

    public MaintenanceMiddleware(
        RequestDelegate next,
        MaintenanceFlagStore flags,
        IOptions<GatewayMaintenanceOptions> options,
        ILogger<MaintenanceMiddleware> logger)
    {
        _next = next;
        _flags = flags;
        _logger = logger;
        _bypassRoles = new HashSet<string>(options.Value.BypassRoles, StringComparer.OrdinalIgnoreCase);
        _bypassPathPrefixes = options.Value.BypassPathPrefixes;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (_bypassPathPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            || (context.User.Identity?.IsAuthenticated == true && HasBypassRole(context.User)))
        {
            await _next(context);
            return;
        }

        if (await _flags.IsEnabledAsync(GlobalKey, context.RequestAborted))
        {
            await BlockAsync(context, "System", "Genel Bakım Modu Aktif. Lütfen daha sonra tekrar deneyiniz.");
            return;
        }

        var clusterId = context.GetReverseProxyFeature()?.Route?.Cluster?.ClusterId;
        if (clusterId is not null)
        {
            // "identity-cluster" -> "identity"
            var service = clusterId.Replace("-cluster", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim()
                .ToLowerInvariant();
            if (service.Length > 0 && await _flags.IsEnabledAsync($"maintenance.{service}", context.RequestAborted))
            {
                await BlockAsync(context, service, $"{char.ToUpperInvariant(service[0])}{service[1..]} Servisi Bakımda.");
                return;
            }
        }

        await _next(context);
    }

    private bool HasBypassRole(ClaimsPrincipal user) =>
        _bypassRoles.Any(user.IsInRole)
        // Tokens keep their original claim names (MapInboundClaims = false).
        || user.Claims.Any(claim =>
            (claim.Type == ClaimTypes.Role || claim.Type == "role" || claim.Type.EndsWith("/role", StringComparison.Ordinal))
            && _bypassRoles.Contains(claim.Value));

    private Task BlockAsync(HttpContext context, string service, string message)
    {
        _logger.LogInformation("Request blocked due to maintenance. Service: {Service}, Path: {Path}", service, context.Request.Path);
        return GatewayProblemDetails.WriteMaintenanceAsync(context, service, message);
    }
}
