using EduPlatform.Shared.Contracts.Reporting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Domain.Assignments;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Institutions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Review H3 (finding 10): a teacher's assignment actions re-check that the teacher is still allowed — still an active
/// teacher of the assignment's institution, and only for students they still teach.
/// </summary>
public sealed class SpeedReadingTeacherAssignmentAccessTests
{
    private readonly Guid _institution = Guid.NewGuid();
    private readonly Guid _teacher = Guid.NewGuid();
    private readonly Guid _ownStudent = Guid.NewGuid();
    private readonly Guid _formerStudent = Guid.NewGuid();
    private readonly Guid _stranger = Guid.NewGuid();

    [Fact]
    public async Task A_teacher_removed_from_the_institution_can_no_longer_use_its_assignments()
    {
        await using var db = CreateDb();
        var assignment = await SeedAssignmentAsync(db, _institution, teacherActive: false);
        var service = CreateService(db, readable: [_ownStudent]);

        (await service.GetDetailsAsync(_teacher, assignment.Id)).Should().BeNull();
        (await service.AddStudentAsync(_teacher, assignment.Id, _ownStudent)).Should().Be(AssignmentStudentMutationStatus.AssignmentNotFound);
        (await service.RemoveStudentAsync(_teacher, assignment.Id, _formerStudent)).Should().Be(AssignmentStudentMutationStatus.AssignmentNotFound);
        (await service.DeleteAsync(_teacher, assignment.Id)).Should().BeFalse();
        (await service.GetTeacherAssignmentsAsync(_teacher, 1, 25, null, null, null)).Items.Should().BeEmpty();
        (await db.Assignments.SingleAsync()).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task A_teacher_sees_and_adds_only_students_they_still_teach()
    {
        await using var db = CreateDb();
        var assignment = await SeedAssignmentAsync(db, _institution, teacherActive: true);
        var service = CreateService(db, readable: [_ownStudent]);

        var details = await service.GetDetailsAsync(_teacher, assignment.Id);
        details!.Students.Select(student => student.StudentId).Should().Equal(_ownStudent);
        (await service.AddStudentAsync(_teacher, assignment.Id, _stranger)).Should().Be(AssignmentStudentMutationStatus.StudentNotFound);
        (await service.CreateAsync(_teacher, new CreateAssignmentRequest(assignment.ExerciseId, null, [_stranger], "Yeni", null,
            DateTime.UtcNow.AddDays(3)))).Should().BeNull();
        (await service.RemoveStudentAsync(_teacher, assignment.Id, _formerStudent)).Should().Be(AssignmentStudentMutationStatus.Success);
    }

    [Fact]
    public async Task A_personal_assignment_stays_with_its_teacher()
    {
        await using var db = CreateDb();
        var assignment = await SeedAssignmentAsync(db, institutionId: null, teacherActive: false);
        var service = CreateService(db, readable: [_ownStudent, _formerStudent]);

        (await service.GetDetailsAsync(_teacher, assignment.Id))!.Students.Should().HaveCount(2);
        (await service.GetTeacherAssignmentsAsync(_teacher, 1, 25, null, null, null)).Items.Should().ContainSingle();
    }

    private static OwnedSpeedReadingDbContext CreateDb() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private async Task<Assignment> SeedAssignmentAsync(OwnedSpeedReadingDbContext db, Guid? institutionId, bool teacherActive)
    {
        var exercise = Exercise.Create("Okuma", "SpeedReading", "{}", 1, _teacher, Guid.NewGuid());
        db.Exercises.Add(exercise);
        var assignment = Assignment.Create(_teacher, exercise.Id, null, "Ödev", null, DateTime.UtcNow.AddDays(7), institutionId: institutionId);
        db.Assignments.Add(assignment);
        db.StudentAssignments.AddRange(
            StudentAssignment.Assign(assignment.Id, _ownStudent, createdBy: _teacher.ToString()),
            StudentAssignment.Assign(assignment.Id, _formerStudent, createdBy: _teacher.ToString()));
        if (institutionId.HasValue)
        {
            var membership = SpeedReadingInstitutionMembership.Create(institutionId.Value, _teacher, SpeedReadingInstitutionMemberRole.Teacher, _teacher, DateTime.UtcNow);
            if (!teacherActive)
                membership.SetActive(false, Guid.NewGuid(), DateTime.UtcNow);
            db.InstitutionMemberships.Add(membership);
        }
        await db.SaveChangesAsync();
        return assignment;
    }

    private static ISpeedReadingAssignments CreateService(OwnedSpeedReadingDbContext db, Guid[] readable)
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssignments")!;
        return (ISpeedReadingAssignments)Activator.CreateInstance(type, db, new Directory(), new TeacherAccess(readable))!;
    }

    private sealed class Directory : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse(
                userIds.Select(id => new SpeedReadingUserDirectoryItem(id, "Öğrenci", "Bir", true)).ToArray()));
        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(string? role, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class TeacherAccess(Guid[] readable) : ISpeedReadingTeacherAccess
    {
        public Task<IReadOnlySet<Guid>> GetReadableStudentIdsAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentUserIds,
            Guid? targetTeacherUserId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(studentUserIds.Where(readable.Contains).ToHashSet());
        public Task<bool> CanReadStudentAsync(Guid viewerUserId, Guid studentUserId, Guid? targetTeacherUserId = null,
            bool isSystemAdmin = false, bool isInstitutionAdmin = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(readable.Contains(studentUserId));
        public Task<SpeedReadingTeacherStudentScopeResponse?> GetStudentScopeAsync(Guid viewerUserId, Guid? targetTeacherUserId = null,
            bool isSystemAdmin = false, bool isInstitutionAdmin = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<SpeedReadingTeacherStudentScopeResponse?> GetInstitutionStudentScopeAsync(Guid viewerUserId, Guid institutionId,
            bool isSystemAdmin = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> CanReadInstitutionStudentAsync(Guid viewerUserId, Guid institutionId, Guid studentUserId,
            bool isSystemAdmin = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
