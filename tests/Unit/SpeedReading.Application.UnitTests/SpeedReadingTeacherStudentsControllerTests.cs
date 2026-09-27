using System.Security.Claims;
using System.Text.Json;
using EduPlatform.Shared.Contracts.Reporting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.API.Controllers;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Institutions;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingTeacherStudentsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetMyStudents_search_filters_full_roster_before_pagination()
    {
        var teacherId = Guid.NewGuid();
        var firstAdaId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        var secondAdaId = Guid.NewGuid();
        var controller = CreateController(
            teacherId,
            [
                RosterRecord(teacherId, firstAdaId),
                RosterRecord(teacherId, bobId),
                RosterRecord(teacherId, secondAdaId)
            ],
            [
                User(firstAdaId, "Ada", "Yılmaz", "ada1@example.test"),
                User(bobId, "Bora", "Yılmaz", "bora@example.test"),
                User(secondAdaId, "Ada", "Demir", "ada2@example.test")
            ]);

        var firstPageResult = await controller.GetMyStudents(1, 1, searchTerm: "  Ada  ");
        var secondPageResult = await controller.GetMyStudents(2, 1, searchTerm: "Ada");

        ReadPage(firstPageResult).Should().BeEquivalentTo((2, firstAdaId));
        ReadPage(secondPageResult).Should().BeEquivalentTo((2, secondAdaId));
    }

    [Fact]
    public async Task GetMyStudents_inactive_filter_uses_identity_and_speed_reading_profile_status()
    {
        var teacherId = Guid.NewGuid();
        var activeId = Guid.NewGuid();
        var disabledIdentityId = Guid.NewGuid();
        var inactiveProfileId = Guid.NewGuid();
        var roster = new[]
        {
            RosterRecord(teacherId, activeId, isProfileActive: true),
            RosterRecord(teacherId, disabledIdentityId, isProfileActive: true),
            RosterRecord(teacherId, inactiveProfileId, isProfileActive: false)
        };
        var users = new[]
        {
            User(activeId, "Ada", "Yılmaz", "ada@example.test"),
            User(disabledIdentityId, "Bora", "Yılmaz", "bora@example.test", isActive: false),
            User(inactiveProfileId, "Can", "Demir", "can@example.test")
        };
        var controller = CreateController(teacherId, roster, users);

        var result = await controller.GetMyStudents(1, 10, isActive: false);

        ReadIds(result).Should().BeEquivalentTo([disabledIdentityId, inactiveProfileId]);
    }

    private static SpeedReadingTeacherStudentsController CreateController(
        Guid teacherId,
        IReadOnlyList<SpeedReadingTeacherStudentRosterRecord> roster,
        IReadOnlyList<SpeedReadingUserDirectoryItem> users)
    {
        var institutionDirectory = new TestInstitutionDirectory();
        var userDirectory = new TestUserDirectory(users);
        var controller = new SpeedReadingTeacherStudentsController(
            new TestAssignments(roster),
            institutionDirectory,
            userDirectory)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, teacherId.ToString())], "test"))
                }
            }
        };
        return controller;
    }

    private static SpeedReadingTeacherStudentRosterRecord RosterRecord(
        Guid teacherId,
        Guid studentId,
        bool? isProfileActive = true) => new(
            null, teacherId, studentId, DateTime.UtcNow, 3, 8, 180, 75, 20, null, isProfileActive);

    private static SpeedReadingUserDirectoryItem User(
        Guid userId,
        string firstName,
        string lastName,
        string email,
        bool isActive = true) => new(userId, firstName, lastName, isActive) { Email = email };

    private static (int TotalCount, Guid FirstStudentId) ReadPage(IActionResult action)
    {
        var response = action.Should().BeOfType<OkObjectResult>().Subject;
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response.Value, JsonOptions));
        var root = document.RootElement;
        return (root.GetProperty("totalCount").GetInt32(),
            root.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    private static IReadOnlyList<Guid> ReadIds(IActionResult action)
    {
        var response = action.Should().BeOfType<OkObjectResult>().Subject;
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response.Value, JsonOptions));
        return document.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();
    }

    private sealed class TestInstitutionDirectory : ISpeedReadingInstitutionDirectory
    {
        public Task<SpeedReadingInstitutionScopeResponse> GetInstitutionsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingInstitutionScopeResponse([]));
    }

    private sealed class TestUserDirectory(IReadOnlyList<SpeedReadingUserDirectoryItem> users)
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

    private sealed class TestAssignments(IReadOnlyList<SpeedReadingTeacherStudentRosterRecord> roster)
        : ISpeedReadingTeacherStudentAssignments
    {
        public Task<SpeedReadingTeacherStudentRosterPage> GetTeacherRosterAsync(
            Guid teacherUserId,
            IReadOnlyCollection<Guid> activeInstitutionIds,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default,
            int? gradeLevel = null,
            Guid? studentUserId = null)
        {
            var filtered = roster
                .Where(item => !gradeLevel.HasValue || item.GradeLevel == gradeLevel.Value)
                .Where(item => !studentUserId.HasValue || item.StudentUserId == studentUserId.Value)
                .ToArray();
            var items = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray();
            return Task.FromResult(new SpeedReadingTeacherStudentRosterPage(
                items, filtered.Length, pageNumber, pageSize));
        }

        public Task<SpeedReadingTeacherStudentAssignmentResult> AssignAsync(
            Guid? institutionId, Guid teacherUserId, Guid studentUserId, Guid actorId, DateTime at,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SpeedReadingTeacherStudentAssignmentPage> GetStudentsAsync(
            Guid institutionId, Guid teacherUserId, int pageNumber, int pageSize,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> RemoveAsync(
            Guid? institutionId, Guid teacherUserId, Guid studentUserId, Guid actorId, DateTime at,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlySet<Guid>> GetStudentUserIdsAsync(
            Guid teacherUserId, IReadOnlyCollection<Guid> requestedStudentUserIds,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
