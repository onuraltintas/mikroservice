using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class StudentDailyAgendaTransportTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task RealHttpRouteAuthorizesStudentBindsFiltersAndSerializesExactDestination()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var student = Guid.NewGuid();
        pg.Data.User.UserId = student; pg.Data.User.Roles = ["Student"]; pg.Db.Add(CoachingStudentProfile.Create(student));
        var assignment = Assignment.Create(Guid.NewGuid(), "HTTP günlük iş", DateTime.UtcNow.AddDays(1));
        assignment.AssignToStudent(student); pg.Db.Add(assignment); await pg.Db.SaveChangesAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(pg.Db).AddSingleton<ICoachingAccessPolicy>(new CoachingAccessPolicy(pg.Data.User)).AddSingleton(TimeProvider.System);
        builder.Services.AddAuthentication("AgendaTest").AddScheme<AuthenticationSchemeOptions, AgendaTestAuthentication>("AgendaTest", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(StudentDailyAgendaController).Assembly);
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) }; const string route = "api/coaching/study-planning/daily-agenda";
        using var anonymous = await client.GetAsync(route); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Teacher");
        using var teacher = await client.GetAsync(route); Assert.Equal(HttpStatusCode.Forbidden, teacher.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Student");
        using var valid = await client.GetAsync(route + "?mode=upcoming&pageNumber=1&studentId=" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        var page = await valid.Content.ReadFromJsonAsync<StudentDailyAgendaPage>(); Assert.Equal(assignment.Id, Assert.Single(page!.Items).RecordId);
        Assert.Equal("assignment", page.Items[0].Kind); Assert.Equal(1, page.UpcomingCount);
        foreach (var query in new[] { "mode=unknown", "pageNumber=0", "pageNumber=invalid", "pageNumber=1001" })
        {
            using var invalid = await client.GetAsync(route + "?" + query); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
    }
}

public sealed class AgendaTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].ToString();
        return Task.FromResult(string.IsNullOrEmpty(role) ? AuthenticateResult.NoResult() : AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], Scheme.Name)), Scheme.Name)));
    }
}
