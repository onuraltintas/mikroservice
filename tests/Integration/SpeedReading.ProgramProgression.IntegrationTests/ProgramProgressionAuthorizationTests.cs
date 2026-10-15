using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpeedReading.API.Controllers;
using SpeedReading.Application.DailyProgress;

namespace SpeedReading.ProgramProgression.IntegrationTests;

public sealed class ProgramProgressionAuthorizationTests
{
    [Theory]
    [InlineData("", false, false, 401)]
    [InlineData("Student", true, true, 403)]
    [InlineData("Teacher", true, true, 403)]
    [InlineData("InstitutionAdmin", true, true, 403)]
    [InlineData("SystemAdmin", false, true, 403)]
    [InlineData("SystemAdmin", true, false, 403)]
    [InlineData("SystemAdmin", true, true, 200)]
    [InlineData("Admin", true, true, 200)]
    public async Task Admin_mutation_requires_admin_role_permission_and_mfa(string role, bool permission, bool mfa, int status)
    {
        await using var app = CreateApp();
        await app.StartAsync();
        var client = app.GetTestClient();
        SetIdentity(client, role, permission, mfa);
        var response = await client.PutAsJsonAsync("/api/speed-reading/program-progression/settings", new { allowEarlyAdvance = true });
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        var preference = await client.PutAsJsonAsync($"/api/speed-reading/program-progression/users/{Guid.NewGuid()}", new { mode = "allow" });
        Assert.Equal((HttpStatusCode)status, preference.StatusCode);
    }

    [Theory]
    [InlineData("", 401)]
    [InlineData("Teacher", 403)]
    [InlineData("InstitutionAdmin", 403)]
    [InlineData("Student", 200)]
    public async Task Only_students_can_advance_and_the_actor_comes_from_the_authenticated_identity(string role, int status)
    {
        await using var app = CreateApp();
        await app.StartAsync();
        var client = app.GetTestClient();
        SetIdentity(client, role, true, true);
        var response = await client.PostAsJsonAsync("/api/speed-reading/daily-progress/advance-day", new {
            programProgressId = Guid.NewGuid(), completedProgramDay = 1, userId = Guid.NewGuid()
        });
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        if (status == 200) Assert.Equal(Identity.User, app.Services.GetRequiredService<Progression>().LastStudent);
    }

    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging(logging => logging.ClearProviders());
        builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, Identity>("Fixture", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        builder.Services.AddSingleton<IAuthorizationHandler, MfaAuthorizationHandler>();
        builder.Services.AddSingleton<IMfaPolicyStore, MfaStore>();
        builder.Services.AddSingleton<Progression>();
        builder.Services.AddSingleton<ISpeedReadingProgramProgression>(provider => provider.GetRequiredService<Progression>());
        // The tested daily-progress actions use progression only; the existing daily service is never called.
        builder.Services.AddSingleton<ISpeedReadingDailyProgress>(_ => System.Reflection.DispatchProxy.Create<ISpeedReadingDailyProgress, UnusedDaily>());
        builder.Services.AddControllers().AddApplicationPart(typeof(ProgramProgressionController).Assembly);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        return app;
    }

    private static void SetIdentity(HttpClient client, string role, bool permission, bool mfa)
    {
        if (role.Length > 0) client.DefaultRequestHeaders.Add("X-Fixture-Role", role);
        if (permission) client.DefaultRequestHeaders.Add("X-Fixture-Permission", "yes");
        if (mfa) client.DefaultRequestHeaders.Add("X-Fixture-Mfa", "yes");
    }

    private sealed class Identity(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public static readonly Guid User = Guid.NewGuid();
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Fixture-Role", out var role)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, User.ToString()), new(ClaimTypes.Role, role.ToString()) };
            if (Request.Headers.ContainsKey("X-Fixture-Permission")) claims.Add(new("permission", PlatformPermissions.SpeedReading.SettingsManage));
            if (Request.Headers.ContainsKey("X-Fixture-Mfa")) claims.Add(new("amr", "mfa"));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
    private sealed class MfaStore : IMfaPolicyStore
    {
        public Task<string> GetModeAsync(string category, CancellationToken token = default) => Task.FromResult(MfaPolicyModes.MutationsOnly);
    }
    public class UnusedDaily : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) => throw new InvalidOperationException("Unexpected daily service call");
    }
    private sealed class Progression : ISpeedReadingProgramProgression
    {
        public Guid LastStudent;
        public Task<ProgramProgressionSettings> GetSettingsAsync(CancellationToken token = default) => Task.FromResult(new ProgramProgressionSettings(false, null));
        public Task<ProgramProgressionSettings> UpdateSettingsAsync(bool allow, Guid actor, CancellationToken token = default) => Task.FromResult(new ProgramProgressionSettings(allow, DateTime.UtcNow));
        public Task<StudentProgressionPreference> GetPreferenceAsync(Guid user, CancellationToken token = default) => Task.FromResult(new StudentProgressionPreference("inherit", false, false));
        public Task<StudentProgressionPreference> UpdatePreferenceAsync(Guid user, string mode, Guid actor, CancellationToken token = default) => Task.FromResult(new StudentProgressionPreference(mode, true, false));
        public Task<ProgramDayAdvanceState> GetAdvanceStateAsync(Guid user, CancellationToken token = default) => Task.FromResult(new ProgramDayAdvanceState(null, 1, 2, true, true));
        public Task<ProgramDayAdvanceState> AdvanceAsync(Guid user, AdvanceProgramDayRequest request, CancellationToken token = default)
        {
            LastStudent = user;
            return GetAdvanceStateAsync(user, token);
        }
    }
}
