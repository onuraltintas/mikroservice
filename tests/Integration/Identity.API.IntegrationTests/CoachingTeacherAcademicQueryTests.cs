using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.GetExamResults;
using Coaching.Application.Queries.GetGoals;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingTeacherAcademicQueryTests
{
    [Fact]
    public async Task TeacherExamQuery_ShouldReturnOnlyTheCurrentTeachersExams()
    {
        var teacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();
        var ownExam = Exam.Create(teacherId, "Kendi denemem", ExamType.Mock, DateTime.UtcNow.AddDays(1), 100);
        var otherExam = Exam.Create(otherTeacherId, "Başka koçun denemesi", ExamType.Mock, DateTime.UtcNow.AddDays(2), 100);
        ownExam.UpdateEditableDetails("Kendi denemem", ExamType.Mock, "Matematik", "Açıklama", DateTime.UtcNow.AddDays(1), 60, 100, 8);

        var handler = new GetTeacherExamsQueryHandler(
            new StubExamRepository(ownExam, otherExam),
            CreatePolicy(teacherId, "Teacher"));

        var result = await handler.Handle(new GetTeacherExamsQuery(teacherId), CancellationToken.None);

        var item = result.Items.Should().ContainSingle().Which;
        item.Title.Should().Be("Kendi denemem");
        item.Subject.Should().Be("Matematik");
        item.Description.Should().Be("Açıklama");
        item.DurationMinutes.Should().Be(60);
        item.TargetGradeLevel.Should().Be(8);
    }

    [Fact]
    public async Task TeacherGoalQuery_ShouldRejectAnotherTeacher()
    {
        var ownerTeacherId = Guid.NewGuid();
        var viewerTeacherId = Guid.NewGuid();
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Hedef", GoalCategory.ExamPreparation, ownerTeacherId);
        goal.UpdateEditableDetails("Hedef", "Açıklama", GoalCategory.ExamPreparation, DateTime.UtcNow.AddDays(10), 80, ExamType.LGS, "Matematik");

        var handler = new GetTeacherGoalsQueryHandler(
            new StubGoalRepository(goal),
            CreatePolicy(viewerTeacherId, "Teacher"));

        var action = () => handler.Handle(new GetTeacherGoalsQuery(ownerTeacherId), CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.Code == "Authorization.Forbidden");
    }

    [Fact]
    public async Task TeacherExamDetail_ShouldExposeOwnResultsForCorrection()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var exam = Exam.Create(teacherId, "LGS denemesi", ExamType.LGS, DateTime.UtcNow.AddDays(1), 500);
        exam.AddResult(ExamResult.Create(exam.Id, studentId, 420));
        var result = exam.Results.Single();
        result.SetAnswerStatistics(80, 10, 0);
        result.AddTeacherNotes("Tekrar gereken konular var.");

        var handler = new GetTeacherExamDetailQueryHandler(
            new StubExamRepository(exam),
            CreatePolicy(teacherId, "Teacher"));

        var detail = await handler.Handle(new GetTeacherExamDetailQuery(exam.Id), CancellationToken.None);

        detail.Should().NotBeNull();
        detail!.Results.Should().ContainSingle().Which.Should().Match<TeacherExamResultDto>(item =>
            item.StudentId == studentId
            && item.Score == 420
            && item.CorrectAnswers == 80
            && item.TeacherNotes == "Tekrar gereken konular var.");
    }

    private static ICoachingAccessPolicy CreatePolicy(Guid userId, params string[] roles) =>
        new CoachingAccessPolicy(new StubCurrentUserService(userId, roles));

    private sealed class StubExamRepository(params Exam[] exams) : IExamRepository
    {
        private readonly IReadOnlyList<Exam> _exams = exams;

        public Task<Exam?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_exams.SingleOrDefault(exam => exam.Id == id));

        public Task<Exam?> GetMetadataByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_exams.SingleOrDefault(exam => exam.Id == id));

        public Task<PagedRepositoryResult<ExamResult>> GetResultsByExamIdAsync(Guid examId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var exam = _exams.SingleOrDefault(item => item.Id == examId);
            return Task.FromResult(Page(exam?.Results ?? [], pageNumber, pageSize));
        }

        public Task<List<Exam>> GetByInstitutionIdAsync(Guid institutionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_exams.Where(exam => exam.InstitutionId == institutionId).ToList());

        public Task<PagedRepositoryResult<Exam>> GetByTeacherIdAsync(Guid teacherId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(Page(_exams.Where(exam => exam.CreatedByTeacherId == teacherId), pageNumber, pageSize));

        public Task<PagedRepositoryResult<Exam>> GetByStudentIdAsync(Guid studentId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(Page(Array.Empty<Exam>(), pageNumber, pageSize));

        public Task<Exam> AddAsync(Exam exam, CancellationToken cancellationToken = default) => Task.FromResult(exam);
        public Task UpdateAsync(Exam exam, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Exam exam, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubGoalRepository(params AcademicGoal[] goals) : IAcademicGoalRepository
    {
        private readonly IReadOnlyList<AcademicGoal> _goals = goals;

        public Task<AcademicGoal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_goals.SingleOrDefault(goal => goal.Id == id));

        public Task<PagedRepositoryResult<AcademicGoal>> GetByTeacherIdAsync(Guid teacherId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(Page(_goals.Where(goal => goal.SetByTeacherId == teacherId), pageNumber, pageSize));

        public Task<PagedRepositoryResult<AcademicGoal>> GetByStudentIdAsync(Guid studentId, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(Page(Array.Empty<AcademicGoal>(), pageNumber, pageSize));

        public Task<AcademicGoal> AddAsync(AcademicGoal goal, CancellationToken cancellationToken = default) => Task.FromResult(goal);
        public Task UpdateAsync(AcademicGoal goal, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(AcademicGoal goal, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static PagedRepositoryResult<T> Page<T>(IEnumerable<T> items, int pageNumber, int pageSize)
    {
        var list = items.ToList();
        return new PagedRepositoryResult<T>(list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(), list.Count);
    }

    private sealed class StubCurrentUserService(Guid userId, string[] roles) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => roles;
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
