using System.Reflection;
using EduPlatform.Shared.Contracts.Reporting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Domain.Assignments;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class OwnedSpeedReadingTeacherReportsTests
{
    [Fact]
    public async Task Teacher_assignment_report_uses_only_the_teachers_active_roster_and_real_results()
    {
        var teacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();
        var studentA = Guid.NewGuid();
        var studentB = Guid.NewGuid();
        var outOfScopeStudent = Guid.NewGuid();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDb();

        var first = AddAssignment(db, teacherId, "Ödev 1", now.AddDays(-3));
        var second = AddAssignment(db, teacherId, "Ödev 2", now.AddDays(-2));
        var unrelatedTeacherAssignment = AddAssignment(db, otherTeacherId, "Başka öğretmen", now.AddDays(-1));
        var unrelatedStudentAssignment = AddAssignment(db, teacherId, "Kapsam dışı öğrenci", now.AddDays(-1));

        var completed = StudentAssignment.Assign(first.Id, studentA, assignedAt: now.AddDays(-3));
        completed.Complete(Guid.NewGuid(), 90, 75, now.AddDays(-1));
        var inProgress = StudentAssignment.Assign(second.Id, studentA, assignedAt: now.AddDays(-2));
        var notStarted = StudentAssignment.Assign(first.Id, studentB, assignedAt: now.AddDays(-3));
        var unrelatedTeacherRow = StudentAssignment.Assign(unrelatedTeacherAssignment.Id, studentA, assignedAt: now.AddDays(-1));
        var unrelatedStudentRow = StudentAssignment.Assign(unrelatedStudentAssignment.Id, outOfScopeStudent, assignedAt: now.AddDays(-1));
        db.StudentAssignments.AddRange(completed, inProgress, notStarted, unrelatedTeacherRow, unrelatedStudentRow);
        await db.SaveChangesAsync();

        var report = CreateReports(db, new TestUserDirectory(
            new SpeedReadingUserDirectoryItem(studentA, "Ayşe", "Yılmaz", true),
            new SpeedReadingUserDirectoryItem(studentB, "Can", "Demir", true)))
            .GetAssignmentsAsync(
                new SpeedReadingTeacherStudentScopeResponse(
                    [], [studentA, studentB], 2, [], ReportingTeacherUserId: teacherId),
                now.AddDays(-7),
                now);

        var result = await report;

        result.DataAvailable.Should().BeTrue();
        result.UnavailableReason.Should().BeNull();
        result.AssignmentCount.Should().Be(2);
        result.CompletionStats.Should().BeEquivalentTo(new TeacherAssignmentCompletionStats(
            TotalStudents: 2,
            Completed: 0,
            InProgress: 1,
            NotStarted: 1,
            CompletionRate: 33.33m));
        result.PerformanceStats.Should().BeEquivalentTo(new TeacherAssignmentPerformanceStats(
            AverageScore: 90,
            MedianScore: 90,
            HighestScore: 90,
            LowestScore: 90,
            StandardDeviation: 0));
        result.StudentBreakdown.Should().BeEquivalentTo(
        [
            new TeacherAssignmentStudentBreakdown(studentA, "Ayşe Yılmaz", "in-progress", 90, null, now.AddDays(-1)),
            new TeacherAssignmentStudentBreakdown(studentB, "Can Demir", "not-started", null, null, null)
        ]);
        result.ScoreDistribution.Should().ContainSingle(item =>
            item.Name == "90-99" && item.Series.Single().Value == 1);
        result.TimeStats.Should().BeNull();
    }

    [Fact]
    public async Task Institution_assignment_report_includes_only_active_local_teachers_and_students()
    {
        var institutionId = Guid.NewGuid();
        var otherInstitutionId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var outsideStudentId = Guid.NewGuid();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDb();
        db.InstitutionMemberships.AddRange(
            Membership(institutionId, teacherId, SpeedReadingInstitutionMemberRole.Teacher),
            Membership(otherInstitutionId, otherTeacherId, SpeedReadingInstitutionMemberRole.Teacher),
            Membership(institutionId, studentId, SpeedReadingInstitutionMemberRole.Student),
            Membership(otherInstitutionId, outsideStudentId, SpeedReadingInstitutionMemberRole.Student));

        var localAssignment = AddAssignment(db, teacherId, "Kurum ödevi", now.AddDays(-1));
        var externalTeacherAssignment = AddAssignment(db, otherTeacherId, "Başka kurum ödevi", now.AddDays(-1));
        var inactiveTeacherId = Guid.NewGuid();
        var inactiveTeacherMembership = Membership(institutionId, inactiveTeacherId, SpeedReadingInstitutionMemberRole.Teacher);
        inactiveTeacherMembership.SetActive(false, Guid.NewGuid(), now);
        db.InstitutionMemberships.Add(inactiveTeacherMembership);
        var inactiveTeacherAssignment = AddAssignment(db, inactiveTeacherId, "Pasif öğretmen ödevi", now.AddDays(-1));
        db.StudentAssignments.AddRange(
            StudentAssignment.Assign(localAssignment.Id, studentId, assignedAt: now.AddDays(-1)),
            StudentAssignment.Assign(externalTeacherAssignment.Id, studentId, assignedAt: now.AddDays(-1)),
            StudentAssignment.Assign(inactiveTeacherAssignment.Id, studentId, assignedAt: now.AddDays(-1)),
            StudentAssignment.Assign(localAssignment.Id, outsideStudentId, assignedAt: now.AddDays(-1)));
        await db.SaveChangesAsync();

        var result = await CreateReports(db, new TestUserDirectory())
            .GetAssignmentsAsync(
                new SpeedReadingTeacherStudentScopeResponse([institutionId], [], 1),
                now.AddDays(-7),
                now);

        result.DataAvailable.Should().BeTrue();
        result.AssignmentCount.Should().Be(1);
        result.CompletionStats!.TotalStudents.Should().Be(1);
        result.StudentBreakdown.Should().ContainSingle(item => item.StudentId == studentId);
    }

    [Fact]
    public async Task Assignment_report_does_not_count_completion_after_the_report_period_end()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDb();
        var assignment = AddAssignment(db, teacherId, "Dönem sonrası tamamlandı", now.AddDays(-2));
        var studentAssignment = StudentAssignment.Assign(assignment.Id, studentId, assignedAt: now.AddDays(-2));
        studentAssignment.Complete(Guid.NewGuid(), 85, 70, now.AddDays(1));
        db.StudentAssignments.Add(studentAssignment);
        await db.SaveChangesAsync();

        var result = await CreateReports(db, new TestUserDirectory())
            .GetAssignmentsAsync(
                new SpeedReadingTeacherStudentScopeResponse([], [studentId], 1, [], ReportingTeacherUserId: teacherId),
                now.AddDays(-7),
                now);

        result.DataAvailable.Should().BeTrue();
        result.CompletionStats!.Completed.Should().Be(0);
        result.CompletionStats.NotStarted.Should().Be(1);
        result.PerformanceStats.Should().BeNull();
        result.ScoreDistribution.Should().BeEmpty();
        result.StudentBreakdown.Single().Score.Should().BeNull();
    }

    [Fact]
    public async Task Assignment_report_derives_completion_time_from_the_saved_result_not_a_guess()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDb();
        var assignment = AddAssignment(db, teacherId, "Süreli ödev", now.AddDays(-2));
        var resultId = Guid.NewGuid();
        var studentAssignment = StudentAssignment.Assign(assignment.Id, studentId, assignedAt: now.AddDays(-2));
        studentAssignment.Complete(resultId, 80, 65, now.AddDays(-1));
        db.StudentAssignments.Add(studentAssignment);

        var exerciseId = Guid.NewGuid();
        var session = ExerciseSession.Start(studentId, exerciseId, null, 1, now.AddDays(-1).AddSeconds(-50), null, studentAssignment.Id);
        session.Pause(now.AddDays(-1).AddSeconds(-40));
        session.Resume(now.AddDays(-1).AddSeconds(-20));
        session.Complete(now.AddDays(-1));
        db.ExerciseSessions.Add(session);
        db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(
            resultId,
            session.Id,
            studentId,
            exerciseId,
            null,
            10,
            30,
            100,
            80,
            65,
            80,
            now.AddDays(-1)));
        await db.SaveChangesAsync();

        var result = await CreateReports(db, new TestUserDirectory())
            .GetAssignmentsAsync(
                new SpeedReadingTeacherStudentScopeResponse([], [studentId], 1, [], ReportingTeacherUserId: teacherId),
                now.AddDays(-7),
                now);

        result.StudentBreakdown.Single().CompletionTime.Should().Be(30);
        result.TimeStats.Should().BeEquivalentTo(new TeacherAssignmentTimeStats(30, 30, 30, 30));
    }

    [Fact]
    public async Task Assignment_report_ignores_result_duration_when_result_owner_does_not_match_student()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDb();
        var assignment = AddAssignment(db, teacherId, "Uyumsuz sonuç", now.AddDays(-2));
        var resultId = Guid.NewGuid();
        var studentAssignment = StudentAssignment.Assign(assignment.Id, studentId, assignedAt: now.AddDays(-2));
        studentAssignment.Complete(resultId, 80, 65, now.AddDays(-1));
        db.StudentAssignments.Add(studentAssignment);
        db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(
            resultId,
            Guid.NewGuid(),
            otherStudentId,
            Guid.NewGuid(),
            null,
            10,
            30,
            100,
            80,
            65,
            80,
            now.AddDays(-1)));
        await db.SaveChangesAsync();

        var result = await CreateReports(db, new TestUserDirectory())
            .GetAssignmentsAsync(
                new SpeedReadingTeacherStudentScopeResponse([], [studentId], 1, [], ReportingTeacherUserId: teacherId),
                now.AddDays(-7),
                now);

        result.StudentBreakdown.Single().CompletionTime.Should().BeNull();
        result.TimeStats.Should().BeNull();
    }

    private static OwnedSpeedReadingDbContext CreateDb() => new(
        new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Assignment AddAssignment(
        OwnedSpeedReadingDbContext db,
        Guid teacherId,
        string title,
        DateTime createdAt)
    {
        var assignment = Assignment.Create(
            teacherId,
            Guid.NewGuid(),
            null,
            title,
            null,
            createdAt.AddDays(7),
            createdAt: createdAt);
        db.Assignments.Add(assignment);
        return assignment;
    }

    private static SpeedReadingInstitutionMembership Membership(
        Guid institutionId,
        Guid userId,
        SpeedReadingInstitutionMemberRole role) =>
        SpeedReadingInstitutionMembership.Create(institutionId, userId, role, Guid.NewGuid(), DateTime.UtcNow);

    private static ILegacySpeedReadingTeacherReports CreateReports(
        OwnedSpeedReadingDbContext db,
        ISpeedReadingUserDirectory userDirectory)
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingTeacherReports",
            throwOnError: true)!;
        return (ILegacySpeedReadingTeacherReports)Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [db, userDirectory],
            culture: null)!;
    }

    private sealed class TestUserDirectory(params SpeedReadingUserDirectoryItem[] users)
        : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse(
                users.Where(user => userIds.Contains(user.UserId)).ToArray()));

        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(
            string? role,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
