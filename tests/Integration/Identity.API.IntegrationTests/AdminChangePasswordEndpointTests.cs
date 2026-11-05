using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using EduPlatform.Shared.Infrastructure.Extensions;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Extensions;
using EduPlatform.Shared.Security.Interfaces;
using EduPlatform.Shared.Security.Services;
using FluentAssertions;
using FluentValidation;
using Identity.Application;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.API.IntegrationTests;

/// <summary>
/// POST api/users/{id}/change-password through the real MVC pipeline: test session claims,
/// the real permission/role/MFA policies, MediatR + FluentValidation, the global exception
/// handler, LocalIdentityService and a migrated PostgreSQL database.
/// </summary>
public sealed class AdminChangePasswordEndpointTests : IAsyncLifetime
{
    private const string PolicyMessage =
        "Şifre 8-128 karakter arasında olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir.";

    private IdentityPostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await IdentityPostgresDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Theory]
    [InlineData("Sifre-2026")]
    [InlineData("Sifre_2026")]
    [InlineData("Yeni+Sifre9")]
    [InlineData("Guclu!Sifre1")]
    public async Task SystemAdminWithMfa_SetsAPolicyCompliantPassword_AndRevokesSessions(string password)
    {
        var target = await SeedUserWithSessionAsync();
        await using var app = await StartAsync();
        var client = AdminClient(app);

        var response = await client.PostAsJsonAsync($"/api/users/{target}/change-password", new { password });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await using var context = _database.CreateContext();
        var user = await context.Users.SingleAsync(u => u.Id == target);
        new PasswordHasher().VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt).Should().BeTrue();
        (await context.RefreshTokens.Where(t => t.UserId == target).AllAsync(t => t.RevokedAt != null))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("Kisa-1A")]
    [InlineData("sifre-2026")]
    [InlineData("SIFRE-2026")]
    [InlineData("Sifre-Yeni")]
    [InlineData("Sifre2026")]
    [InlineData("")]
    public async Task PolicyViolations_ReturnTurkishValidationProblem_AndKeepTheOldPassword(string password)
    {
        var target = await SeedUserWithSessionAsync();
        await using var app = await StartAsync();
        var client = AdminClient(app);

        var response = await client.PostAsJsonAsync($"/api/users/{target}/change-password", new { password });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var messages = await ValidationMessagesAsync(response);
        messages.Should().NotBeEmpty();
        messages.Should().OnlyContain(message => message == PolicyMessage || message == "Şifre boş olamaz.");
        await using var context = _database.CreateContext();
        var user = await context.Users.SingleAsync(u => u.Id == target);
        user.PasswordHash.Should().Equal(new byte[64]);
    }

    [Fact]
    public async Task UnknownUser_ReturnsTurkishError()
    {
        await using var app = await StartAsync();
        var client = AdminClient(app);

        var response = await client.PostAsJsonAsync($"/api/users/{Guid.NewGuid()}/change-password", new { password = "Sifre-2026" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Kullanıcı bulunamadı.");
    }

    [Fact]
    public async Task SystemAdminWithoutMfa_GetsAnMfaProblem()
    {
        var target = await SeedUserWithSessionAsync();
        await using var app = await StartAsync();
        var client = AdminClient(app, mfa: false);

        var response = await client.PostAsJsonAsync($"/api/users/{target}/change-password", new { password = "Sifre-2026" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be("Auth.MfaRequired");
        body.RootElement.GetProperty("detail").GetString().Should().Contain("MFA");
    }

    [Fact]
    public async Task SystemAdminWithoutThePermission_GetsAPermissionProblem()
    {
        var target = await SeedUserWithSessionAsync();
        await using var app = await StartAsync();
        var client = AdminClient(app, permission: false);

        var response = await client.PostAsJsonAsync($"/api/users/{target}/change-password", new { password = "Sifre-2026" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be("Auth.Forbidden");
        body.RootElement.GetProperty("detail").GetString().Should().Be("Bu işlem için yetkiniz yok.");
    }

    private static async Task<string[]> ValidationMessagesAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("errors").EnumerateObject()
            .SelectMany(property => property.Value.EnumerateArray().Select(item => item.GetString()!))
            .ToArray();
    }

    private async Task<Guid> SeedUserWithSessionAsync()
    {
        var user = User.Create(Guid.NewGuid(), $"pw-{Guid.NewGuid():N}@example.test", "Ayşe", "Yılmaz");
        user.SetPassword(new byte[64], new byte[32]);
        await using var context = _database.CreateContext();
        context.Users.Add(user);
        context.RefreshTokens.Add(RefreshToken.Create(
            user.Id, $"session-{Guid.NewGuid():N}", DateTime.UtcNow.AddDays(7), "127.0.0.1", product: PlatformProduct.Coaching));
        await context.SaveChangesAsync();
        return user.Id;
    }

    private static HttpClient AdminClient(WebApplication app, bool mfa = true, bool permission = true)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Session", Guid.NewGuid().ToString());
        if (mfa) client.DefaultRequestHeaders.Add("X-Test-Mfa", "true");
        if (permission) client.DefaultRequestHeaders.Add("X-Test-Permission", "true");
        return client;
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _database.ConnectionString,
        });

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddValidatorsFromAssembly(typeof(Identity.Application.DependencyInjection).Assembly);
        builder.Services.AddMediatorWithBehaviors(typeof(Identity.Application.DependencyInjection).Assembly);
        builder.Services.AddApplication();
        builder.Services.AddGlobalExceptionHandler();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(Identity.API.Controllers.UserController).Assembly)
            .AddEduPlatformApiConventions();

        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, Session>("test", _ => { });
        builder.Services.AddCustomAuthorization();
        builder.Services.Replace(ServiceDescriptor.Scoped<IMfaPolicyStore, RequiredMfaPolicy>());

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class RequiredMfaPolicy : IMfaPolicyStore
    {
        public Task<string> GetModeAsync(string category, CancellationToken cancellationToken = default) =>
            Task.FromResult(MfaPolicyModes.Required);
    }

    private sealed class Session(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Session", out var session))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, session.ToString()),
                new(ClaimTypes.Role, "SystemAdmin"),
            };
            if (Request.Headers.ContainsKey("X-Test-Permission"))
                claims.Add(new Claim("permission", Identity.Domain.Constants.Permissions.Users.ChangePassword));
            if (Request.Headers.ContainsKey("X-Test-Mfa"))
                claims.Add(new Claim("amr", "mfa"));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
