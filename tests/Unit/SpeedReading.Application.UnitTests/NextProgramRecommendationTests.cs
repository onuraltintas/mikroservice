using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assessment;
using SpeedReading.Application.StudentProgram;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class NextProgramRecommendationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Completed_current_cycle_recommends_age_matched_general_program_with_correct_approval(bool managed)
    {
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var age = Guid.NewGuid();
        var now = DateTime.UtcNow;
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
                Guid.NewGuid(), null, 80, 10, 300, 300, 45.5m, 0, now, isMeasured: true,
                isAssessmentMode: true, assessmentAttemptId: post.Id));
        if (managed)
            db.TeacherStudentAssignments.Add(SpeedReadingTeacherStudentAssignment.Create(null,
                Guid.NewGuid(), user, user, now));
        await db.SaveChangesAsync();
        var result = await Service(db).GetNextProgramRecommendationAsync(user, CancellationToken.None);
        result.Should().NotBeNull();
        result!.TemplateId.Should().Be(next.Id);
        result.SourceProgressId.Should().Be(progress.Id);
        result.AssessmentAttemptId.Should().Be(post.Id);
        result.RequiresStaffApproval.Should().Be(managed);
        (await db.StudentProgramProgresses.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Missing_measurement_does_not_generate_a_guessed_recommendation()
    {
        await using var db = CreateDb();
        (await Service(db).GetNextProgramRecommendationAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
    }

    private static OwnedSpeedReadingDbContext CreateDb() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ISpeedReadingStudentProgram Service(OwnedSpeedReadingDbContext db)
    {
        var assembly = typeof(OwnedSpeedReadingDbContext).Assembly;
        var assessment = (ISpeedReadingAssessment)Activator.CreateInstance(assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!, db, null)!;
        return (ISpeedReadingStudentProgram)Activator.CreateInstance(assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingStudentProgram")!, db, assessment)!;
    }

    private static ProgramTemplate Template(Guid age, string name, int min, int max, int purpose) =>
        ProgramTemplate.Import(Guid.NewGuid(), name, "", age, min, max, "{}", 1, 2, 5, 1, 1, true,
            1, purpose, purpose == 1 ? "LGS" : null, false, DateTime.UtcNow, null, null, null);
}
