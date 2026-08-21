using Coaching.Application.Commands.CreateAssignment;
using Coaching.Application.Commands.CreateExam;
using Coaching.Application.Commands.CreateGoal;
using Coaching.Application.Commands.CreateSession;
using Coaching.Application.Commands.UpdateGoalProgress;
using Coaching.Application.Commands.UpdateSessionStudentNote;
using Coaching.Application.Queries.GetCoachingAdminAssignments;
using Coaching.Application.Queries.GetCoachingAdminSessions;
using Coaching.Application.Queries.GetCoachingAdminExams;
using Coaching.Application.Queries.GetCoachingAdminGoals;
using Coaching.Application.Queries.GetStudentProgress;
using Coaching.Domain.Enums;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingContractTests
{
    [Fact]
    public async Task CreateAssignmentContract_RejectsEmptyTargetsAndInvalidScoring()
    {
        var validator = new CreateAssignmentCommandValidator();
        var result = await validator.ValidateAsync(new CreateAssignmentCommand
        {
            TeacherId = Guid.NewGuid(),
            Title = "Math",
            AssignmentType = "Individual",
            DueDate = DateTime.UtcNow.AddDays(1),
            MaxScore = 10,
            PassingScore = 11,
            StudentIds = new()
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName)
            .Should().Contain(new[] { "PassingScore", "StudentIds" });
    }

    [Fact]
    public async Task CreateAssignmentContract_RequiresSafeIdempotencyKey()
    {
        var validator = new CreateAssignmentCommandValidator();
        var result = await validator.ValidateAsync(new CreateAssignmentCommand
        {
            TeacherId = Guid.NewGuid(),
            Title = "Math",
            AssignmentType = "Individual",
            DueDate = DateTime.UtcNow.AddDays(1),
            StudentIds = [Guid.NewGuid()],
            IdempotencyKey = "too-short"
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "IdempotencyKey");
    }

    [Fact]
    public async Task CreateSessionContract_RejectsOverlongSession()
    {
        var validator = new CreateSessionCommandValidator();
        var result = await validator.ValidateAsync(new CreateSessionCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            241,
            "Math",
            null,
            SessionType.OneOnOne));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "DurationMinutes");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("not-a-url")]
    public async Task CreateSessionContract_RejectsUnsafeMeetingLink(string meetingLink)
    {
        var validator = new CreateSessionCommandValidator();
        var result = await validator.ValidateAsync(new CreateSessionCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            60,
            "Math",
            null,
            SessionType.OneOnOne,
            "meeting-link-key",
            null,
            meetingLink));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateSessionCommand.MeetingLink));
    }

    [Fact]
    public async Task CreateSessionContract_RequiresMultipleStudentsForGroupSession()
    {
        var validator = new CreateSessionCommandValidator();
        var result = await validator.ValidateAsync(new CreateSessionCommand(
            Guid.NewGuid(),
            Guid.Empty,
            DateTime.UtcNow.AddHours(1),
            60,
            "Math",
            null,
            SessionType.Group,
            "group-session-key",
            [Guid.NewGuid()]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "StudentIds");
    }

    [Fact]
    public async Task CreateExamContract_RejectsInvalidScoreAndPastDate()
    {
        var validator = new CreateExamCommandValidator();
        var result = await validator.ValidateAsync(new CreateExamCommand(
            Guid.NewGuid(),
            "Mock exam",
            ExamType.Mock,
            DateTime.UtcNow.AddMinutes(-1),
            0,
            null,
            null));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName)
            .Should().Contain(new[] { "ExamDate", "MaxScore" });
    }

    [Fact]
    public async Task CreateExamContract_RejectsUnknownTypeAndDatabaseOverflowScore()
    {
        var validator = new CreateExamCommandValidator();
        var result = await validator.ValidateAsync(new CreateExamCommand(
            Guid.NewGuid(),
            "Mock exam",
            (ExamType)999,
            DateTime.UtcNow.AddDays(1),
            1_000m,
            null,
            new string('x', 2_000)));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName)
            .Should().Contain(new[] { "Type", "MaxScore" });
    }

    [Fact]
    public async Task CreateGoalContract_RejectsInvalidStudentAndTargetScore()
    {
        var validator = new CreateGoalCommandValidator();
        var result = await validator.ValidateAsync(new CreateGoalCommand(
            Guid.Empty,
            "Exam preparation",
            GoalCategory.ExamPreparation,
            null,
            new string('x', 2_001),
            DateTime.UtcNow.AddDays(1),
            -1));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName)
            .Should().Contain(new[] { "StudentId", "Description", "TargetScore" });
    }

    [Fact]
    public async Task CreateGoalContract_RejectsUnknownCategoryAndDatabaseOverflowScore()
    {
        var validator = new CreateGoalCommandValidator();
        var result = await validator.ValidateAsync(new CreateGoalCommand(
            Guid.NewGuid(),
            "Exam preparation",
            (GoalCategory)999,
            null,
            null,
            DateTime.UtcNow.AddDays(1),
            1_000m));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName)
            .Should().Contain(new[] { "Category", "TargetScore" });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task UpdateGoalProgressContract_RejectsProgressOutsidePercentageRange(int progress)
    {
        var validator = new UpdateGoalProgressCommandValidator();

        var result = await validator.ValidateAsync(new UpdateGoalProgressCommand(Guid.NewGuid(), progress));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpdateGoalProgressCommand.Progress));
    }

    [Fact]
    public async Task StudentProgressQueryContract_RequiresStudentId()
    {
        var result = await new GetStudentProgressQueryValidator()
            .ValidateAsync(new GetStudentProgressQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GetStudentProgressQuery.StudentId));
    }

    [Fact]
    public async Task UpdateSessionStudentNoteContract_RejectsOverlongNote()
    {
        var result = await new UpdateSessionStudentNoteCommandValidator().ValidateAsync(
            new UpdateSessionStudentNoteCommand(Guid.NewGuid(), Guid.NewGuid(), new string('x', 2_001)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpdateSessionStudentNoteCommand.Note));
    }

    [Fact]
    public async Task CoachingAdminAssignmentsContract_RejectsInvalidFilterAndPage()
    {
        var validator = new GetCoachingAdminAssignmentsQueryValidator();

        var result = await validator.ValidateAsync(new GetCoachingAdminAssignmentsQuery(
            PageNumber: 0,
            PageSize: 101,
            Status: "unknown",
            Source: "unknown"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GetCoachingAdminAssignmentsQuery.Status));
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GetCoachingAdminAssignmentsQuery.Source));
    }

    [Fact]
    public async Task CoachingAdminOperationalQueries_RejectInvalidFiltersAndPage()
    {
        var sessionResult = await new GetCoachingAdminSessionsQueryValidator().ValidateAsync(
            new GetCoachingAdminSessionsQuery(0, 101, "unknown", new string('x', 201)));
        var examResult = await new GetCoachingAdminExamsQueryValidator().ValidateAsync(
            new GetCoachingAdminExamsQuery(0, 101, "unknown", new string('x', 201)));
        var goalResult = await new GetCoachingAdminGoalsQueryValidator().ValidateAsync(
            new GetCoachingAdminGoalsQuery(0, 101, null, new string('x', 201)));

        sessionResult.IsValid.Should().BeFalse();
        sessionResult.Errors.Should().Contain(error => error.PropertyName == nameof(GetCoachingAdminSessionsQuery.Status));
        examResult.IsValid.Should().BeFalse();
        examResult.Errors.Should().Contain(error => error.PropertyName == nameof(GetCoachingAdminExamsQuery.ExamType));
        goalResult.IsValid.Should().BeFalse();
        goalResult.Errors.Should().Contain(error => error.PropertyName == nameof(GetCoachingAdminGoalsQuery.Search));
    }
}
