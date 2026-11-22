using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

/// <summary>KocApp uyumu (22.11.2026): the student's books and sets, their progress and the next part to plan.</summary>
public sealed class StudentResourceTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    private static StudentResourceDetails Book(int total = 100, int doneBefore = 0, string unit = StudentResourceUnits.Page,
        params StudentResourceSection[] sections) =>
        new("TEST Matematik Soru Bankası", "TEST Yayınları", "Matematik", StudentResourceKinds.QuestionBank, unit, total, doneBefore, sections);

    [Fact]
    public void Progress_counts_every_unit_once_from_what_was_done_before_and_the_completed_ranges()
    {
        Assert.Equal(42, StudentResource.Covered(100, 10, [(5, 20), (15, 30), (50, 50), (90, 120)]));
        Assert.Equal(0, StudentResource.Covered(100, 0, []));
        Assert.Equal(100, StudentResource.Covered(100, 100, [(1, 5)]));
        Assert.Equal(3, StudentResource.Covered(10, 0, [(8, 10), (9, 9)]));
    }

    [Fact]
    public void A_resource_checks_its_name_unit_total_and_sections()
    {
        var student = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => StudentResource.Create(student, student, Book() with { Name = " " }, Now));
        Assert.Throws<ArgumentException>(() => StudentResource.Create(student, student, Book(unit: "Chapter"), Now));
        Assert.Throws<ArgumentException>(() => StudentResource.Create(student, student, Book(total: 0), Now));
        Assert.Throws<ArgumentException>(() => StudentResource.Create(student, student, Book(total: 10, doneBefore: 11), Now));
        var outside = Assert.Throws<ArgumentException>(() => StudentResource.Create(student, student, Book(10, 0, StudentResourceUnits.Page, new("Ünite 1", 5, 11)), Now));
        Assert.Contains("1–10", outside.Message);
        var resource = StudentResource.Create(student, student, Book(20, 0, StudentResourceUnits.Test, new("Ünite 2", 11, 20), new(" Ünite 1 ", 1, 10)), Now);
        Assert.Equal(new[] { "Ünite 1", "Ünite 2" }, resource.Sections.Select(x => x.Title));
        Assert.True(resource.Contains(1, 20));
        Assert.False(resource.Contains(0, 3));
        Assert.False(resource.Contains(5, 21));
    }

    [Fact]
    public void The_next_part_is_the_first_free_part_of_a_section_or_a_usual_sitting_without_sections()
    {
        var student = Guid.NewGuid();
        var sectioned = StudentResource.Create(student, student, Book(20, 0, StudentResourceUnits.Page, new("Ünite 1", 1, 10), new("Ünite 2", 11, 20)), Now);
        var next = CoachingStudentResourceService.Next(sectioned, [(1, 10)], [(11, 12)]);
        Assert.Equal(new StudentResourceNext(13, 20, "Ünite 2", null), next);
        Assert.Null(CoachingStudentResourceService.Next(sectioned, [(1, 20)], []));

        var tests = StudentResource.Create(student, student, Book(10, 3, StudentResourceUnits.Test), Now);
        Assert.Equal(new StudentResourceNext(4, 5, null, null), CoachingStudentResourceService.Next(tests, [], []));
        Assert.Equal(new StudentResourceNext(4, 4, null, null), CoachingStudentResourceService.Next(tests, [], [(5, 6)]));
        var pages = StudentResource.Create(student, student, Book(100), Now);
        Assert.Equal(new StudentResourceNext(1, 10, null, null), CoachingStudentResourceService.Next(pages, [], []));
    }

    [Fact]
    public void A_task_points_at_a_range_of_its_own_students_resource_and_its_copies_keep_it()
    {
        var student = Guid.NewGuid();
        var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Plan");
        var resource = StudentResource.Create(student, student, Book(50), Now);
        var other = StudentResource.Create(Guid.NewGuid(), student, Book(50), Now);
        var task = StudyPlanTask.Create(plan, new(2026, 11, 23), "Çarpanlar", 40, adviceKey: StudyPlanTask.PracticeReviewKeyPrefix + "abc");
        Assert.Throws<ArgumentException>(() => task.LinkResource(other, 1, 5));
        Assert.Throws<ArgumentException>(() => task.LinkResource(resource, 40, 60));
        Assert.Throws<ArgumentException>(() => task.LinkResource(resource, 3, null));
        Assert.Throws<ArgumentException>(() => task.LinkResource(null, 1, 2));
        task.LinkResource(resource, 12, 15);
        Assert.Equal((resource.Id, 12, 15), (task.ResourceId!.Value, task.ResourceFrom!.Value, task.ResourceTo!.Value));
        var copy = StudyPlanTask.Create(plan, new(2026, 11, 24), "Çarpanlar", 20);
        copy.KeepLinksOf(task);
        Assert.Equal((task.ResourceId, task.ResourceFrom, task.ResourceTo, task.AdviceKey), (copy.ResourceId, copy.ResourceFrom, copy.ResourceTo, copy.AdviceKey));
        Assert.Null(copy.SourceName);
        task.LinkResource(resource, null, null);
        Assert.Equal((resource.Id, (int?)null), (task.ResourceId!.Value, task.ResourceFrom));
        task.LinkResource(null, null, null);
        Assert.Null(task.ResourceId);
        Assert.Equal("TEST Kitap, test 12–15", StudentResourceNames.Text("TEST Kitap", StudentResourceUnits.Test, 12, 15));
        Assert.Equal("TEST Kitap, sayfa 7", StudentResourceNames.Text("TEST Kitap", StudentResourceUnits.Page, 7, 7));
        Assert.Equal("TEST Kitap", StudentResourceNames.Text("TEST Kitap", StudentResourceUnits.Question, null, null));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task The_student_keeps_her_resources_and_sees_the_completed_task_range_as_progress()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var db = pg.Db; var topic = await pg.Data.Seed();
        var task = await db.StudyPlanTasks.SingleAsync(); var student = task.StudentId;
        pg.Data.User.UserId = student; pg.Data.User.Roles = ["Student"];
        var access = new CoachingAccessPolicy(pg.Data.User);
        var service = new CoachingStudentResourceService(db, access, new CoachingStudyPlanAccess(access, new Identity(student), pg.Data.Scope), TimeProvider.System);

        Assert.Equal(student, service.RequireStudent());
        var created = await service.CreateAsync(student, new StudentResourceInput("TEST Matematik Soru Bankası", "TEST Yayınları", "Matematik",
            StudentResourceKinds.QuestionBank, StudentResourceUnits.Test, 40, 2), default);
        Assert.Equal((2, 5, 0, true), (created.DoneUnits, created.Percent, created.PlannedUnits, created.CanDelete));
        Assert.Equal(new StudentResourceNext(3, 4, null, null), created.Next);

        var resource = await db.Set<StudentResource>().SingleAsync();
        task.LinkResource(resource, 3, 6); await db.SaveChangesAsync();
        var listed = Assert.Single(await service.ListAsync(student, false, default));
        Assert.Equal((6, 15, false), (listed.DoneUnits, listed.Percent, listed.CanDelete));
        Assert.Equal(new StudentResourceNext(7, 8, null, null), listed.Next);

        var shorter = await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(student, listed.Id,
            new StudentResourceInput(listed.Name, null, null, listed.Kind, listed.Unit, 5, 0, null, listed.Version), default));
        Assert.Equal("StudyPlanning.Conflict", shorter.Code);
        Assert.Equal("StudyPlanning.Conflict", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(student, listed.Id, listed.Version, default))).Code);
        var stale = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetArchivedAsync(student, listed.Id, new(listed.Version + 5, true), default));
        Assert.Equal("StudyPlanning.Conflict", stale.Code);
        var archived = await service.SetArchivedAsync(student, listed.Id, new(listed.Version, true), default);
        Assert.True(archived.IsArchived); Assert.Null(archived.Next);
        Assert.Empty(await service.ListAsync(student, false, default));
        Assert.Single(await service.ListAsync(student, true, default));

        // The plan view names the resource and its range next to the task.
        var reader = await db.StudyPlanTasks.AsNoTracking().SingleAsync();
        var names = await StudentResourceNames.ForAsync(db, student, [reader], default);
        Assert.Equal("TEST Matematik Soru Bankası", names[resource.Id].Title);

        // KocApp plan, phase 2: her teacher gives the same book to another student of hers, once.
        var teacher = Guid.NewGuid(); var other = Guid.NewGuid();
        pg.Data.User.UserId = teacher; pg.Data.User.Roles = ["Teacher"];
        var teacherAccess = new CoachingAccessPolicy(pg.Data.User);
        var teacherService = new CoachingStudentResourceService(db, teacherAccess,
            new CoachingStudyPlanAccess(teacherAccess, new Identity(student, other), pg.Data.Scope), TimeProvider.System);
        Assert.Equal(student, await teacherService.RequireTeacherWriteAsync(student, default));
        Assert.Equal(new StudentResourceCopyResult(1, 0), await teacherService.CopyAsync(student, resource.Id, new([other, student]), default));
        Assert.Equal(new StudentResourceCopyResult(0, 1), await teacherService.CopyAsync(student, resource.Id, new([other]), default));
        var copy = Assert.Single(await teacherService.ListAsync(other, false, default));
        Assert.Equal(("TEST Matematik Soru Bankası", 0, 40), (copy.Name, copy.DoneUnits, copy.TotalUnits));
        Assert.Equal("Authorization.Forbidden", (await Assert.ThrowsAsync<BusinessRuleException>(
            () => teacherService.CopyAsync(student, resource.Id, new([Guid.NewGuid()]), default))).Code);

        // KocApp plan, phase 2: a new plan's tasks of a topic take the next free parts of the section tied to that topic.
        var book = StudentResource.Create(student, student, new("TEST Konu Kitabı", null, "Matematik", StudentResourceKinds.TopicBook,
            StudentResourceUnits.Page, 30, 3, [new StudentResourceSection("Çarpanlar", 1, 15, topic.Id), new StudentResourceSection("Üslü sayılar", 16, 30)]), Now);
        db.Add(book); await db.SaveChangesAsync();
        var plan = await db.StudyPlanRevisions.SingleAsync();
        var first = StudyPlanTask.Create(plan, new(2026, 11, 23), "Çarpanlar", 40, topic.Id);
        var second = StudyPlanTask.Create(plan, new(2026, 11, 24), "Çarpanlar", 40, topic.Id);
        var third = StudyPlanTask.Create(plan, new(2026, 11, 25), "Çarpanlar", 40, topic.Id);
        var free = StudyPlanTask.Create(plan, new(2026, 11, 23), "Serbest", 30);
        await StudentResourcePlanLinks.LinkAsync(db, student, [second, first, third, free], default);
        Assert.Equal((book.Id, 4, 13), (first.ResourceId!.Value, first.ResourceFrom!.Value, first.ResourceTo!.Value));
        Assert.Equal((book.Id, 14, 15), (second.ResourceId!.Value, second.ResourceFrom!.Value, second.ResourceTo!.Value));
        Assert.Null(third.ResourceId);
        Assert.Null(free.ResourceId);

        // The teacher chose a resource for the lesson: its tasks take that resource's free units in turn.
        var bank = StudentResource.Create(student, student, new("TEST Soru Bankası", null, "Matematik", StudentResourceKinds.QuestionBank,
            StudentResourceUnits.Test, 5, 0, null, topic.LessonId, "LGS"), Now);
        db.Add(bank); await db.SaveChangesAsync();
        Assert.Equal((topic.LessonId, "LGS"), (bank.LessonId!.Value, bank.ExamCode));
        var a = StudyPlanTask.Create(plan, new(2026, 11, 26), "Çarpanlar", 40, topic.Id); a.SetWeeklyPlacement(topic.LessonId, "Matematik", 540);
        var b = StudyPlanTask.Create(plan, new(2026, 11, 27), "Çarpanlar", 40, topic.Id); b.SetWeeklyPlacement(topic.LessonId, "Matematik", 540);
        var c = StudyPlanTask.Create(plan, new(2026, 11, 28), "Deneme çözümü", 40); c.SetWeeklyPlacement(topic.LessonId, "Matematik", 540);
        await StudentResourcePlanLinks.LinkAsync(db, student, [a, b, c], default, [new WeeklyResourceChoice(topic.LessonId, bank.Id)]);
        Assert.Equal((bank.Id, 1, 2), (a.ResourceId!.Value, a.ResourceFrom!.Value, a.ResourceTo!.Value));
        Assert.Equal((bank.Id, 3, 4), (b.ResourceId!.Value, b.ResourceFrom!.Value, b.ResourceTo!.Value));
        Assert.Equal((bank.Id, 5, 5), (c.ResourceId!.Value, c.ResourceFrom!.Value, c.ResourceTo!.Value));
        await Assert.ThrowsAsync<ArgumentException>(() => StudentResourcePlanLinks.LinkAsync(db, student, [], default,
            [new WeeklyResourceChoice(topic.LessonId, Guid.NewGuid())]));
    }

    private sealed class Identity(params Guid[] students) : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid id, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacher, IReadOnlyCollection<Guid> ids, Guid? institution, bool admin, CancellationToken ct)
            => ids.All(students.Contains) ? Task.FromResult(institution) : throw new BusinessRuleException("Authorization.Forbidden", "Denied");
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewer, IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult(ids);
    }
}
