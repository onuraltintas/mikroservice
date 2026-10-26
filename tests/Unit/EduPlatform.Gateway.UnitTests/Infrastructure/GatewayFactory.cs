using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace EduPlatform.Gateway.UnitTests.Infrastructure;

/// <summary>
/// Hosts the real gateway Program in-memory and points every YARP cluster at <see cref="BackendStub"/>.
/// </summary>
public sealed class GatewayFactory : WebApplicationFactory<Program>
{
    public const string JwtSecret = "gateway-tests-secret-0123456789-abcdefghijklmnopqrstuvwxyz";
    public const string JwtIssuer = "eduplatform.identity";
    public const string JwtAudience = "eduplatform.api";
    public const string TestRemoteIpHeader = "X-Test-Remote-Ip";

    public static readonly string[] ClusterIds =
    [
        "identity-cluster",
        "coaching-cluster",
        "notification-cluster",
        "speed-reading-cluster",
        "course-management-cluster"
    ];

    private readonly string _backendBaseAddress;
    private readonly IReadOnlyDictionary<string, string?> _settings;

    public GatewayFactory(string backendBaseAddress, IReadOnlyDictionary<string, string?>? settings = null)
    {
        _backendBaseAddress = backendBaseAddress;
        _settings = settings ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("JWT_SECRET", JwtSecret);
        builder.UseSetting("JWT_ISSUER", JwtIssuer);
        builder.UseSetting("JWT_AUDIENCE", JwtAudience);
        builder.UseSetting("JWT_KEY_ID", "tests");
        // Production runs without Course Management; the contract tests cover the routes it adds.
        builder.UseSetting("CourseManagement:Enabled", "true");

        foreach (var clusterId in ClusterIds)
        {
            var name = clusterId.Replace("-cluster", string.Empty, StringComparison.Ordinal);
            builder.UseSetting(
                $"ReverseProxy:Clusters:{clusterId}:Destinations:destination1:Address",
                $"{_backendBaseAddress}/{name}");
        }

        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter, TestRemoteIpStartupFilter>());
    }

    /// <summary>A random private client address so Redis counters never collide between runs.</summary>
    public static string RandomClientIp() =>
        $"10.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";

    public static string CreateToken(string userId, IEnumerable<string>? roles = null, IEnumerable<string>? permissions = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange((roles ?? []).Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange((permissions ?? []).Select(permission => new Claim("permission", permission)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)) { KeyId = "tests" };
        var token = new JwtSecurityToken(
            JwtIssuer,
            JwtAudience,
            claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        token.Header["kid"] = "tests";
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// TestServer has no socket, so tests choose the client address through a test-only header
    /// that is removed before the gateway pipeline runs.
    /// </summary>
    private sealed class TestRemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(TestRemoteIpHeader, out var value)
                    && IPAddress.TryParse(value.ToString(), out var address))
                {
                    context.Connection.RemoteIpAddress = address;
                }

                context.Request.Headers.Remove(TestRemoteIpHeader);
                await nextMiddleware(context);
            });
            next(app);
        };
    }
}
