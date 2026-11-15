using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.StudyPlanning;
using Coaching.API.Controllers;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

/// <summary>
/// User decision 15.11.2026: a student with an active teacher makes or changes her own plan only when the teacher allows
/// it (off by default); a student without a teacher plans freely.
/// </summary>
public sealed class StudentPlanningPermissionTests
{
    private sealed class Identity(Guid student) : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid id, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacher, IReadOnlyCollection<Guid> ids, Guid? institution, bool admin, CancellationToken ct)
            => ids.All(x => x == student) ? Task.FromResult(institution) : throw new BusinessRuleException("Authorization.Forbidden", "Denied");
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewer, IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult(ids);
    }

    private sealed class Recorder : ICoachingEventPublisher
    {
        public List<object> Messages { get; } = [];
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class { Messages.Add(message); return Task.CompletedTask; }
    }

    private static async Task<(Guid Student, Guid Teacher, Guid Lesson)> SeedAsync(CurriculumManagementTests.Fixture f, bool withTeacher)
    {
        await f.Seed(); f.User.Roles = ["Student"];
        var student = f.User.UserId!.Value; var teacher = Guid.NewGuid();
        f.Db.Add(CoachingStudentProfile.Create(student));
        if (withTeacher) f.Db.Add(CoachingTeacherStudentAssignment.Create(teacher, student, null, null, teacher));
        await f.Db.SaveChangesAsync();
        return (student, teacher, (await f.Db.StudyCatalogLessons.SingleAsync()).Id);
    }

    private static CoachingWeeklyStudyDraftService Weekly(CurriculumManagementTests.Fixture f) => WeeklyStudyDraftTests.Service(f, new(f.Db));

    [Fact]
    public async Task A_student_without_a_teacher_plans_freely_whatever_the_switch_says()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var (student, _, lesson) = await SeedAsync(f, withTeacher: false);

        var state = await StudentPlanningPermission.StateAsync(f.Db, student, default);
        Assert.Equal((false, false, true), (state.HasTeacher, state.AllowedByTeacher, state.StudentMayPlan));
        await Weekly(f).SaveAsync(new(null, "Europe/Istanbul", [WeeklyStudyDraftTests.Slot(lesson, 1080, 30)]), default);
    }

    [Fact]
    public async Task With_a_teacher_the_student_does_not_change_the_weekly_schedule_until_the_teacher_allows_it()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var (student, teacher, lesson) = await SeedAsync(f, withTeacher: true);
        var input = new WeeklyDraftInput(null, "Europe/Istanbul", [WeeklyStudyDraftTests.Slot(lesson, 1080, 30)]);

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => Weekly(f).SaveAsync(input, default));
        Assert.Equal((StudentPlanningPermission.Code, StudentPlanningPermission.Message), (refused.Code, refused.Message));
        Assert.Equal(403, Assert.IsType<ObjectResult>(await new WeeklyStudyDraftController(Weekly(f)).Save(input, default)).StatusCode);
        // Reading stays open.
        Assert.IsType<OkObjectResult>(await new WeeklyStudyDraftController(Weekly(f)).Get(default));

        (await f.Db.CoachingStudentProfiles.SingleAsync()).SetCanPlanOwnStudy(true, teacher);
        await f.Db.SaveChangesAsync();
        Assert.True((await StudentPlanningPermission.StateAsync(f.Db, student, default)).StudentMayPlan);
        await Weekly(f).SaveAsync(input, default);
    }

    [Fact]
    public async Task When_the_teacher_leaves_the_student_plans_again()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var (student, teacher, lesson) = await SeedAsync(f, withTeacher: true);
        (await f.Db.CoachingTeacherStudentAssignments.SingleAsync()).SetActive(false, teacher);
        await f.Db.SaveChangesAsync();

        Assert.True((await StudentPlanningPermission.StateAsync(f.Db, student, default)).StudentMayPlan);
        await Weekly(f).SaveAsync(new(null, "Europe/Istanbul", [WeeklyStudyDraftTests.Slot(lesson, 1080, 30)]), default);
    }

    [Fact]
    public async Task The_teacher_switches_it_and_the_student_is_told_each_time_it_changes()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var (student, teacher, _) = await SeedAsync(f, withTeacher: true);
        f.User.Roles = ["Teacher"]; f.User.UserId = teacher;
        var access = new CoachingAccessPolicy(f.User); var events = new Recorder();
        var service = new StudentPlanningPermissionService(f.Db, access, events, new CoachingStudyPlanAccess(access, new Identity(student), f.Scope));

        Assert.False((await service.GetForStudentAsync(student, default)).StudentMayPlan);
        Assert.True((await service.SetForStudentAsync(student, true, default)).StudentMayPlan);
        Assert.True((await service.SetForStudentAsync(student, true, default)).StudentMayPlan);
        Assert.False((await service.SetForStudentAsync(student, false, default)).StudentMayPlan);
        await service.WeeklyScheduleSavedByTeacherAsync(student, default);

        var notices = events.Messages.Cast<StudyPlanUpdatedEvent>().ToArray();
        Assert.All(notices, notice => Assert.Equal(student, notice.StudentId));
        Assert.Equal([StudentPlanningPermissionService.StudentPlansNotice, StudentPlanningPermissionService.TeacherPlansNotice,
            StudentPlanningPermissionService.WeeklyScheduleNotice], notices.Select(notice => notice.ChangeDescription));

        // Another teacher's student is not hers to switch.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetForStudentAsync(Guid.NewGuid(), true, default));
    }

    [Fact]
    public async Task The_state_says_when_the_plan_in_force_covers_only_part_of_the_way()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var (student, _, _) = await SeedAsync(f, withTeacher: false);
        Assert.Null((await StudentPlanningPermission.StateAsync(f.Db, student, default)).PartialPlanEnd);

        var end = new DateOnly(2026, 11, 22);
        foreach (var old in await f.Db.StudyPlanRevisions.Where(x => x.StudentId == student && x.IsActive).ToListAsync()) old.Archive();
        await f.Db.SaveChangesAsync();
        var plan = Coaching.Domain.Entities.StudyPlanRevision.Create(student, Guid.NewGuid(), 2, "Bu hafta");
        plan.SetWeeklySource(new(1, null, new(2026, 11, 16), end, true, "test-fingerprint", null, null, Partial: true));
        plan.Activate(); f.Db.Add(plan); await f.Db.SaveChangesAsync();

        Assert.Equal(end, (await StudentPlanningPermission.StateAsync(f.Db, student, default)).PartialPlanEnd);
    }

    [Fact]
    public async Task The_student_reads_her_own_state()
    {
        await using var f = new CurriculumManagementTests.Fixture(); await SeedAsync(f, withTeacher: true);
        var access = new CoachingAccessPolicy(f.User);
        var service = new StudentPlanningPermissionService(f.Db, access, new Recorder(), new CoachingStudyPlanAccess(access, new Identity(Guid.Empty), f.Scope));

        var state = await service.GetOwnAsync(default);
        Assert.Equal((true, false, false), (state.HasTeacher, state.AllowedByTeacher, state.StudentMayPlan));
    }
}
