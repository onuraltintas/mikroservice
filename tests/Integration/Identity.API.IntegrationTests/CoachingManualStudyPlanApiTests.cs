using Coaching.API.Controllers;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Identity.API.IntegrationTests;

public sealed class CoachingManualStudyPlanApiTests
{
    [Fact]
    public void Json_RequiresExplicitVersionAndBodyLimitFitsMaximumPlan()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StudyPlanPublishInput>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ManualStudyPlanUpdate>("{\"Plan\":{\"Title\":\"Plan\",\"Tasks\":[]}}"));
        foreach (var name in new[] { nameof(ManualStudyPlansController.Create), nameof(ManualStudyPlansController.Replace) })
        {
            var limit = Assert.Single(typeof(ManualStudyPlansController).GetMethod(name)!
                .GetCustomAttributes(typeof(RequestSizeLimitAttribute), false).Cast<RequestSizeLimitAttribute>());
            Assert.Equal(1024 * 1024, ((Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata)limit).MaxRequestBodySize);
        }
    }

    [Fact]
    public async Task Api_Returns201WithLocationAndRequiresStudent()
    {
        var controller = new ManualStudyPlansController(new Stub());
        var result = Assert.IsType<CreatedAtActionResult>(await controller.Create(new("Plan", [])));
        Assert.Equal(nameof(ManualStudyPlansController.Get), result.ActionName);
        Assert.Equal("Student", Assert.Single(typeof(ManualStudyPlansController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Roles);
    }

    [Theory]
    [InlineData("Authorization.Forbidden", 403)]
    [InlineData("StudyPlanning.NotFound", 404)]
    [InlineData("StudyPlanning.Conflict", 409)]
    public async Task Api_ReturnsExpectedErrors(string code, int status)
    {
        var controller = new ManualStudyPlansController(new Stub { Failure = new BusinessRuleException(code, "Message") });
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(await controller.Publish(Guid.NewGuid(), new(0))).StatusCode);
    }

    [Fact]
    public async Task Api_InvalidInputReturns400AndMissingPlanReturns404()
    {
        Assert.IsType<NotFoundObjectResult>(await new ManualStudyPlansController(new Stub()).Get(Guid.NewGuid()));
        var controller = new ManualStudyPlansController(new Stub { Failure = new ArgumentException() });
        Assert.IsType<BadRequestObjectResult>(await controller.Replace(Guid.NewGuid(), new(0, new("Plan", []))));
    }

    [Fact]
    public async Task Api_ListsPlansAndChangesTasksWithBoundedAuthenticatedRequests()
    {
        var controller = new ManualStudyPlansController(new Stub());
        Assert.IsType<OkObjectResult>(await controller.List(1, 20, StudyPlanStatus.Active));
        Assert.IsType<OkObjectResult>(await controller.Complete(Guid.NewGuid(), Guid.NewGuid(), new(0, 30)));
        Assert.IsType<OkObjectResult>(await controller.Reschedule(Guid.NewGuid(), Guid.NewGuid(), new(0, new DateOnly(2026, 10, 6))));
        Assert.IsType<OkObjectResult>(await controller.RemainingScope(Guid.NewGuid(), Guid.NewGuid(), new(0, 25, 6), default));
        Assert.IsType<OkObjectResult>(await controller.Progress(Guid.NewGuid(), Guid.NewGuid(), new(0, 15, 5, false), default));
        Assert.IsType<OkObjectResult>(await controller.CorrectRealization(Guid.NewGuid(), Guid.NewGuid(), new(0, 10, 4, false, "Yanlış süre girildi"), default));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StudyRealizationInput>("{\"ActualMinutes\":15,\"IsCompleted\":false}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StudyTaskCompleteInput>("{\"ActualMinutes\":30}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StudyTaskRescheduleInput>("{\"PlannedDate\":\"2026-10-06\"}"));
        foreach (var name in new[] { nameof(ManualStudyPlansController.Complete), nameof(ManualStudyPlansController.Progress), nameof(ManualStudyPlansController.RemainingScope), nameof(ManualStudyPlansController.CorrectRealization), nameof(ManualStudyPlansController.Reschedule) })
        {
            var method = typeof(ManualStudyPlansController).GetMethod(name)!;
            Assert.Single(method.GetCustomAttributes(typeof(RequestSizeLimitAttribute), false));
            Assert.Single(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), false));
        }
        var denied = new ManualStudyPlansController(new Stub { Failure = new BusinessRuleException("StudyPlanning.Conflict", "Changed") });
        Assert.IsType<ConflictObjectResult>(await denied.Complete(Guid.NewGuid(), Guid.NewGuid(), new(0, 30)));
    }

    private sealed class Stub : IManualStudyPlanService
    {
        public Task<ManualStudyPlanView> SaveCalendarTaskAsync(Guid id,Guid? taskId,CalendarTaskSaveInput request,CancellationToken ct=default)=>Result();
        public Task<ManualStudyPlanView> DeleteCalendarTaskAsync(Guid id,Guid taskId,int expectedVersion,CancellationToken ct=default)=>Result();
        public Task<ManualStudyPlanView> SetRemainingScopeAsync(Guid id,Guid taskId,StudyRemainingScopeInput request,CancellationToken ct=default)=>Result();
        public Task<ManualStudyPlanView> RecordRealizationAsync(Guid id,Guid taskId,StudyRealizationInput request,bool correction,CancellationToken ct=default)=>Result();
        public Exception? Failure { get; init; }
        public Task<StudyPlanPage> ListAsync(int pageNumber, int pageSize, StudyPlanStatus? status, CancellationToken cancellationToken = default)
            => Task.FromResult(new StudyPlanPage([], 0, pageNumber, pageSize));
        public Task<ManualStudyPlanView> CompleteTaskAsync(Guid id, Guid taskId, int expectedVersion, int actualMinutes, CancellationToken cancellationToken = default) => Result();
        public Task<ManualStudyPlanView> CompleteTaskAsync(Guid id, Guid taskId, int expectedVersion, int actualMinutes, int? actualQuestions, CancellationToken cancellationToken = default) => Result();
        public Task<ManualStudyPlanView> RescheduleTaskAsync(Guid id, Guid taskId, int expectedVersion, DateOnly plannedDate, CancellationToken cancellationToken = default) => Result();
        public Task<ManualStudyPlanView> MoveTasksAsync(Guid id, StudyTasksMoveInput request, CancellationToken cancellationToken = default) => Result();
        public Task<ManualStudyPlanView?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ManualStudyPlanView?>(null);
        public Task<ManualStudyPlanView> CreateDraftAsync(ManualStudyPlanInput request, CancellationToken cancellationToken = default) => Result();
        public Task<ManualStudyPlanView> ReplaceDraftAsync(Guid id, int expectedVersion, ManualStudyPlanInput request, CancellationToken cancellationToken = default) => Result();
        public Task<ManualStudyPlanView> PublishAsync(Guid id, int expectedVersion, CancellationToken cancellationToken = default) => Result();
        public Task<ManualStudyPlanView> RestoreArchivedAsync(Guid id,StudyPlanRestoreInput request,CancellationToken cancellationToken=default)=>Result();
        public Task<ManualStudyPlanView> ArchiveDraftAsync(Guid id, int expectedVersion, CancellationToken cancellationToken = default) => Result();
        private Task<ManualStudyPlanView> Result() => Failure is null
            ? Task.FromResult(new ManualStudyPlanView(Guid.NewGuid(), 0, "Plan", StudyPlanStatus.Draft, []))
            : Task.FromException<ManualStudyPlanView>(Failure);
    }
}
