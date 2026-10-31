using System.Net;
using System.Net.Http.Json;
using Coaching.Application.Interfaces;
using Coaching.Infrastructure.ExternalServices;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;

namespace Identity.API.IntegrationTests;

// V3 (parent panel): the weekly parent e-mail reads the child's speed-reading week from the speed-reading service.
public sealed class SpeedReadingParentSummaryClientTests
{
    [Fact]
    public async Task ReadsTheWeekWithTheServiceKeyAndTheStudentsWeekStart()
    {
        var student = Guid.NewGuid();
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal($"https://speed.example/api/internal/speed-reading/parent-weekly-summary?studentUserId={student}&weekStart=2026-11-02",
                request.RequestUri!.ToString());
            Assert.Equal("test-service-key", Assert.Single(request.Headers.GetValues(InternalServiceAuthentication.HeaderName)));
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new
            {
                minutes = 95, activeDays = 4, exercisesCompleted = 12, latestWpm = 260.5, wpmChange = -4, averageComprehension = 78.5,
                programDay = 9, programTotalDays = 21, badgesEarnedThisWeek = 1
            }) };
        }));
        var week = await new SpeedReadingParentSummaryClient(http, Configuration("https://speed.example/")).GetWeekAsync(student, new DateOnly(2026, 11, 2), default);
        Assert.Equal(new SpeedReadingParentWeek(95, 4, 12, 260.5m, -4m, 78.5m, 9, 21, 1), week);
    }

    [Fact]
    public async Task NoActivityIsNullAndAnOutageIsAnError()
    {
        using var none = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.NoContent)));
        Assert.Null(await new SpeedReadingParentSummaryClient(none, Configuration("https://speed.example")).GetWeekAsync(Guid.NewGuid(), new DateOnly(2026, 11, 2), default));
        using var nulls = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
        {
            minutes = 0, activeDays = 0, exercisesCompleted = 0, latestWpm = (decimal?)null, wpmChange = (decimal?)null,
            averageComprehension = (decimal?)null, programDay = (int?)null, programTotalDays = (int?)null, badgesEarnedThisWeek = 0
        }) }));
        Assert.Equal(new SpeedReadingParentWeek(0, 0, 0, null, null, null, null, null, 0),
            await new SpeedReadingParentSummaryClient(nulls, Configuration("https://speed.example")).GetWeekAsync(Guid.NewGuid(), new DateOnly(2026, 11, 2), default));
        using var down = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new SpeedReadingParentSummaryClient(down, Configuration("https://speed.example")).GetWeekAsync(Guid.NewGuid(), new DateOnly(2026, 11, 2), default));
    }

    // Speed reading is optional for a deployment: without Services:SpeedReadingService the section is simply left out.
    [Fact]
    public async Task WithoutTheServiceUrlNothingIsAsked()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(_ => { calls++; return new HttpResponseMessage(HttpStatusCode.OK); }));
        Assert.Null(await new SpeedReadingParentSummaryClient(http, Configuration(null)).GetWeekAsync(Guid.NewGuid(), new DateOnly(2026, 11, 2), default));
        Assert.Equal(0, calls);
    }

    private static IConfiguration Configuration(string? url) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["Services:SpeedReadingService"] = url, ["Internal:ServiceApiKey"] = "test-service-key" }).Build();
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(respond(request)); }
}
