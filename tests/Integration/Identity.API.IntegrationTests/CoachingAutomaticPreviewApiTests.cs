using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAutomaticPreviewApiTests
{
    [Fact]
    public void Api_RequiresStudentExplicitVersionAndBoundedTraffic()
    {
        var type = typeof(AutomaticStudyPreviewController);
        Assert.Equal("Student", Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Roles);
        var method = type.GetMethod(nameof(AutomaticStudyPreviewController.Preview))!;
        Assert.Equal("study-planning-write", Assert.Single(method.GetCustomAttributes(typeof(EnableRateLimitingAttribute), false).Cast<EnableRateLimitingAttribute>()).PolicyName);
        var limit = Assert.Single(method.GetCustomAttributes(typeof(RequestSizeLimitAttribute), false).Cast<RequestSizeLimitAttribute>());
        Assert.Equal(65536, ((Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata)limit).MaxRequestBodySize);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AutomaticStudyPreviewRequest>("{}"));
    }

    [Fact]
    public async Task Api_ForwardsRequestAndCancellationAndReturnsPreview()
    {
        var stub = new Stub();
        var request = new AutomaticStudyPreviewRequest(new(2026, 10, 5), 7, 2, [new(Guid.NewGuid(), 30)]);
        using var cancellation = new CancellationTokenSource();
        var result = Assert.IsType<OkObjectResult>(await new AutomaticStudyPreviewController(stub).Preview(request, cancellation.Token));
        Assert.Equal(request, stub.Request);
        Assert.Equal(cancellation.Token, stub.Token);
        Assert.Contains("\"success\":true", JsonSerializer.Serialize(result.Value));
    }

    [Theory]
    [InlineData("forbidden", 403)]
    [InlineData("conflict", 409)]
    [InlineData("missing", 404)]
    [InlineData("invalid", 400)]
    public async Task Api_MapsExpectedErrorsWithoutLeakingValidationDetails(string failure, int status)
    {
        Exception exception = failure switch
        {
            "forbidden" => new BusinessRuleException("Authorization.Forbidden", "Denied"),
            "conflict" => new BusinessRuleException("StudyPlanning.Conflict", "Changed"),
            "missing" => new KeyNotFoundException(),
            _ => new ArgumentException("Internal query details")
        };
        var result = Assert.IsAssignableFrom<ObjectResult>(await new AutomaticStudyPreviewController(new Stub { Failure = exception })
            .Preview(new(new(2026, 10, 5), 7, 0, [new(Guid.NewGuid(), 30)])));
        Assert.Equal(status, result.StatusCode);
        Assert.DoesNotContain("Internal query details", JsonSerializer.Serialize(result.Value));
    }

    private sealed class Stub : IAutomaticStudyPlanPreviewService
    {
        public Exception? Failure { get; init; }
        public AutomaticStudyPreviewRequest? Request;
        public CancellationToken Token;
        public Task<AutomaticStudyPreview> PreviewAsync(AutomaticStudyPreviewRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            Token = cancellationToken;
            return Failure is null ? Task.FromResult(new AutomaticStudyPreview(2, "Europe/Istanbul", null, null, [],
                new StudyDraftSchedule([], [], 0, 0, 0, 0))) : Task.FromException<AutomaticStudyPreview>(Failure);
        }
    }
}
