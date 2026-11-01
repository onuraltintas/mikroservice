using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

/// <summary>
/// KM8 (user decision 01.11.2026): the coaching catalog keeps only exam lessons, so only exam purposes (LGS, YKS, AGS/ÖABT,
/// YDS, YÖKDİL) can be saved. School, General and Undecided stay in the enum for existing rows; such a student must choose
/// an exam before setup or plan creation continues, and their plans stay readable.
/// </summary>
public sealed class CoachingExamOnlyPurposeTests
{
    private const string Notice = "Çalışma amacın artık desteklenmiyor; hazırlandığın sınavı seç.";

    [Fact]
    public void OnlyExamPurposesAreSelectable_TheRetiredOnesKeepTheirNumbers()
    {
        Assert.Equal(new[] { LearningPurpose.Lgs, LearningPurpose.Yks, LearningPurpose.Ags, LearningPurpose.Yds, LearningPurpose.Yokdil },
            Enum.GetValues<LearningPurpose>().Where(LearningPurposes.IsSelectable).ToArray());
        Assert.Equal(new[] { LearningPurpose.School, LearningPurpose.General, LearningPurpose.Undecided },
            Enum.GetValues<LearningPurpose>().Where(LearningPurposes.IsRetired).ToArray());
        Assert.Equal(new[] { 0, 3, 4 }, new[] { LearningPurpose.School, LearningPurpose.General, LearningPurpose.Undecided }.Select(x => (int)x));
    }

    [Theory]
    [InlineData(LearningPurpose.School)]
    [InlineData(LearningPurpose.General)]
    [InlineData(LearningPurpose.Undecided)]
    public async Task NewSavesRejectTheRetiredPurposes(LearningPurpose purpose)
    {
        await using var f = await CoachingLearningSetupTests.Fixture.Create();
        var error = await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SaveAsync(new(null, 2026, purpose, null, null, null, [], [], false), default));
        Assert.Equal(Notice, error.Message);
        Assert.Empty(f.Db.StudentLearningContexts);
        var api = new LearningSetupController(f.Service);
        Assert.IsType<BadRequestObjectResult>(await api.Save(new(null, 2026, purpose, null, null, null, [], [], false), default));
    }

    [Theory]
    [InlineData(LearningPurpose.School)]
    [InlineData(LearningPurpose.General)]
    [InlineData(LearningPurpose.Undecided)]
    public async Task ALegacyPurposeStudentMustChooseAnExam_TheirPlansStayUntouched(LearningPurpose purpose)
    {
        await using var f = await CoachingLearningSetupTests.Fixture.Create();
        // An old school-lesson setup: a published program with a grade-12 lesson the student had chosen.
        var catalog = CurriculumVersion.Create("school-2026", "Okul programı", "v1", 2026, 2026, "Test kaynağı", null); catalog.SetPublished(true);
        var school = StudyCatalogLesson.Create("test", "school-math", "Matematik", 12, null); school.AssignCurriculum(catalog.Id); school.SetActive(true);
        var unit = StudyCatalogUnit.Create("test", "unit", school.Id, "Sayılar", 1); unit.SetActive(true);
        var topic = StudyCatalogTopic.Create("test", "topic", school.Id, unit.Id, "Doğal sayılar", null, 1, 30); topic.SetActive(true);
        var lgs = StudyCatalogLesson.Create("test", "lgs-math", "Matematik", 8, "LGS"); lgs.AssignCurriculum(catalog.Id); lgs.SetActive(true);
        var lgsUnit = StudyCatalogUnit.Create("test", "lgs-unit", lgs.Id, "Çarpanlar", 1); lgsUnit.SetActive(true);
        var lgsTopic = StudyCatalogTopic.Create("test", "lgs-topic", lgs.Id, lgsUnit.Id, "Çarpanlar ve katlar", null, 1, 40); lgsTopic.SetActive(true);
        var context = StudentLearningContext.Create(f.Student, 2026, purpose, null, null, null);
        context.SetEducation(Coaching.Domain.Profiles.CoachingEducationStatus.Student, 12, catalog.Id);
        context.ReplaceSelections([StudentCoursePreference.Create(context.Id, school.Id, 1, null)],
            [StudentTopicState.Create(context.Id, topic.Id, DeclaredTopicStatus.Studied, f.Student)], true);
        var hours = StudyAvailability.Create(f.Student, "Europe/Istanbul"); hours.ReplaceWindows([new(DayOfWeek.Monday, 1020, 1080)]);
        var revision = StudyPlanRevision.Create(f.Student, Guid.NewGuid(), 1, "Okul planım"); revision.Activate();
        var task = StudyPlanTask.Create(revision, new(2026, 10, 19), "Doğal sayılar", 30); task.Complete(25);
        f.Db.AddRange(catalog, school, unit, topic, lgs, lgsUnit, lgsTopic, context, hours, revision, task);
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();

        var view = await f.Service.GetAsync(default);
        Assert.True(view.PurposeRetired);
        Assert.False(view.IsReady);
        Assert.Equal("Purpose", view.NextStep);
        Assert.Equal("Purpose", view.MissingFields[0]);
        Assert.Equal(purpose, view.Context!.Purpose);
        // No lessons are offered for a retired purpose, not even the old grade-matching school lessons.
        var choices = await f.Service.GetCatalogAsync(null, 1, default);
        Assert.Empty(choices.Lessons);
        Assert.Contains(Notice, choices.Warnings);
        // Re-saving the old purpose is refused; choosing an exam starts a new scope.
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SaveAsync(new(view.Context.Version, 2026, purpose, null, null, null, [], [], false), default));
        var chosen = await f.Service.SaveAsync(new(view.Context.Version, 2026, LearningPurpose.Lgs, 2027, null, null, [], [], false), default);
        Assert.False(chosen.PurposeRetired);
        Assert.Equal(LearningPurpose.Lgs, chosen.Context!.Purpose);
        Assert.Equal(new[] { lgs.Id }, (await f.Service.GetCatalogAsync(null, 1, default)).Lessons.Select(x => x.Id));
        // The existing plan and its history are unchanged.
        Assert.Equal(25, (await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync()).ActualMinutes);
        Assert.Equal(StudyPlanStatus.Active, (await f.Db.StudyPlanRevisions.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task ALegacyPurposeBlocksAutomaticPlanPreviewAndWeeklyLessons()
    {
        await using var f = await CoachingLearningSetupTests.Fixture.Create();
        f.Db.Add(StudentLearningContext.Create(f.Student, 2026, LearningPurpose.General, null, null, null));
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var weekly = new CoachingWeeklyStudyDraftService(f.Db, new CoachingAccessPolicy(f.Actor), f.Service);
        Assert.Empty((await weekly.GetAsync(default)).Lessons);
        var preview = new CoachingAutomaticStudyPlanPreviewService(f.Db, new CoachingAccessPolicy(f.Actor), f.Service);
        await Assert.ThrowsAnyAsync<Exception>(() => preview.PreviewAsync(new(new(2026, 10, 19), 7, 0, [new(Guid.NewGuid(), 30)], 1)));
        Assert.Empty(f.Db.StudyPlanRevisions);
    }
}
