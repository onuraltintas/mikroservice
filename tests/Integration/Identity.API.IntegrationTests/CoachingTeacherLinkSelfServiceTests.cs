using Coaching.API.Controllers;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Roadmap O12: a student sees and can leave an independent teacher; a teacher can remove an independent student.
// Links made through an institution are changed by the institution, not by either side alone.
public sealed class CoachingTeacherLinkSelfServiceTests
{
    [Fact]
    public async Task AStudentSeesTheirTeachersAndWhichOnesTheyCanLeave()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var (coach, institutionTeacher, student, institution) = await SeedAsync(f);

        var teachers = await f.Store.ListStudentTeachersAsync(student, default);

        teachers.Should().HaveCount(2);
        teachers.Single(x => x.TeacherUserId == coach).Should().Match<CoachingStudentTeacherLink>(x => x.CanLeave && x.InstitutionId == null && x.TeacherName == "Test Teacher");
        teachers.Single(x => x.TeacherUserId == institutionTeacher).Should().Match<CoachingStudentTeacherLink>(x => !x.CanLeave && x.InstitutionId == institution);
    }

    [Fact]
    public async Task InstitutionLinksCannotBeEndedByTheStudentOrTheTeacherAlone()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var (_, institutionTeacher, student, _) = await SeedAsync(f);

        (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Store.EndAssignmentAsync(student, institutionTeacher, student, true, default))).Code.Should().Be("Profiles.Conflict");
        (await Assert.ThrowsAsync<BusinessRuleException>(() => f.Store.EndAssignmentAsync(institutionTeacher, institutionTeacher, student, false, default))).Code.Should().Be("Profiles.Conflict");
        (await f.Db.CoachingTeacherStudentAssignments.SingleAsync(x => x.TeacherUserId == institutionTeacher)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ATeacherRemovingAStudentTellsTheStudent()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var (coach, _, student, _) = await SeedAsync(f); var events = new Events();

        await new CoachingProfileStore(f.Db, f.Accounts, events).EndAssignmentAsync(coach, coach, student, false, default);

        var ended = events.Messages.OfType<CoachingTeacherLinkEndedEvent>().Should().ContainSingle().Subject;
        (ended.Reason, ended.TeacherUserId, ended.StudentUserId, ended.TeacherName).Should().Be((CoachingTeacherLinkEndedReasons.TeacherRemoved, coach, student, "Test Teacher"));
        (await f.Db.CoachingTeacherStudentAssignments.SingleAsync(x => x.TeacherUserId == coach)).IsActive.Should().BeFalse();
    }

    [Fact]
    public void StudentsListTheirTeachersThroughTheApi()
    {
        var method = typeof(CoachingStudentLinksController).GetMethod(nameof(CoachingStudentLinksController.List))!;
        method.GetCustomAttributes(typeof(HttpGetAttribute), false).Should().ContainSingle();
        typeof(CoachingStudentLinksController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().Contain(x => x.Roles == "Student");
    }

    private static async Task<(Guid Coach, Guid InstitutionTeacher, Guid Student, Guid Institution)> SeedAsync(CoachingOwnedProfilePersistenceTests.Fixture f)
    {
        var coach = f.AddAccount("Teacher"); var institutionTeacher = f.AddAccount("Teacher"); var student = f.AddAccount("Student");
        var institution = Guid.NewGuid();
        foreach (var teacher in new[] { coach, institutionTeacher }) await f.Store.ProvisionAsync(teacher, "Teacher", default);
        await f.Store.ProvisionAsync(student, "Student", default);
        f.Db.AddRange(CoachingTeacherStudentAssignment.Create(coach, student, null, null, coach),
            CoachingTeacherStudentAssignment.Create(institutionTeacher, student, institution, null, institutionTeacher));
        await f.Db.SaveChangesAsync();
        return (coach, institutionTeacher, student, institution);
    }

    private sealed class Events : ICoachingEventPublisher
    {
        public List<object> Messages { get; } = [];
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class { Messages.Add(message); return Task.CompletedTask; }
    }
}
