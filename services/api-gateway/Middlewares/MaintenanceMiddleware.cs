using Microsoft.Extensions.Caching.Distributed;
using Yarp.ReverseProxy.Model;
using System.Security.Claims;

namespace EduPlatform.Gateway.Middlewares;

public class MaintenanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDistributedCache _cache;
    private readonly ILogger<MaintenanceMiddleware> _logger;
    private const string CacheKeyPrefix = "config:";

    // Bakım modundan etkilenmeyen roller (Whitelist)
    private readonly HashSet<string> _allowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "SystemAdmin",
        "InstitutionOwner"
    };

    public MaintenanceMiddleware(
        RequestDelegate next,
        IDistributedCache cache,
        ILogger<MaintenanceMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // 1. Altyapı Endpoint'leri (Her zaman açık olmalı)
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) || 
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // 2. Authentication Endpoint'leri (Giriş yapabilmek için açık olmalı)
        // Login, Google Login, Register, Forgot Password gibi tüm auth işlemleri
        // Bu istekler Identity Service'e ulaşmalı ki kullanıcı kimlik doğrulabilsin (Admin mi değil mi anlaşılsın)
        if (path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/refresh-token", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/configurations", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // 3. Admin Bypass Kontrolü (Sektör Best Practice: Doğrulanmış Kimlik)
        // Manuel token parse YOK. Sadece doğrulanmış (imzası geçerli) kullanıcıya güvenilir.
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // Kullanıcının rollerini kontrol et
            if (IsAdminUser(context.User))
            {
                // Admin kullanıcıları bakım modundan etkilenmez
                await _next(context);
                return;
            }
        }

        // 4. Bakım Modu Kontrolleri
        
        // A. Genel Sistem Bakımı
        if (await IsInMaintenance("system.maintenancemode"))
        {
            await BlockRequest(context, "System", "Genel Bakım Modu Aktif. Lütfen daha sonra tekrar deneyiniz.");
            return;
        }

        // B. Mikroservis Bazlı Bakım (API Gateway Routing'e göre)
        var proxyFeature = context.GetReverseProxyFeature();
        if (proxyFeature?.Route?.Cluster is not null)
        {
            var clusterId = proxyFeature.Route.Cluster.ClusterId;
            // "identity-cluster" -> "identity"
            var serviceName = clusterId.Replace("-cluster", "", StringComparison.OrdinalIgnoreCase).ToLower().Trim();
            
            if (await IsInMaintenance($"maintenance.{serviceName}"))
            {
                await BlockRequest(context, serviceName, $"{char.ToUpper(serviceName[0]) + serviceName[1..]} Servisi Bakımda.");
                return;
            }
        }

        await _next(context);
    }

    private bool IsAdminUser(ClaimsPrincipal user)
    {
        // Standart Role Claim kontrolü
        foreach (var role in _allowedRoles)
        {
            if (user.IsInRole(role)) return true;
        }

        // Fallback: Bazı durumlarda "role" claim tipi farklı gelebilir, manuel claim kontrolü
        return user.Claims.Any(c => 
            (c.Type == ClaimTypes.Role || c.Type == "role" || c.Type.EndsWith("/role")) &&
            _allowedRoles.Contains(c.Value));
    }

    private async Task<bool> IsInMaintenance(string key)
    {
        try
        {
            var value = await _cache.GetStringAsync($"{CacheKeyPrefix}{key}");
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        { 
            // Redis hatası sistemi durdurmamalı, varsayılan olarak açık kabul et
            _logger.LogWarning(ex, "Redis maintenance check failed for key: {Key}", key);
            return false; 
        }
    }

    private async Task BlockRequest(HttpContext context, string serviceName, string message)
    {
        _logger.LogInformation("Request blocked due to maintenance. Service: {Service}, Path: {Path}", serviceName, context.Request.Path);
        
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new 
        { 
            error = "Service Unavailable", 
            message = message, 
            service = serviceName,
            timestamp = DateTime.UtcNow 
        });
    }
}
