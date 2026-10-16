using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Coaching.Application.Profiles;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Security.Interfaces;
using EduPlatform.Shared.Security.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.API.IntegrationTests;

public sealed class CoachingOwnershipHttpTests
{
    [Theory]
    [InlineData("/api/internal/coaching/legacy-profile-snapshot")]
    [InlineData("/api/internal/coaching/institutions/11111111-1111-1111-1111-111111111111/active")]
    public async Task InternalDirectoryRejectsBrowserSessionsWithoutServiceKey(string path)
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture(); f.Actor.UserId = f.AddAccount("Student");
        await using var app = await Server(f); var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Session", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task HttpFlowRequiresProductSessionAndWritesOnlyAuthenticatedUsersCoachingProfile()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var student = f.AddAccount("Student"); var other = f.AddAccount("Student"); f.Actor.UserId = student;
        await using var app = await Server(f);
        var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/coaching/profiles/me")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Session", "true");
        client.DefaultRequestHeaders.Add("X-Test-Product", "speed-reading");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/coaching/profiles/me")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Product"); client.DefaultRequestHeaders.Add("X-Test-Product", "coaching");
        (await client.GetAsync("/api/coaching/profiles/me")).EnsureSuccessStatusCode();
        Assert.Empty(f.Db.CoachingStudentProfiles);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/coaching/profiles/me/provision", new { role = "Teacher" })).StatusCode);
        (await client.PostAsJsonAsync("/api/coaching/profiles/me/provision", new { role = "Student", userId = other })).EnsureSuccessStatusCode();
        Assert.Equal(student, (await f.Db.CoachingStudentProfiles.SingleAsync()).UserId);
        var profile = await f.Db.CoachingStudentProfiles.SingleAsync();
        var update = new UpdateCoachingStudentProfile(profile.Version, CoachingEducationStatus.Student, 12,
            "Test school", "Ankara", null, null, null, null, null, null, null, false);
        var json = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        json.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        (await client.PutAsJsonAsync("/api/coaching/profiles/me/student", update, json)).EnsureSuccessStatusCode();
        var stale = await client.PutAsJsonAsync("/api/coaching/profiles/me/student", update, json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(12, (await f.Db.CoachingStudentProfiles.SingleAsync()).GradeLevel);
        Assert.False((await f.Db.CoachingStudentProfiles.SingleAsync()).ShareProgressWithTeachers);
        f.Accounts.Users[student] = f.Accounts.Users[student] with { HasCoachingAccess = false };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/coaching/profiles/me")).StatusCode);
    }

    [Theory]
    [InlineData("/api/invitations/my-invitations")]
    [InlineData("/api/teachers/me/students")]
    public async Task LegacyIdentityProfileRoutesAreGoneBeforeControllerActivation(string path)
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture(); f.Actor.UserId = f.AddAccount("Student");
        await using var app = await Server(f);
        var client = app.GetTestClient(); client.DefaultRequestHeaders.Add("X-Test-Session", "true");
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Contains("Profiles.OwnedByCoaching", await response.Content.ReadAsStringAsync());
        Assert.Empty(f.Db.CoachingStudentProfiles);
    }

    private static async Task<WebApplication> Server(CoachingOwnedProfilePersistenceTests.Fixture f)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(f.Db);
        builder.Services.AddSingleton<ICoachingAccountDirectory>(f.Accounts);
        builder.Services.AddSingleton<ICurrentUserService>(f.Actor);
        builder.Services.AddScoped<CoachingProfileStore>();
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, Session>("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers(options => options.Filters.Add<Identity.API.CoachingOwnershipFilter>())
            .AddApplicationPart(typeof(Coaching.API.Controllers.CoachingProfilesController).Assembly)
            .AddApplicationPart(typeof(Identity.API.Controllers.InvitationsController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false)));
        var app = builder.Build(); app.UseAuthentication();
        app.UseWhen(context => context.Request.Path.StartsWithSegments("/api/coaching"), branch => branch.UseMiddleware<ProductScopeMiddleware>("coaching"));
        app.UseAuthorization(); app.MapControllers(); await app.StartAsync(); return app;
    }

    private sealed class Session(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, ICurrentUserService actor) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Test-Session")) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, actor.UserId!.Value.ToString()), new Claim(ClaimTypes.Role, "Student"), new Claim(ClaimTypes.Role, "Teacher"),
                new Claim("platform_product", Request.Headers["X-Test-Product"].FirstOrDefault() ?? "coaching") };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}
