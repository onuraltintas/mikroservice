using FluentAssertions;
using SpeedReading.Domain.Assignments;
using SpeedReading.Application.Assignments;
using SpeedReading.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Contracts.Reporting;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Institutions;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingInstitutionAssignmentTests
{
    [Fact]
    public void Institution_assignment_keeps_a_stable_tenant_scope()
    {
        var institutionId = Guid.NewGuid();
        var assignment = Assignment.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "Reading", null,
            DateTime.UtcNow.AddDays(7), institutionId: institutionId);

        assignment.InstitutionId.Should().Be(institutionId);
    }

    [Fact]
    public async Task Institution_list_excludes_assignments_from_other_tenants_and_unscoped_teachers()
    {
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        await using var db = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Assignments.AddRange(
            Assignment.Create(teacherId, Guid.NewGuid(), null, "Own", null,
                DateTime.UtcNow.AddDays(7), institutionId: institutionId),
            Assignment.Create(teacherId, Guid.NewGuid(), null, "Other", null,
                DateTime.UtcNow.AddDays(7), institutionId: Guid.NewGuid()),
            Assignment.Create(teacherId, Guid.NewGuid(), null, "Unscoped", null,
                DateTime.UtcNow.AddDays(7)));
        await db.SaveChangesAsync();
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssignments")!;
        var service = (ISpeedReadingAssignments)Activator.CreateInstance(type, db, null)!;

        var page = await service.GetInstitutionAssignmentsAsync(
            institutionId, 1, 25, null, null, null, null);

        page.Items.Select(item => item.Title).Should().Equal("Own");
    }

    [Fact]
    public async Task Institution_create_rejects_teacher_without_active_membership()
    {
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        await using var db = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssignments")!;
        var service = (ISpeedReadingAssignments)Activator.CreateInstance(type, db, null)!;

        var id = await service.CreateForInstitutionAsync(institutionId, teacherId,
            Guid.NewGuid(), new CreateAssignmentRequest(Guid.NewGuid(), null, [],
                "Reading", null, DateTime.UtcNow.AddDays(7)), CancellationToken.None);

        id.Should().BeNull();
    }

    [Fact]
    public async Task Institution_delete_cannot_deactivate_another_tenants_assignment()
    {
        var institutionId = Guid.NewGuid();
        var assignment = Assignment.Create(Guid.NewGuid(), Guid.NewGuid(), null,
            "Other", null, DateTime.UtcNow.AddDays(7), institutionId: Guid.NewGuid());
        await using var db = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssignments")!;
        var service = (ISpeedReadingAssignments)Activator.CreateInstance(type, db, null)!;

        var deleted = await service.DeleteInstitutionAsync(
            institutionId, assignment.Id, CancellationToken.None);

        deleted.Should().BeFalse();
        assignment.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Institution_create_links_only_active_teacher_student_roster()
    {
        var institutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var exercise = Exercise.Create("Reading", "SpeedReading", "{}", 1, actorId, Guid.NewGuid());
        await using var db = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Exercises.Add(exercise);
        db.InstitutionMemberships.AddRange(
            SpeedReadingInstitutionMembership.Create(institutionId, teacherId,
                SpeedReadingInstitutionMemberRole.Teacher, actorId, DateTime.UtcNow),
            SpeedReadingInstitutionMembership.Create(institutionId, studentId,
                SpeedReadingInstitutionMemberRole.Student, actorId, DateTime.UtcNow));
        db.TeacherStudentAssignments.Add(SpeedReadingTeacherStudentAssignment.Create(
            institutionId, teacherId, studentId, actorId, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssignments")!;
        var service = (ISpeedReadingAssignments)Activator.CreateInstance(type, db,
            new TestDirectory(studentId))!;

        var id = await service.CreateForInstitutionAsync(institutionId, teacherId,
            actorId, new CreateAssignmentRequest(exercise.Id, null, [studentId],
                "Reading", null, DateTime.UtcNow.AddDays(7)));

        id.Should().NotBeNull();
        db.Assignments.Single().InstitutionId.Should().Be(institutionId);
        db.StudentAssignments.Single().StudentId.Should().Be(studentId);
    }

    private sealed class TestDirectory(Guid studentId) : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse(
                userIds.Where(id => id == studentId)
                    .Select(id => new SpeedReadingUserDirectoryItem(id, "Student", "One", true)).ToArray()));

        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(
            string? role, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([studentId]);
    }
}
