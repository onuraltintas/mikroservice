using System.Security.Claims;
using System.Net;
using System.Net.Http.Json;
using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Infrastructure.ExternalServices;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingProgressAccessTests
{
    [Fact]
    public async Task Scope_and_search_use_only_the_authorized_speed_reading_institution()
    {
        await using var db = CreateDb();
        var coachingInstitution = Guid.NewGuid();
        var speedInstitution = Guid.NewGuid();
        var coachingStudent = Guid.NewGuid();
        var speedStudent = Guid.NewGuid();
        Add(db, coachingInstitution, coachingStudent);
        Add(db, speedInstitution, speedStudent);
        await db.SaveChangesAsync();
        var viewer = new Viewer("speed-reading", "InstitutionAdmin");
        var access = new OwnedSpeedReadingProgressAccess(db, new Institutions(coachingInstitution, speedInstitution),
            new Authorization(speedInstitution), new Users(coachingStudent, speedStudent), viewer, GlobalAccess());

        (await access.GetScopeAsync(viewer.UserId!.Value))!.StudentUserIds.Should().Equal(speedStudent);
        (await access.SearchStudentUserIdsAsync(viewer.UserId.Value, "Student")).Should().Equal(speedStudent);
    }

    [Theory]
    [InlineData("coaching", "InstitutionAdmin")]
    [InlineData("speed-reading", "Student")]
    public async Task Wrong_product_or_role_cannot_read_progress(string product, string role)
    {
        await using var db = CreateDb();
        var viewer = new Viewer(product, role);
        var access = new OwnedSpeedReadingProgressAccess(db, new Institutions(), new Authorization(), new Users(), viewer, GlobalAccess());
        (await access.GetScopeAsync(viewer.UserId!.Value)).Should().BeNull();
        (await access.SearchStudentUserIdsAsync(viewer.UserId.Value, "Student")).Should().BeEmpty();
    }

    [Fact]
    public async Task Revoked_authorization_inactive_membership_and_foreign_viewer_are_excluded()
    {
        await using var db = CreateDb();
        var institution = Guid.NewGuid();
        var student = Guid.NewGuid();
        Add(db, institution, student);
        var inactive = SpeedReadingInstitutionMembership.Create(institution, Guid.NewGuid(), SpeedReadingInstitutionMemberRole.Student, Guid.NewGuid(), DateTime.UtcNow);
        inactive.SetActive(false, Guid.NewGuid(), DateTime.UtcNow);
        db.InstitutionMemberships.Add(inactive);
        await db.SaveChangesAsync();
        var viewer = new Viewer("speed-reading", "InstitutionOwner");
        var access = new OwnedSpeedReadingProgressAccess(db, new Institutions(institution), new Authorization(), new Users(student), viewer, GlobalAccess());
        (await access.GetScopeAsync(viewer.UserId!.Value))!.StudentUserIds.Should().BeEmpty();
        (await access.GetScopeAsync(Guid.NewGuid())).Should().BeNull();
        var allowed = new OwnedSpeedReadingProgressAccess(db, new Institutions(institution), new Authorization(institution), new Users(student), viewer, GlobalAccess());
        (await allowed.GetScopeAsync(viewer.UserId.Value))!.StudentUserIds.Should().Equal(student);
    }

    [Fact]
    public async Task Global_system_admin_retains_global_history_access()
    {
        await using var db = CreateDb();
        var viewer = new Viewer("coaching", "SystemAdmin");
        var access = new OwnedSpeedReadingProgressAccess(db, new Institutions(), new Authorization(), new Users(), viewer, GlobalAccess());
        (await access.GetScopeAsync(viewer.UserId!.Value))!.IsGlobal.Should().BeTrue();
    }

    [Fact]
    public async Task Stale_system_admin_token_cannot_bypass_current_identity_authorization()
    {
        await using var db = CreateDb();
        var viewer = new Viewer("speed-reading", "SystemAdmin");
        var access = new OwnedSpeedReadingProgressAccess(db, new Institutions(), new Authorization(), new Users(), viewer, GlobalAccess(false));
        (await access.GetScopeAsync(viewer.UserId!.Value)).Should().BeNull();
    }

    private static IdentityProgressAccessClient GlobalAccess(bool allowed = true) => new(
        new HttpClient(new GlobalHandler(allowed)), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["INTERNAL_SERVICE_API_KEY"] = "test-only-key", ["Services:IdentityService"] = "http://identity.test" }).Build(),
        NullLogger<IdentityProgressAccessClient>.Instance);
    private sealed class GlobalHandler(bool allowed) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(
            new HttpResponseMessage(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden)
            { Content = JsonContent.Create(new { IsGlobal = true, InstitutionId = (Guid?)null }) });
    }

    private static OwnedSpeedReadingDbContext CreateDb() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static void Add(OwnedSpeedReadingDbContext db, Guid institution, Guid user) => db.InstitutionMemberships.Add(
        SpeedReadingInstitutionMembership.Create(institution, user, SpeedReadingInstitutionMemberRole.Student, Guid.NewGuid(), DateTime.UtcNow));
    private sealed class Viewer(string product, string role) : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => [role];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User { get; } = new(new ClaimsIdentity([new Claim("platform_product", product), new Claim(ClaimTypes.Role, role)], "test"));
    }
    private sealed class Institutions(params Guid[] ids) : ISpeedReadingInstitutionDirectory
    {
        public Task<SpeedReadingInstitutionScopeResponse> GetInstitutionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SpeedReadingInstitutionScopeResponse(ids.Select(id => new SpeedReadingInstitutionScopeItem(id, "Institution", true)).ToArray()));
    }
    private sealed class Authorization(params Guid[] ids) : ISpeedReadingInstitutionAdministrationAuthorization
    {
        public Task<bool> CanManageAsync(Guid userId, Guid institutionId, CancellationToken cancellationToken = default) => Task.FromResult(ids.Contains(institutionId));
    }
    private sealed class Users(params Guid[] ids) : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) => Task.FromResult(new SpeedReadingUserDirectoryResponse(ids.Where(userIds.Contains).Select(id => new SpeedReadingUserDirectoryItem(id, "Student", "Test", true)).ToArray()));
        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(string? role, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Guid>>(ids);
    }
}
