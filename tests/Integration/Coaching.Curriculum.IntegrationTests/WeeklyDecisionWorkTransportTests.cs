using System.Net;
using System.Net.Http.Json;
using Coaching.Application.StudyPlanning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class WeeklyDecisionWorkTransportTests(DecisionTransportHost host) : IClassFixture<DecisionTransportHost>
{
    [Theory]
    [InlineData("links")]
    [InlineData("preview")]
    [InlineData("apply")]
    [InlineData("assignment")]
    [InlineData("deadline")]
    public async Task ValidDecisionRequestsReachTheActionThroughRealMvcValidation(string kind)
    {
        object body = kind switch
        {
            "links" => new DecisionLinksSave(0, null, null),
            "preview" => Proposal(),
            "apply" => new DecisionStudyApply(Proposal(), "preview", true, Guid.NewGuid()),
            "deadline" => new WeeklyDecisionPostpone(0,new(2026,10,28),"Geçerli tarih değişikliği"),
            _ => new DecisionAssignmentCreate(0, DateTime.UtcNow.AddDays(2), Guid.NewGuid(), 30)
        };
        using var response = await host.Client.PostAsJsonAsync("decision-transport/" + kind, body);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task NegativeVersionAndMissingRequiredLinkFieldsReturnBadRequest()
    {
        using var negative = await host.Client.PostAsJsonAsync("decision-transport/links", new DecisionLinksSave(-1, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        using var missing = await host.Client.PostAsJsonAsync("decision-transport/links", new { expectedVersion = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task AssignmentDurationValidationRunsOnTheRecordConstructorParameter()
    {
        using var response = await host.Client.PostAsJsonAsync("decision-transport/assignment",
            new DecisionAssignmentCreate(0, DateTime.UtcNow.AddDays(2), Guid.NewGuid(), 1441));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static DecisionStudyProposal Proposal() => new(0, 0, Guid.NewGuid(), null, new(2026, 10, 23), 600, 30);
    [Fact]
    public async Task DeadlineChangeRequiresExplicitVersionDateAndValidReasonThroughMvc()
    {
        object[] invalid=[new {dueOn="2026-10-28",reason="Geçerli gerekçe"},new {expectedVersion=0,reason="Geçerli gerekçe"},
            new WeeklyDecisionPostpone(-1,new(2026,10,28),"Geçerli gerekçe"),new WeeklyDecisionPostpone(0,new(2026,10,28),"kısa")];
        foreach(var body in invalid){using var response=await host.Client.PostAsJsonAsync("decision-transport/deadline",body);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);}
    }
}

public sealed class DecisionTransportHost : IAsyncLifetime
{
    private WebApplication? app;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(DecisionTransportController).Assembly);
        app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        Client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        if (app is not null) await app.DisposeAsync();
    }
}

[ApiController, Route("decision-transport")]
public sealed class DecisionTransportController : ControllerBase
{
    [HttpPost("links")] public IActionResult Links(DecisionLinksSave request) => NoContent();
    [HttpPost("preview")] public IActionResult Preview(DecisionStudyProposal request) => NoContent();
    [HttpPost("apply")] public IActionResult Apply(DecisionStudyApply request) => NoContent();
    [HttpPost("assignment")] public IActionResult Assignment(DecisionAssignmentCreate request) => NoContent();
    [HttpPost("deadline")] public IActionResult Deadline(WeeklyDecisionPostpone request)=>NoContent();
}
