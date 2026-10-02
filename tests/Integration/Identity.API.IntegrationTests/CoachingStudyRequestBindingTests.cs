using Coaching.Application.StudyPlanning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

public sealed class CoachingStudyRequestBindingTests
{
    public static IEnumerable<object[]> Requests()
    {
        var plan = new ManualStudyPlanInput("Plan", [new(new(2026, 10, 2), "Review", 30, null, false)]);
        yield return [plan]; yield return [new ManualStudyPlanUpdate(0, plan)];
        yield return [new StudyPlanPublishInput(0)]; yield return [new StudyTaskCompleteInput(0, 25)];
        yield return [new StudyTaskRescheduleInput(0, new(2026, 10, 3))];
        yield return [new StudyAvailabilityUpdate(null, "Europe/Istanbul", [new(DayOfWeek.Monday, 600, 660)])];
        yield return [new AutomaticStudyPreviewRequest(new(2026, 10, 2), 7, 0, [new(Guid.NewGuid(), 30)])];
        yield return [new GoalTargetUpdate(0, null, Guid.NewGuid())];
        yield return [new GoalScoreTargetUpdate(0, 400, 500, Coaching.Domain.Enums.ExamType.LGS)];
    }
    [Theory]
    [MemberData(nameof(Requests))]
    public void ValidPlanningRequests_ValidateThroughMvcWithoutMetadataCrashes(object request)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var context = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        provider.GetRequiredService<IObjectModelValidator>().Validate(context, null, string.Empty, request);
        Assert.True(context.ModelState.IsValid);
    }
}
