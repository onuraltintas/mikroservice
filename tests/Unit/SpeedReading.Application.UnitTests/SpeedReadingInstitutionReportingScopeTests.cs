using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;
using EduPlatform.Shared.Contracts.Reporting;
using System.Reflection;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingInstitutionReportingScopeTests
{
    [Fact]
    public async Task Institution_analytics_counts_speed_reading_memberships_not_profile_institution_ids()
    {
        var institutionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var legacyProfileOnlyUserId = Guid.NewGuid();
        await using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        context.UserProfiles.Add(SpeedReadingUserProfile.Import(
            Guid.NewGuid(),
            legacyProfileOnlyUserId,
            currentLevel: 1,
            targetWpm: 150,
            targetComprehension: 70,
            dailyGoalMinutes: 20,
            ageGroupConfigurationId: null,
            institutionId,
            isActive: true,
            createdAt: DateTime.UtcNow,
            createdBy: null,
            updatedAt: null,
            updatedBy: null));
        context.InstitutionMemberships.AddRange(
            SpeedReadingInstitutionMembership.Create(
                institutionId,
                studentId,
                SpeedReadingInstitutionMemberRole.Student,
                Guid.NewGuid(),
                DateTime.UtcNow),
            SpeedReadingInstitutionMembership.Create(
                institutionId,
                teacherId,
                SpeedReadingInstitutionMemberRole.Teacher,
                Guid.NewGuid(),
                DateTime.UtcNow));
        await context.SaveChangesAsync();

        var analyticsType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAdminAnalytics",
            throwOnError: true)!;
        var analytics = (ILegacySpeedReadingAdminAnalytics)Activator.CreateInstance(
            analyticsType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                context,
                new TestInstitutionDirectory(new SpeedReadingInstitutionScopeResponse(
                [new SpeedReadingInstitutionScopeItem(institutionId, "Local School", true)])),
                new EmptyUserDirectory()
            ],
            culture: null)!;

        var result = await analytics.GetInstitutionAnalyticsAsync(null, null);

        result.TotalUsers.Should().Be(2);
        result.TotalStudents.Should().Be(1);
        result.TotalTeachers.Should().Be(1);
        result.InstitutionComparison.Should().ContainSingle(item =>
            item.InstitutionId == institutionId
            && item.TotalUsers == 2
            && item.TotalStudents == 1
            && item.TotalTeachers == 1);
    }

    private sealed class TestInstitutionDirectory(SpeedReadingInstitutionScopeResponse response)
        : ISpeedReadingInstitutionDirectory
    {
        public Task<SpeedReadingInstitutionScopeResponse> GetInstitutionsAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(response);
    }

    private sealed class EmptyUserDirectory : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse([]));

        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(
            string? role,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
