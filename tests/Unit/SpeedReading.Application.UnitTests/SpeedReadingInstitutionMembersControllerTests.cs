using System.Reflection;
using System.Security.Claims;
using EduPlatform.Shared.Contracts.Reporting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.API.Controllers;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingInstitutionMembersControllerTests
{
    [Fact]
    public async Task Active_status_filter_uses_membership_identity_and_profile_status_before_paging()
    {
        var institutionId = Guid.NewGuid();
        var activeTeacher = Guid.NewGuid();
        var inactiveProfileTeacher = Guid.NewGuid();
        var disabledAccountTeacher = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var membershipStore = new StubMemberships(
        [
            Record(institutionId, activeTeacher, true, now),
            Record(institutionId, inactiveProfileTeacher, false, now),
            Record(institutionId, disabledAccountTeacher, true, now)
        ]);
        var userDirectory = new StubUserDirectory(
            User(activeTeacher, true),
            User(inactiveProfileTeacher, true),
            User(disabledAccountTeacher, false));
        var controller = CreateController(institutionId, membershipStore, userDirectory);

        var response = await controller.GetMembers(
            institutionId,
            pageNumber: 1,
            pageSize: 10,
            role: SpeedReadingInstitutionMemberRole.Teacher,
            isActive: true);

        var (items, totalCount) = ReadPage(response);
        membershipStore.LastRequestedActiveFilter.Should().BeNull();
        items.Select(item => item.UserId).Should().BeEquivalentTo([activeTeacher]);
        items.Should().OnlyContain(item => item.IsActive);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task Inactive_status_filter_includes_profile_and_identity_deactivated_memberships()
    {
        var institutionId = Guid.NewGuid();
        var activeTeacher = Guid.NewGuid();
        var inactiveProfileTeacher = Guid.NewGuid();
        var disabledAccountTeacher = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var membershipStore = new StubMemberships(
        [
            Record(institutionId, activeTeacher, true, now),
            Record(institutionId, inactiveProfileTeacher, false, now),
            Record(institutionId, disabledAccountTeacher, true, now)
        ]);
        var userDirectory = new StubUserDirectory(
            User(activeTeacher, true),
            User(inactiveProfileTeacher, true),
            User(disabledAccountTeacher, false));
        var controller = CreateController(institutionId, membershipStore, userDirectory);

        var response = await controller.GetMembers(
            institutionId,
            pageNumber: 2,
            pageSize: 1,
            role: SpeedReadingInstitutionMemberRole.Teacher,
            isActive: false);

        var (items, totalCount) = ReadPage(response);
        items.Should().ContainSingle().Which.UserId.Should().Be(disabledAccountTeacher);
        items.Should().OnlyContain(item => !item.IsActive);
        totalCount.Should().Be(2);
    }

    private static SpeedReadingInstitutionMembersController CreateController(
        Guid institutionId,
        StubMemberships memberships,
        StubUserDirectory userDirectory)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "InstitutionAdmin")
        ], "Test"));
        return new SpeedReadingInstitutionMembersController(
            memberships,
            new StubStudentManagement(),
            new StubEligibility(),
            new StubAdministrationAuthorization(),
            new StubInstitutionDirectory(institutionId),
            userDirectory)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };
    }

    private static (SpeedReadingInstitutionMemberView[] Items, int TotalCount) ReadPage(IActionResult response)
    {
        var page = Assert.IsType<OkObjectResult>(response).Value!;
        var type = page.GetType();
        var items = (IEnumerable<SpeedReadingInstitutionMemberView>)type.GetProperty("items")!.GetValue(page)!;
        var totalCount = (int)type.GetProperty("totalCount")!.GetValue(page)!;
        return (items.ToArray(), totalCount);
    }

    private static SpeedReadingInstitutionMembershipRecord Record(
        Guid institutionId,
        Guid userId,
        bool profileActive,
        DateTime createdAt) => new(
            institutionId,
            userId,
            SpeedReadingInstitutionMemberRole.Teacher,
            true,
            createdAt,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            profileActive,
            null,
            0);

    private static SpeedReadingUserDirectoryItem User(Guid id, bool isActive) =>
        new(id, "Test", id.ToString("N"), isActive) { Email = $"{id:N}@example.test" };

    private sealed class StubMemberships(params SpeedReadingInstitutionMembershipRecord[] records)
        : ISpeedReadingInstitutionMemberships
    {
        public bool? LastRequestedActiveFilter { get; private set; }

        public Task<SpeedReadingInstitutionMembershipPage> GetMembersAsync(
            Guid institutionId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default,
            SpeedReadingInstitutionMemberRole? role = null,
            bool? isActive = null,
            int? gradeLevel = null,
            Guid? teacherUserId = null,
            Guid? memberUserId = null)
        {
            LastRequestedActiveFilter = isActive;
            var query = records.Where(item => item.InstitutionId == institutionId);
            if (role.HasValue) query = query.Where(item => item.Role == role.Value);
            if (isActive.HasValue) query = query.Where(item => item.IsActive == isActive.Value);
            if (memberUserId.HasValue) query = query.Where(item => item.UserId == memberUserId.Value);
            var matching = query.ToArray();
            return Task.FromResult(new SpeedReadingInstitutionMembershipPage(
                matching.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(),
                matching.Length,
                pageNumber,
                pageSize));
        }

        public Task<bool> SetMembershipAsync(Guid institutionId, Guid userId, SpeedReadingInstitutionMemberRole role, bool isActive, Guid actorId, DateTime at, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class StubUserDirectory(params SpeedReadingUserDirectoryItem[] users) : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse(users.Where(user => userIds.Contains(user.UserId)).ToArray()));

        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(string? role, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class StubStudentManagement : ISpeedReadingInstitutionStudentManagement
    {
        public Task<SpeedReadingInstitutionStudentUpdateResult> UpdateAsync(Guid institutionId, Guid studentUserId, int? gradeLevel, Guid? teacherUserId, Guid actorId, DateTime at, CancellationToken cancellationToken = default) =>
            Task.FromResult(SpeedReadingInstitutionStudentUpdateResult.Updated);
    }

    private sealed class StubEligibility : ISpeedReadingInstitutionMemberEligibility
    {
        public Task<bool> IsEligibleAsync(Guid userId, SpeedReadingInstitutionMemberRole role, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class StubAdministrationAuthorization : ISpeedReadingInstitutionAdministrationAuthorization
    {
        public Task<bool> CanManageAsync(Guid userId, Guid institutionId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class StubInstitutionDirectory(Guid institutionId) : ISpeedReadingInstitutionDirectory
    {
        public Task<SpeedReadingInstitutionScopeResponse> GetInstitutionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingInstitutionScopeResponse([
                new SpeedReadingInstitutionScopeItem(institutionId, "Test institution", true)
            ]));
    }
}
