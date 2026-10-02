using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingStudyGoalReportTests
{
    [Fact]
    public async Task CurrentGoals_PreserveManualProgressAndOwnershipRatherThanInferAchievementFromExams()
    {
        await using var db = Database();
        var actor = new Actor();
        var goal = AcademicGoal.Create(actor.UserId!.Value, "My target", GoalCategory.ExamPreparation);
        goal.SetTarget(new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc), 400, ExamType.LGS);
        goal.UpdateProgress(30);
        var teacherGoal = AcademicGoal.Create(actor.UserId.Value, "Teacher target", GoalCategory.StudyHabits, Guid.NewGuid());
        teacherGoal.MarkAsCompleted();
        var otherGoal = AcademicGoal.Create(Guid.NewGuid(), "Private other student target", GoalCategory.Other);
        var institutionGoal = AcademicGoal.Create(actor.UserId.Value, "Institution target", GoalCategory.Other,
            institutionId: Guid.NewGuid());
        var exam = Exam.CreateStudentReported(actor.UserId.Value, "Practice", ExamType.LGS,
            new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), 100);
        exam.AddResult(ExamResult.Create(exam.Id, actor.UserId.Value, 80));
        db.AddRange(goal, teacherGoal, institutionGoal, otherGoal, exam);
        await db.SaveChangesAsync();
        var report = await new CoachingStudentStudyReportService(db, new CoachingAccessPolicy(actor))
            .GetAsync(new(2026, 10, 1), new(2026, 10, 2));

        Assert.Equal(3, report.Goals.Count);
        var own = Assert.Single(report.Goals.Where(x => x.GoalId == goal.Id));
        Assert.Equal(30, own.RecordedProgress);
        Assert.Equal("Unspecified", own.Source);
        Assert.Equal(400, own.TargetScore);
        Assert.Equal(ExamType.LGS, own.TargetExamType);
        Assert.False(own.IsCompleted);
        Assert.Equal("TeacherSet", Assert.Single(report.Goals.Where(x => x.GoalId == teacherGoal.Id)).Source);
        Assert.Equal("Unspecified", Assert.Single(report.Goals.Where(x => x.GoalId == institutionGoal.Id)).Source);
        Assert.Equal("CurrentGoalsWithRecordedProgress", report.GoalReason);
        Assert.DoesNotContain(report.Goals, x => x.GoalId == otherGoal.Id);
        Assert.Equal(30, (await db.AcademicGoals.SingleAsync(x => x.Id == goal.Id)).CurrentProgress);
    }

    [Fact]
    public async Task GoalLimit_RejectsRatherThanSilentlyTruncates()
    {
        await using var db = Database(); var actor = new Actor();
        db.AddRange(Enumerable.Range(0, 1001).Select(x => AcademicGoal.Create(actor.UserId!.Value, $"Goal {x}", GoalCategory.Other)));
        await db.SaveChangesAsync();
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new CoachingStudentStudyReportService(db, new CoachingAccessPolicy(actor)).GetAsync(new(2026, 10, 1), new(2026, 10, 2)));
        Assert.Equal("StudyPlanning.GoalReportLimit", exception.Code);
    }

    [Fact]
    public async Task TeacherCannotUseStudentReport()
    {
        await using var db = Database(); var actor = new Actor { Roles = ["Teacher"] };
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new CoachingStudentStudyReportService(db, new CoachingAccessPolicy(actor)).GetAsync(new(2026, 10, 1), new(2026, 10, 2)));
    }

    private static CoachingDbContext Database() => new(new DbContextOptionsBuilder<CoachingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class Actor : ICurrentUserService
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public IEnumerable<string> Roles { get; set; } = ["Student"];
        public string? Email => null;
        public string? FullName => null;
        public bool IsAuthenticated => UserId.HasValue;
        public ClaimsPrincipal? User => null;
    }
}
