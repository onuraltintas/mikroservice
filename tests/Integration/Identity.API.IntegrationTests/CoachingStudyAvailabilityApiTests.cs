using Coaching.API.Controllers;
using Coaching.Application.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingStudyAvailabilityApiTests
{
    [Fact]
    public async Task Api_ReturnsNotFoundForMissingPreferencesAndOkForReplacement()
    {
        var controller = new StudyPlanningController(new Stub());
        Assert.IsType<NotFoundObjectResult>(await controller.GetAvailability());
        Assert.IsType<OkObjectResult>(await controller.ReplaceAvailability(new(null, "Europe/Istanbul", [])));
    }

    [Theory]
    [InlineData("Authorization.Forbidden", 403)]
    [InlineData("StudyPlanning.Conflict", 409)]
    public async Task Api_MapsBusinessErrorsToUserFriendlyStatus(string code, int status)
    {
        var controller = new StudyPlanningController(new Stub { Failure = new BusinessRuleException(code, "Message") });
        var result = Assert.IsAssignableFrom<ObjectResult>(await controller.ReplaceAvailability(new(null, "Europe/Istanbul", [])));
        Assert.Equal(status, result.StatusCode);
    }

    [Fact]
    public async Task Api_Returns400ForInvalidTimeZoneAndRequiresStudentRole()
    {
        var controller = new StudyPlanningController(new Stub { Failure = new TimeZoneNotFoundException() });
        Assert.IsType<BadRequestObjectResult>(await controller.ReplaceAvailability(new(null, "wrong", [])));
        var auth = Assert.Single(typeof(StudyPlanningController).GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>());
        Assert.Equal("Student", auth.Roles);
    }

    private sealed class Stub : IStudyAvailabilityService
    {
        public Exception? Failure { get; init; }
        public Task<StudyAvailabilityView?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<StudyAvailabilityView?>(null);
        public Task<StudyAvailabilityView> ReplaceAsync(StudyAvailabilityUpdate request, CancellationToken cancellationToken = default)
            => Failure is null ? Task.FromResult(new StudyAvailabilityView(0, request.TimeZoneId, request.Windows))
                : Task.FromException<StudyAvailabilityView>(Failure);
    }
}
