using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.GetExamResults;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingStudentExamServiceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task StudentExam_RoundTripsOwnDataRequiresVersionAndRejectsForeignCatalogAndOwnership()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try
        {
            var actor = new Actor(); var access = new CoachingAccessPolicy(actor);
            var studyReports = new CoachingStudentStudyReportService(db, access);
            Assert.Null((await studyReports.GetAsync(new(2026, 10, 1), new(2026, 10, 2))).CompletionPercentage);
            var ownPlan = StudyPlanRevision.Create(actor.UserId!.Value, Guid.NewGuid(), 1, "Own plan");
            ownPlan.Activate();
            var otherPlan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Other plan");
            otherPlan.Activate();
            var task = StudyPlanTask.Create(ownPlan, new(2026, 10, 1), "Read", 30); task.Complete(25);
            db.AddRange(ownPlan, otherPlan, task, StudyPlanTask.Create(otherPlan, new(2026, 10, 1), "Private", 60));
            await db.SaveChangesAsync();
            var studyReport = await studyReports.GetAsync(new(2026, 10, 1), new(2026, 10, 2));
            Assert.Equal(1, studyReport.ScheduledTasks);
            Assert.Equal(100m, studyReport.CompletionPercentage);
            Assert.Equal(25, studyReport.ActualMinutes);
            var lesson = StudyCatalogLesson.Create("test", "l", "Math", 8, "LGS");
            var unit = StudyCatalogUnit.Create("test", "u", lesson.Id, "Unit", 0);
            var topic = StudyCatalogTopic.Create("test", "t", lesson.Id, unit.Id, "Topic", null, 0, 30);
            db.AddRange(lesson, unit, topic);
            db.Entry(lesson).Property(x => x.IsActive).CurrentValue = true;
            db.Entry(unit).Property(x => x.IsActive).CurrentValue = true;
            db.Entry(topic).Property(x => x.IsActive).CurrentValue = true;
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var service = new CoachingStudentExamService(db, access);
            var topicPage = await new CoachingTopicSearchService(db).SearchAsync(null, null, null, 1, 20);
            Assert.Equal(lesson.Id, topicPage.Items.Single().LessonId);
            var request = new StudentExamInput("My mock", ExamType.Mock, new(2026, 10, 1), 80, 100, 8, 1, 1,
                [new(lesson.Id, topic.Id, 10, 8, 1, 1)]);
            var created = await service.CreateAsync(request);
            db.ChangeTracker.Clear();
            var read = (await service.GetAsync(created.Id))!;
            Assert.Equal("StudentReported", read.Source);
            Assert.Equal(topic.Id, read.Lessons.Single().TopicId);
            Assert.Equal("Math", read.Lessons.Single().LessonName);
            Assert.Equal("Topic", read.Lessons.Single().TopicName);
            Assert.Equal(1, (await service.ListAsync(1, 10)).TotalCount);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReplaceAsync(created.Id,
                new(request with { Title = "Changed" }, read.Version + 1)));
            var replaced = await service.ReplaceAsync(created.Id, new(request with { Title = "Changed" }, read.Version));
            Assert.Equal("Changed", replaced.Title);
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request with { CorrectAnswers = 9 }));
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request with {
                Lessons = [new(lesson.Id, Guid.NewGuid(), 10, 8, 1, 1)] }));
            var otherActor = new Actor();
            var otherService = new CoachingStudentExamService(db, new CoachingAccessPolicy(otherActor));
            Assert.Null(await otherService.GetAsync(created.Id));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => otherService.ReplaceAsync(created.Id, new(request, replaced.Version)));
            var teacher = Exam.Create(Guid.NewGuid(), "Teacher result", ExamType.Mock, DateTime.UtcNow, 100);
            teacher.AddResult(ExamResult.Create(teacher.Id, actor.UserId!.Value, 70));
            db.Add(teacher); await db.SaveChangesAsync();
            var resultPeriod = await studyReports.GetAsync(new(2026, 1, 1), new(2026, 12, 31));
            Assert.Equal(80m, resultPeriod.ExamGroups.Single(x => x.Source == "StudentReported").AveragePercentage);
            Assert.Equal(70m, resultPeriod.ExamGroups.Single(x => x.Source == "TeacherRecorded").AveragePercentage);
            Assert.Equal(10, resultPeriod.LessonResults.Single().QuestionCount);
            Assert.Equal("Math", resultPeriod.LessonResults.Single().LessonName);
            var legacyResults = await new GetStudentExamResultsQueryHandler(new ExamRepository(db), access, new Identity(actor.UserId!.Value))
                .Handle(new(actor.UserId!.Value), CancellationToken.None);
            Assert.Equal("StudentReported", legacyResults.Items.Single(x => x.ExamId == created.Id).Source);
            Assert.Equal("TeacherRecorded", legacyResults.Items.Single(x => x.ExamId == teacher.Id).Source);
            Assert.Single(legacyResults.Items.Single(x => x.ExamId == created.Id).LessonAnswers!);
            var summary = await new CoachingStudentProgressRepository(db).GetStudentSummaryAsync(actor.UserId!.Value);
            Assert.Equal(70, summary.AverageExamPercentage);
            var admin = new CoachingAdminRepository(db);
            var detail = await admin.GetStudentDetailAsync(actor.UserId.Value);
            Assert.Equal("StudentReported", detail.Exams.Single(x => x.Id == created.Id).Source);
            var history = await admin.GetStudentHistoryAsync(actor.UserId.Value, CoachingStudentHistoryType.Exams, 1, 20);
            Assert.Equal("StudentReported", history.Items.Single(x => x.Id == created.Id).Source);
            Assert.Null(await service.GetAsync(teacher.Id));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteAsync(teacher.Id, teacher.Version));
            await service.DeleteAsync(created.Id, replaced.Version);
            Assert.Null(await service.GetAsync(created.Id));
            Assert.True(await db.Exams.AnyAsync(x => x.Id == teacher.Id));
            actor.Roles = ["Teacher"];
            await Assert.ThrowsAsync<BusinessRuleException>(() => studyReports.GetAsync(new(2026, 10, 1), new(2026, 10, 2)));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.ListAsync(1, 10));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class Actor : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Email => null;
        public string? UserName => null;
        public string? FullName => null;
        public ClaimsPrincipal? User => null;
        public IEnumerable<string> Roles { get; set; } = ["Student"];
        public bool IsAuthenticated => true;
        public IEnumerable<Claim> Claims => [];
        public string? GetClaimValue(string type) => null;
    }

    private sealed class Identity(Guid student) : ICoachingIdentityAuthorizationClient
    {
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Guid>>([student]);
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? requestedInstitutionId, bool isSystemAdministrator, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
