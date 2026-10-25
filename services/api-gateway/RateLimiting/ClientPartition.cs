using System.Net;
using System.Net.Sockets;
using System.Security.Claims;

namespace EduPlatform.Gateway.RateLimiting;

/// <summary>Builds rate-limit partition keys.</summary>
public static class ClientPartition
{
    /// <summary>
    /// IPv4 (including IPv4-mapped IPv6) uses the full address. IPv6 uses the /64 prefix,
    /// because a single subscriber routinely holds a whole /64 and can rotate within it.
    /// </summary>
    public static string ForAddress(IPAddress? address)
    {
        if (address is null)
        {
            return "ip:unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return $"ip:{address}";
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"ip6:{new IPAddress(bytes)}/64";
    }

    /// <summary>The authenticated subject (<c>sub</c>), or null for anonymous requests.</summary>
    public static string? ForUser(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var subject = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrWhiteSpace(subject) ? null : $"user:{subject}";
    }

    public static string Resolve(HttpContext context, RateLimitPartitionKind kind) =>
        kind == RateLimitPartitionKind.User
            ? ForUser(context.User) ?? ForAddress(context.Connection.RemoteIpAddress)
            : ForAddress(context.Connection.RemoteIpAddress);
}
