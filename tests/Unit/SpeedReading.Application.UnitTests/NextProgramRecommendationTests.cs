using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assessment;
using SpeedReading.Application.StudentProgram;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Sessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class NextProgramRecommendationTests
{
    [Theory]
    [InlineData(false, 4550)]
    [InlineData(true, 4550)]
    [InlineData(false, 3999)]
    [InlineData(true, 3999)]
    public async Task Completed_current_cycle_recommends_age_matched_general_program_with_correct_approval(bool managed, int scoreHundredths)
    {
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var age = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var teacher = Guid.NewGuid();
        var template = Template(age, "Current", 0, 40, 0);
        var next = Template(age, "Next", 40, 70, 0);
        db.ProgramTemplates.AddRange(template, next, Template(age, "Exam", 0, 100, 1),
            Template(Guid.NewGuid(), "Other age", 40, 70, 0));
        var progress = StudentProgramProgress.Import(Guid.NewGuid(), user, template.Id, now.AddDays(-10),
            1, 1, 1, 1, 1, now.AddDays(-1), false, now.AddDays(-1), 80, 1, 1, now.AddDays(-10), null, null, null);
        db.StudentProgramProgresses.Add(progress);
        var post = AssessmentAttempt.Start(Guid.NewGuid(), user, AssessmentAttemptPhase.PostTraining,
            "tr-posttraining-v1", "tr", age, 3, now, null);
        post.BindToProgram(progress.Id);
        post.Complete(now);
        db.AssessmentAttempts.Add(post);
        for (var index = 0; index < 3; index++)
            db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), Guid.NewGuid(), user,
                Guid.NewGuid(), null, 80, 10, rawWpm: 300, comprehensionScore: scoreHundredths / 100m, weightedKdp: 0, score: 80,
                completedAt: now, isMeasured: true,
                isAssessmentMode: true, assessmentAttemptId: post.Id));
        if (managed)
            db.TeacherStudentAssignments.Add(SpeedReadingTeacherStudentAssignment.Create(null,
                teacher, user, user, now));
        var typeId = Guid.NewGuid();
        db.ExerciseTypes.Add(ExerciseType.Create(typeId, "Fixation", "Fixation", "focus"));
        db.Exercises.Add(Exercise.Create("Exercise", "Fixation", "{}", 1, user, typeId));
        await db.SaveChangesAsync();
        var expected = scoreHundredths < 4000 ? template : next;
        var rejected = scoreHundredths < 4000 ? next : template;
        var result = await Service(db).GetNextProgramRecommendationAsync(user, CancellationToken.None);
        result.Should().NotBeNull();
        result!.TemplateId.Should().Be(expected.Id);
        result.SourceProgressId.Should().Be(progress.Id);
        result.AssessmentAttemptId.Should().Be(post.Id);
        result.RequiresStaffApproval.Should().Be(managed);
        (await db.StudentProgramProgresses.CountAsync()).Should().Be(1);
        var wrongStart = () => Service(db).StartProgramAsync(user, rejected.Id, CancellationToken.None);
        (await wrongStart.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>())
            .Which.Code.Should().Be("Program.RecommendationChanged");
        if (managed)
        {
            var unapproved = () => Service(db).StartProgramAsync(user, expected.Id, CancellationToken.None);
            (await unapproved.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>())
                .Which.Code.Should().Be("Program.StaffApprovalRequired");
        }
        else
        {
            var legacyStart = () => Service(db).StartProgramAsync(user, expected.Id, CancellationToken.None);
            (await legacyStart.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>())
                .Which.Code.Should().Be("Program.ExplicitConfirmationRequired");
        }
        var access = DispatchProxy.Create<ISpeedReadingTeacherAccess, TeacherAccessProxy>();
        ((TeacherAccessProxy)access).Teacher = teacher;
        ((TeacherAccessProxy)access).Student = user;
        var request = new ConfirmNextStudentProgramRequest(expected.Id, progress.Id, post.Id);
        var wrongViewer = () => Service(db, access).ApproveNextProgramAsync(Guid.NewGuid(), user, request, null, CancellationToken.None);
        await wrongViewer.Should().ThrowAsync<UnauthorizedAccessException>();
        var accepted = managed
            ? await Service(db, access).ApproveNextProgramAsync(teacher, user, request, null, CancellationToken.None)
            : await Service(db, access).ConfirmNextProgramAsync(user, request, CancellationToken.None);
        var repeated = managed
            ? await Service(db, access).ApproveNextProgramAsync(teacher, user, request, null, CancellationToken.None)
            : await Service(db, access).ConfirmNextProgramAsync(user, request, CancellationToken.None);
        repeated.ProgramId.Should().Be(accepted.ProgramId);
        (await db.StudentProgramProgresses.CountAsync(item => item.IsActive)).Should().Be(1);
        (await db.StudentProgramProgresses.CountAsync()).Should().Be(2);
        var saved = await db.StudentProgramProgresses.SingleAsync(item => item.Id == accepted.ProgramId);
        saved.CreatedBy.Should().Be((managed ? teacher : user).ToString());
        progress.CompletedDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Missing_measurement_does_not_generate_a_guessed_recommendation()
    {
        await using var db = CreateDb();
        (await Service(db).GetNextProgramRecommendationAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
    }

    private static OwnedSpeedReadingDbContext CreateDb() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ISpeedReadingStudentProgram Service(OwnedSpeedReadingDbContext db, ISpeedReadingTeacherAccess? access = null)
    {
        var assembly = typeof(OwnedSpeedReadingDbContext).Assembly;
        var assessment = (ISpeedReadingAssessment)Activator.CreateInstance(assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!, db, null)!;
        return (ISpeedReadingStudentProgram)Activator.CreateInstance(assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingStudentProgram")!, db, assessment, access)!;
    }

    private static ProgramTemplate Template(Guid age, string name, int min, int max, int purpose) =>
        ProgramTemplate.Import(Guid.NewGuid(), name, "", age, min, max,
            "{\"week1\":{\"day1\":[{\"Type\":\"Fixation\",\"Count\":1,\"Difficulty\":1}]}}", 1, 2, 5, 1, 1, true,
            1, purpose, purpose == 1 ? "LGS" : null, false, DateTime.UtcNow, null, null, null);

    public class TeacherAccessProxy : DispatchProxy
    {
        public Guid Teacher { get; set; }
        public Guid Student { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult((Guid)args![0]! == Teacher && (Guid)args[1]! == Student);
    }
}
