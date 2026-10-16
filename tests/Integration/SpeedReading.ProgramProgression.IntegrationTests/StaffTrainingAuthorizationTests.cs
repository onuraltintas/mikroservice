using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpeedReading.API.Controllers;
using SpeedReading.Application.Content;
using SpeedReading.Application.StudentProgram;

namespace SpeedReading.ProgramProgression.IntegrationTests;

public sealed class StaffTrainingAuthorizationTests
{
    [Theory]
    [InlineData("", 401)]
    [InlineData("Editor", 200)]
    [InlineData("Teacher", 200)]
    [InlineData("Admin", 200)]
    [InlineData("SystemAdmin", 200)]
    [InlineData("Student", 403)]
    [InlineData("Editor,Student", 403)]
    [InlineData("InstitutionAdmin", 403)]
    public async Task Training_catalog_and_enrollment_enforce_role_and_use_the_authenticated_actor(string roles, int expected)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddLogging(log => log.ClearProviders());
        builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, Identity>("Fixture", _ => { });
        builder.Services.AddAuthorization();
        var programs = DispatchProxy.Create<ISpeedReadingStudentProgram, Programs>();
        builder.Services.AddSingleton(programs);
        builder.Services.AddSingleton(DispatchProxy.Create<ILegacySpeedReadingPrograms, Catalog>());
        builder.Services.AddControllers().AddApplicationPart(typeof(StaffTrainingController).Assembly);
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = app.GetTestClient();
        if (roles.Length > 0) client.DefaultRequestHeaders.Add("X-Fixture-Roles", roles);
        Assert.Equal((HttpStatusCode)expected, (await client.GetAsync("/api/speed-reading/staff-training/programs")).StatusCode);
        var response = await client.PostAsJsonAsync("/api/speed-reading/staff-training/start",
            new { templateId = Guid.NewGuid(), userId = Guid.NewGuid() });
        Assert.Equal((HttpStatusCode)expected, response.StatusCode);
        Assert.Equal(expected == 200 ? Identity.Actor : null, ((Programs)(object)programs).LastActor);
    }

    private sealed class Identity(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        internal static readonly Guid Actor = Guid.NewGuid();
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Fixture-Roles", out var roles)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Actor.ToString()) };
            claims.AddRange(roles.ToString().Split(',').Select(role => new Claim(ClaimTypes.Role, role)));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
    public class Programs : DispatchProxy
    {
        public Guid? LastActor;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name != nameof(ISpeedReadingStudentProgram.StartStaffTrainingAsync)) throw new InvalidOperationException("Unexpected operation");
            LastActor = (Guid)args![0]!;
            return Task.FromResult(new StartStudentProgramResult(true, Guid.NewGuid(), "Eğitim", "Başlatıldı"));
        }
    }
    public class Catalog : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == nameof(ILegacySpeedReadingPrograms.GetProgramTemplatesAsync)
            ? Task.FromResult<IReadOnlyList<ExerciseProgramTemplateSummary>>([]) : throw new InvalidOperationException("Unexpected operation");
    }
}
