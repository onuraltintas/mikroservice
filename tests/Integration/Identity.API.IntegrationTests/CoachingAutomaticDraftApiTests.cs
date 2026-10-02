using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAutomaticDraftApiTests
{
    [Fact]
    public async Task Api_RequiresSnapshotAndReturnsDraftLocation()
    {
        Assert.Equal("Student", Assert.Single(typeof(AutomaticStudyDraftController).GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Roles);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AutomaticStudyDraftRequest>("{\"Title\":\"Plan\"}"));
        var request = new AutomaticStudyDraftRequest("Plan", new(new(2026, 10, 5), 7, 0, [new(Guid.NewGuid(), 30)]), null, null);
        var stub = new Stub();
        var result = Assert.IsType<CreatedAtActionResult>(await new AutomaticStudyDraftController(stub).Create(request));
        Assert.Equal(request, stub.Request);
        Assert.Equal(nameof(ManualStudyPlansController.Get), result.ActionName);
        Assert.Equal("ManualStudyPlans", result.ControllerName);
    }
    [Theory]
    [InlineData("conflict", 409)]
    [InlineData("invalid", 400)]
    [InlineData("missing", 404)]
    [InlineData("forbidden", 403)]
    public async Task Api_MapsFailures(string failure, int status)
    {
        Exception error = failure switch {
            "conflict" => new BusinessRuleException("StudyPlanning.Conflict", "Changed"),
            "forbidden" => new BusinessRuleException("Authorization.Forbidden", "Denied"),
            "missing" => new KeyNotFoundException(), _ => new ArgumentException() };
        var result = Assert.IsAssignableFrom<ObjectResult>(await new AutomaticStudyDraftController(new Stub { Failure = error })
            .Create(new("Plan", new(new(2026, 10, 5), 7, 0, []), null, null)));
        Assert.Equal(status, result.StatusCode);
    }
    private sealed class Stub : IAutomaticStudyPlanDraftService
    {
        public AutomaticStudyDraftRequest? Request;
        public Exception? Failure;
        public Task<ManualStudyPlanView> CreateAutomaticDraftAsync(AutomaticStudyDraftRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Failure is null ? Task.FromResult(new ManualStudyPlanView(Guid.NewGuid(), 0, request.Title, StudyPlanStatus.Draft, []))
                : Task.FromException<ManualStudyPlanView>(Failure);
        }
    }
}
