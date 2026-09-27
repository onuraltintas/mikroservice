using System.Reflection;
using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Commands.CreateAssignment;
using Coaching.Application.Commands.CreateSession;
using Coaching.Application.Commands.CreateExam;
using Coaching.Application.Commands.CreateGoal;
using Coaching.Application.Commands.UpdateGoalProgress;
using Coaching.Application.Commands.UpdateGoal;
using Coaching.Application.Commands.DeleteGoal;
using Coaching.Domain.Enums;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAdminAssignmentManagementTests
{
    [Fact]
    public async Task InstitutionAdmin_CreateAssignmentUsesAuthenticatedInstitutionAndValidatesTargets()
    {
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var (mediator, capture) = CapturingMediator.Create();
        var identity = new StubIdentityAuthorizationClient(institutionId);
        var controller = CreateController(
            mediator,
            new CoachingAdminScope(false, institutionId, [studentId]),
            identity);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Request.Headers["Idempotency-Key"] = "request-1";

        var result = await controller.CreateAssignment(new CreateAssignmentCommand
        {
            TeacherId = teacherId,
            InstitutionId = Guid.NewGuid(),
            StudentIds = [studentId],
            Title = "Ödev",
            DueDate = DateTime.UtcNow.AddDays(2)
        }, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.CreatedAtActionResult>();
        var dispatched = capture.Requests.OfType<CreateAssignmentCommand>().Should().ContainSingle().Which;
        dispatched.InstitutionId.Should().Be(institutionId);
        dispatched.IsInstitutionAdminOperation.Should().BeTrue();
        dispatched.IdempotencyKey.Should().Be("request-1");
        identity.TargetRequest.Should().NotBeNull();
        identity.TargetRequest!.Value.TeacherId.Should().Be(teacherId);
        identity.TargetRequest.Value.StudentIds.Should().Equal(studentId);
        identity.TargetRequest.Value.InstitutionId.Should().Be(institutionId);
        identity.TargetRequest.Value.IsSystemAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task InstitutionAdmin_CreateAssignmentRejectsTargetsOutsideAuthenticatedInstitution()
    {
        var institutionId = Guid.NewGuid();
        var (mediator, capture) = CapturingMediator.Create();
        var identity = new StubIdentityAuthorizationClient(null);
        var controller = CreateController(
            mediator,
            new CoachingAdminScope(false, institutionId, []),
            identity);

        var result = await controller.CreateAssignment(new CreateAssignmentCommand
        {
            TeacherId = Guid.NewGuid(),
            StudentIds = [Guid.NewGuid()],
            Title = "Ödev",
            DueDate = DateTime.UtcNow.AddDays(2)
        }, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ForbidResult>();
        capture.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task InstitutionAdmin_CannotCancelAnAssignmentOutsideTheAuthenticatedInstitution()
    {
        var institutionId = Guid.NewGuid();
        var mediator = CapturingMediator.Create();
        var controller = CreateController(
            mediator.Mediator,
            new CoachingAdminScope(false, institutionId, []),
            new StubIdentityAuthorizationClient(institutionId));

        var result = await controller.CancelAssignment(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
        mediator.Capture.Requests.Should().ContainSingle().Which.Should().BeOfType<Coaching.Application.Queries.GetAssignment.GetAssignmentQuery>()
            .Which.InstitutionId.Should().Be(institutionId);
        mediator.Capture.Requests.Should().NotContain(request => request is Coaching.Application.Commands.CancelAssignment.CancelAssignmentCommand);
    }

    [Fact]
    public async Task InstitutionAdmin_CreateSessionUsesAuthenticatedInstitutionAndValidatesTargets()
    {
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var (mediator, capture) = CapturingMediator.Create();
        var identity = new StubIdentityAuthorizationClient(institutionId);
        var controller = CreateController(mediator, new CoachingAdminScope(false, institutionId, [studentId]), identity);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Request.Headers["Idempotency-Key"] = "session-1";

        var result = await controller.CreateSession(new CreateSessionCommand(
            teacherId,
            studentId,
            DateTime.UtcNow.AddDays(1),
            45,
            "Rehberlik",
            null,
            SessionType.OneOnOne), CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        var dispatched = capture.Requests.OfType<CreateSessionCommand>().Should().ContainSingle().Which;
        dispatched.InstitutionId.Should().Be(institutionId);
        dispatched.IsInstitutionAdminOperation.Should().BeTrue();
        dispatched.IdempotencyKey.Should().Be("session-1");
        identity.TargetRequest.Should().NotBeNull();
        identity.TargetRequest!.Value.StudentIds.Should().Equal(studentId);
        identity.TargetRequest.Value.InstitutionId.Should().Be(institutionId);
    }

    [Fact]
    public async Task InstitutionAdmin_CannotCancelASessionOutsideTheAuthenticatedInstitution()
    {
        var institutionId = Guid.NewGuid();
        var mediator = CapturingMediator.Create();
        var controller = CreateController(
            mediator.Mediator,
            new CoachingAdminScope(false, institutionId, []),
            new StubIdentityAuthorizationClient(institutionId));

        var result = await controller.CancelSession(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
        mediator.Capture.Requests.Should().ContainSingle().Which.Should().BeOfType<Coaching.Application.Queries.GetCoachingAdminSession.GetCoachingAdminSessionQuery>()
            .Which.InstitutionId.Should().Be(institutionId);
        mediator.Capture.Requests.Should().NotContain(request => request is Coaching.Application.Commands.DeleteSession.CancelSessionCommand);
    }

    [Fact]
    public async Task InstitutionAdmin_CreateExamUsesAuthenticatedInstitutionAndValidatesTeacher()
    {
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var (mediator, capture) = CapturingMediator.Create();
        var identity = new StubIdentityAuthorizationClient(institutionId);
        var controller = CreateController(mediator, new CoachingAdminScope(false, institutionId, []), identity);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Request.Headers["Idempotency-Key"] = "exam-1";

        var result = await controller.CreateExam(new CreateExamCommand(
            teacherId,
            "Ara Sınavı",
            ExamType.Mock,
            DateTime.UtcNow,
            100,
            Guid.NewGuid(),
            "Deneme"), CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        var dispatched = capture.Requests.OfType<CreateExamCommand>().Should().ContainSingle().Which;
        dispatched.InstitutionId.Should().Be(institutionId);
        dispatched.IsInstitutionAdminOperation.Should().BeTrue();
        identity.TargetRequest.Should().NotBeNull();
        identity.TargetRequest!.Value.TeacherId.Should().Be(teacherId);
        identity.TargetRequest.Value.StudentIds.Should().BeEmpty();
        identity.TargetRequest.Value.InstitutionId.Should().Be(institutionId);
    }

    [Fact]
    public async Task InstitutionAdmin_CannotDeleteAnExamOutsideTheAuthenticatedInstitution()
    {
        var institutionId = Guid.NewGuid();
        var mediator = CapturingMediator.Create();
        var controller = CreateController(
            mediator.Mediator,
            new CoachingAdminScope(false, institutionId, []),
            new StubIdentityAuthorizationClient(institutionId));

        var result = await controller.DeleteExam(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
        mediator.Capture.Requests.Should().ContainSingle().Which.Should().BeOfType<Coaching.Application.Queries.GetCoachingAdminExam.GetCoachingAdminExamQuery>()
            .Which.InstitutionId.Should().Be(institutionId);
        mediator.Capture.Requests.Should().NotContain(request => request is Coaching.Application.Commands.DeleteExam.DeleteExamCommand);
    }

    [Fact]
    public async Task InstitutionAdmin_CreateGoalRequiresInstitutionStudentAndValidatesOptionalTeacher()
    {
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var (mediator, capture) = CapturingMediator.Create();
        var identity = new StubIdentityAuthorizationClient(institutionId);
        var controller = CreateController(mediator, new CoachingAdminScope(false, institutionId, [studentId]), identity);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Request.Headers["Idempotency-Key"] = "goal-request-1";

        var result = await controller.CreateGoal(new CreateGoalCommand(
            studentId,
            "Matematik hedefi",
            GoalCategory.SubjectMastery,
            teacherId,
            null,
            null,
            null), CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        var dispatched = capture.Requests.OfType<CreateGoalCommand>().Should().ContainSingle().Which;
        dispatched.IsInstitutionAdminOperation.Should().BeTrue();
        dispatched.InstitutionId.Should().Be(institutionId);
        dispatched.IdempotencyKey.Should().Be("goal-request-1");
        identity.TargetRequest.Should().NotBeNull();
        identity.TargetRequest!.Value.TeacherId.Should().Be(teacherId);
        identity.TargetRequest.Value.StudentIds.Should().Equal(studentId);
        identity.TargetRequest.Value.InstitutionId.Should().Be(institutionId);
    }

    [Fact]
    public async Task InstitutionAdmin_CannotCreateGoalForStudentOutsideCurrentInstitution()
    {
        var (mediator, capture) = CapturingMediator.Create();
        var controller = CreateController(
            mediator,
            new CoachingAdminScope(false, Guid.NewGuid(), []),
            new StubIdentityAuthorizationClient(null));
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.CreateGoal(new CreateGoalCommand(
            Guid.NewGuid(),
            "Hedef",
            GoalCategory.SubjectMastery,
            null,
            null,
            null,
            null), CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ForbidResult>();
        capture.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task InstitutionAdmin_CanCreateUnassignedGoalForInstitutionStudent()
    {
        var institutionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var (mediator, capture) = CapturingMediator.Create();
        var controller = CreateController(
            mediator,
            new CoachingAdminScope(false, institutionId, [studentId]),
            new StubIdentityAuthorizationClient(null));
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Request.Headers["Idempotency-Key"] = "unassigned-goal-1";

        var result = await controller.CreateGoal(new CreateGoalCommand(
            studentId,
            "Öğrenci çalışma hedefi",
            GoalCategory.StudyHabits,
            null,
            null,
            null,
            null), CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        var dispatched = capture.Requests.OfType<CreateGoalCommand>().Should().ContainSingle().Which;
        dispatched.IsInstitutionAdminOperation.Should().BeTrue();
        dispatched.InstitutionId.Should().Be(institutionId);
    }

    [Fact]
    public async Task InstitutionAdmin_CannotUpdateGoalOutsideCurrentInstitution()
    {
        var institutionId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var (mediator, capture) = CapturingMediator.Create();
        var controller = CreateController(
            mediator,
            new CoachingAdminScope(false, institutionId, [Guid.NewGuid()]),
            new StubIdentityAuthorizationClient(institutionId));

        var result = await controller.UpdateGoalProgress(
            goalId,
            new UpdateGoalProgressCommand(goalId, 50),
            CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
        capture.Requests.Should().ContainSingle()
            .Which.Should().BeOfType<Coaching.Application.Queries.GetCoachingAdminGoal.GetCoachingAdminGoalQuery>()
            .Which.InstitutionId.Should().Be(institutionId);
        capture.Requests.Should().NotContain(request => request is UpdateGoalProgressCommand);
    }

    [Fact]
    public async Task InstitutionAdmin_CannotDeleteGoalOutsideCurrentInstitution()
    {
        var institutionId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var mediator = CapturingMediator.Create();
        var controller = CreateController(
            mediator.Mediator,
            new CoachingAdminScope(false, institutionId, [Guid.NewGuid()]),
            new StubIdentityAuthorizationClient(institutionId));

        var result = await controller.DeleteGoal(goalId, CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();
        mediator.Capture.Requests.Should().ContainSingle()
            .Which.Should().BeOfType<Coaching.Application.Queries.GetCoachingAdminGoal.GetCoachingAdminGoalQuery>()
            .Which.InstitutionId.Should().Be(institutionId);
        mediator.Capture.Requests.Should().NotContain(request => request is DeleteGoalCommand);
    }

    [Fact]
    public async Task InstitutionAdmin_UpdatesScopedGoalUsingInternalOperationFlag()
    {
        var institutionId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var mediator = CapturingMediator.Create();
        mediator.Capture.GoalToReturn = new Coaching.Application.Queries.GetCoachingAdminGoal.CoachingAdminGoalDetailDto(
            goalId, studentId, teacherId, "Hedef", null, GoalCategory.SubjectMastery,
            null, null, null, null, 0, false, DateTime.UtcNow);
        var controller = CreateController(
            mediator.Mediator,
            new CoachingAdminScope(false, institutionId, [studentId]),
            new StubIdentityAuthorizationClient(institutionId));

        var result = await controller.UpdateGoalProgress(
            goalId, new UpdateGoalProgressCommand(goalId, 50), CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        mediator.Capture.Requests.OfType<UpdateGoalProgressCommand>().Should().ContainSingle()
            .Which.IsInstitutionAdminOperation.Should().BeTrue();
    }

    [Fact]
    public async Task InstitutionAdmin_DeletesScopedGoalUsingInternalOperationFlag()
    {
        var institutionId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var mediator = CapturingMediator.Create();
        mediator.Capture.GoalToReturn = new Coaching.Application.Queries.GetCoachingAdminGoal.CoachingAdminGoalDetailDto(
            goalId, studentId, null, "Hedef", null, GoalCategory.SubjectMastery,
            null, null, null, null, 0, false, DateTime.UtcNow);
        var controller = CreateController(
            mediator.Mediator,
            new CoachingAdminScope(false, institutionId, [studentId]),
            new StubIdentityAuthorizationClient(institutionId));

        var result = await controller.DeleteGoal(goalId, CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NoContentResult>();
        mediator.Capture.Requests.OfType<DeleteGoalCommand>().Should().ContainSingle()
            .Which.IsInstitutionAdminOperation.Should().BeTrue();
    }

    [Fact]
    public async Task InstitutionAdmin_EditsScopedGoalUsingInternalOperationFlag()
    {
        var institutionId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var mediator = CapturingMediator.Create();
        mediator.Capture.GoalToReturn = new Coaching.Application.Queries.GetCoachingAdminGoal.CoachingAdminGoalDetailDto(
            goalId, studentId, teacherId, "Hedef", null, GoalCategory.SubjectMastery,
            null, null, null, null, 0, false, DateTime.UtcNow);
        var controller = CreateController(
            mediator.Mediator,
            new CoachingAdminScope(false, institutionId, [studentId]),
            new StubIdentityAuthorizationClient(institutionId));
        var command = new UpdateGoalCommand(
            goalId, "Güncel hedef", null, GoalCategory.SubjectMastery,
            null, null, null, null);

        var result = await controller.UpdateGoal(goalId, command, CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        mediator.Capture.Requests.OfType<UpdateGoalCommand>().Should().ContainSingle()
            .Which.IsInstitutionAdminOperation.Should().BeTrue();
    }

    [Fact]
    public void InstitutionAdminOperationFlagCannotBeSetByRequestJson()
    {
        var command = JsonSerializer.Deserialize<CreateAssignmentCommand>(
            """{"TeacherId":"22222222-2222-2222-2222-222222222222","IsInstitutionAdminOperation":true}""");

        command.Should().NotBeNull();
        command!.IsInstitutionAdminOperation.Should().BeFalse();

        var goalCommand = JsonSerializer.Deserialize<CreateGoalCommand>(
            """{"StudentId":"11111111-1111-1111-1111-111111111111","Title":"Test","Category":0,"IsInstitutionAdminOperation":true,"InstitutionId":"33333333-3333-3333-3333-333333333333"}""");
        goalCommand.Should().NotBeNull();
        goalCommand!.IsInstitutionAdminOperation.Should().BeFalse();
        goalCommand.InstitutionId.Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(CoachingAdminController.CreateAssignment))]
    [InlineData(nameof(CoachingAdminController.CancelAssignment))]
    [InlineData(nameof(CoachingAdminController.DeleteAssignment))]
    [InlineData(nameof(CoachingAdminController.GradeAssignment))]
    [InlineData(nameof(CoachingAdminController.UpdateAssignment))]
    public void AssignmentManagementActionsAllowScopedInstitutionAdministratorsAndKeepMfa(string actionName)
    {
        var method = typeof(CoachingAdminController).GetMethod(actionName);
        method.Should().NotBeNull();
        method!.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(inherit: true)
            .Should().Contain(attribute => attribute.Roles == "SystemAdmin,InstitutionAdmin,InstitutionOwner")
            .And.Contain(attribute => attribute.Policy == "MfaRequired");
    }

    [Theory]
    [InlineData(nameof(CoachingAdminController.CreateGoal))]
    [InlineData(nameof(CoachingAdminController.UpdateGoalProgress))]
    [InlineData(nameof(CoachingAdminController.UpdateGoal))]
    [InlineData(nameof(CoachingAdminController.DeleteGoal))]
    public void GoalManagementActionsAllowScopedInstitutionAdministratorsAndKeepMfa(string actionName)
    {
        var method = typeof(CoachingAdminController).GetMethod(actionName);
        method.Should().NotBeNull();
        method!.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(inherit: true)
            .Should().Contain(attribute => attribute.Roles == "SystemAdmin,InstitutionAdmin,InstitutionOwner")
            .And.Contain(attribute => attribute.Policy == "MfaRequired");
    }

    private static CoachingAdminController CreateController(
        IMediator mediator,
        CoachingAdminScope scope,
        ICoachingIdentityAuthorizationClient identity) =>
        new(mediator, new FixedScopeAuthorization(scope), identity);

    private sealed class FixedScopeAuthorization(CoachingAdminScope scope) : ICoachingAdminScopeAuthorization
    {
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(scope);
    }

    private sealed class StubIdentityAuthorizationClient(Guid? authorizedInstitutionId)
        : ICoachingIdentityAuthorizationClient
    {
        public (Guid TeacherId, IReadOnlyCollection<Guid> StudentIds, Guid? InstitutionId, bool IsSystemAdmin)? TargetRequest { get; private set; }

        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken cancellationToken) =>
            Task.FromResult<CoachingAdminAccessScope?>(null);

        public Task<Guid?> AuthorizeTeacherTargetsAsync(
            Guid teacherId,
            IReadOnlyCollection<Guid> studentIds,
            Guid? requestedInstitutionId,
            bool isSystemAdministrator,
            CancellationToken cancellationToken)
        {
            TargetRequest = (teacherId, studentIds.ToArray(), requestedInstitutionId, isSystemAdministrator);
            return Task.FromResult(authorizedInstitutionId);
        }

        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(
            Guid viewerUserId,
            IReadOnlyCollection<Guid> studentIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }

    public class CapturingMediator : DispatchProxy
    {
        public List<object> Requests { get; } = [];
        public Coaching.Application.Queries.GetCoachingAdminGoal.CoachingAdminGoalDetailDto? GoalToReturn { get; set; }

        public static (IMediator Mediator, CapturingMediator Capture) Create()
        {
            var mediator = DispatchProxy.Create<IMediator, CapturingMediator>();
            return (mediator, (CapturingMediator)(object)mediator);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != "Send")
                throw new NotSupportedException($"Unexpected mediator call: {targetMethod?.Name}");

            var request = args?.FirstOrDefault(argument => argument is not CancellationToken)
                ?? throw new InvalidOperationException("Mediator request missing.");
            Requests.Add(request);
            if (targetMethod.ReturnType == typeof(Task))
                return Task.CompletedTask;

            var resultType = targetMethod.ReturnType.GenericTypeArguments.Single();
            object? result = request switch
            {
                CreateAssignmentCommand => new CreateAssignmentResponse(Guid.NewGuid(), "Ödev", DateTime.UtcNow, 1),
                CreateGoalCommand => new CreateGoalResponse(Guid.NewGuid()),
                Coaching.Application.Queries.GetCoachingAdminGoal.GetCoachingAdminGoalQuery => GoalToReturn,
                UpdateGoalCommand update => new UpdateGoalResponse(update.GoalId, update.Title),
                _ => null
            };
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [result]);
        }
    }
}
