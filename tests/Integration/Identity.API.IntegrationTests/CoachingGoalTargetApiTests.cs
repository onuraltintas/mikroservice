using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.API.IntegrationTests;

public sealed class CoachingGoalTargetApiTests
{
    [Fact]
    public async Task ScoreApi_RequiresExplicitFieldsAndUsesWriteProtections()
    {
        var method = typeof(GoalTargetController).GetMethod(nameof(GoalTargetController.ReplaceScore))!;
        Assert.Equal("study-planning-write", Assert.Single(method.GetCustomAttributes(typeof(EnableRateLimitingAttribute), false).Cast<EnableRateLimitingAttribute>()).PolicyName);
        Assert.Single(method.GetCustomAttributes(typeof(RequestSizeLimitAttribute), false));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GoalScoreTargetUpdate>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GoalScoreTargetUpdate>("{\"ExpectedVersion\":0}"));
        Assert.IsType<OkObjectResult>(await new GoalTargetController(new Stub()).ReplaceScore(Guid.NewGuid(), new(0, 400, 500, Coaching.Domain.Enums.ExamType.LGS)));
    }

    [Theory]
    [InlineData("forbidden", 403)]
    [InlineData("conflict", 409)]
    [InlineData("missing", 404)]
    [InlineData("invalid", 400)]
    public async Task ScoreApi_MapsExpectedFailures(string failure, int status)
    {
        Exception exception = failure switch
        {
            "forbidden" => new BusinessRuleException("Authorization.Forbidden", "Denied"),
            "conflict" => new BusinessRuleException("StudyPlanning.Conflict", "Changed"),
            "missing" => new KeyNotFoundException(),
            _ => new ArgumentException("Internal detail")
        };
        var result = Assert.IsAssignableFrom<ObjectResult>(await new GoalTargetController(new Stub { Failure = exception })
            .ReplaceScore(Guid.NewGuid(), new(0, 400, 500, Coaching.Domain.Enums.ExamType.LGS)));
        Assert.Equal(status, result.StatusCode);
        Assert.DoesNotContain("Internal detail", JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public void Api_RequiresStudentVersionAndLimitsWriteBodyAndTraffic()
    {
        var type = typeof(GoalTargetController);
        Assert.Equal("Student", Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Roles);
        var method = type.GetMethod(nameof(GoalTargetController.Replace))!;
        Assert.Equal("study-planning-write", Assert.Single(method.GetCustomAttributes(typeof(EnableRateLimitingAttribute), false).Cast<EnableRateLimitingAttribute>()).PolicyName);
        var limit = Assert.Single(method.GetCustomAttributes(typeof(RequestSizeLimitAttribute), false).Cast<RequestSizeLimitAttribute>());
        Assert.Equal(1024, ((Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata)limit).MaxRequestBodySize);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GoalTargetUpdate>("{}"));
    }

    [Fact]
    public async Task Api_ReturnsOwnedTargetAndForwardsReplacement()
    {
        var stub = new Stub();
        var controller = new GoalTargetController(stub);
        var id = Guid.NewGuid();
        Assert.IsType<OkObjectResult>(await controller.Get(id));
        var update = new GoalTargetUpdate(2, null, Guid.NewGuid());
        Assert.IsType<OkObjectResult>(await controller.Replace(id, update));
        Assert.Equal((id, update), stub.Request);
        Assert.IsType<NotFoundObjectResult>(await new GoalTargetController(new Stub { Missing = true }).Get(id));
    }

    [Theory]
    [InlineData("forbidden", 403)]
    [InlineData("conflict", 409)]
    [InlineData("missing", 404)]
    [InlineData("invalid", 400)]
    public async Task Api_MapsExpectedFailures(string failure, int status)
    {
        Exception exception = failure switch
        {
            "forbidden" => new BusinessRuleException("Authorization.Forbidden", "Denied"),
            "conflict" => new BusinessRuleException("StudyPlanning.Conflict", "Changed"),
            "missing" => new KeyNotFoundException(),
            _ => new ArgumentException()
        };
        var result = Assert.IsAssignableFrom<ObjectResult>(await new GoalTargetController(new Stub { Failure = exception })
            .Replace(Guid.NewGuid(), new GoalTargetUpdate(0, null, null)));
        Assert.Equal(status, result.StatusCode);
    }

    private sealed class Stub : IGoalTargetService
    {
        public bool Missing { get; init; }
        public Exception? Failure { get; init; }
        public (Guid, GoalTargetUpdate) Request;
        public Task<GoalTargetView?> GetAsync(Guid goalId, CancellationToken cancellationToken = default)
            => Task.FromResult<GoalTargetView?>(Missing ? null : new(goalId, 0, null, null, true));
        public Task<GoalTargetView> ReplaceAsync(Guid goalId, GoalTargetUpdate request, CancellationToken cancellationToken = default)
        {
            Request = (goalId, request);
            return Failure is null ? Task.FromResult(new GoalTargetView(goalId, request.ExpectedVersion + 1,
                request.TargetUniversityProgramId, request.TargetSchoolId, true)) : Task.FromException<GoalTargetView>(Failure);
        }
        public Task<GoalTargetView> ReplaceScoreAsync(Guid goalId, GoalScoreTargetUpdate request, CancellationToken cancellationToken = default)
            => Failure is null ? Task.FromResult(new GoalTargetView(goalId, request.ExpectedVersion + 1, null, null, true,
                ScoreTarget: new(request.TargetScore, request.MaxScore, request.ExamType, null))) : Task.FromException<GoalTargetView>(Failure);
    }
}
