using EduPlatform.Shared.Contracts.Reporting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Gamification;
using SpeedReading.Application.Notifications;
using SpeedReading.Domain.Gamification;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Review H4 (finding 25): the institution leaderboard and institution announcements follow today's memberships, not
/// the legacy profile field (filled only by the old import) or a token claim that is never issued.
/// </summary>
public sealed class SpeedReadingInstitutionAudienceTests
{
    [Fact]
    public async Task The_institution_leaderboard_lists_the_members_of_the_students_institution()
    {
        await using var db = CreateDb();
        var institution = Guid.NewGuid();
        var me = Guid.NewGuid();
        var classmate = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        foreach (var user in new[] { me, classmate, stranger })
        {
            db.UserProfiles.Add(SpeedReadingUserProfile.CreateDefault(Guid.NewGuid(), user, DateTime.UtcNow));
            db.UserGamifications.Add(UserGamification.CreateDefault(Guid.NewGuid(), user, DateTime.UtcNow, null));
        }
        db.InstitutionMemberships.AddRange(
            Member(institution, me), Member(institution, classmate), Member(Guid.NewGuid(), stranger));
        await db.SaveChangesAsync();
        var service = (ILegacySpeedReadingGamification)Activator.CreateInstance(
            typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingGamification")!,
            db, new Directory())!;

        var board = await service.GetLeaderboardAsync("TotalXP", 0, 10, me, isGlobalAdministrator: false);

        board.Select(entry => entry.UserId).Should().BeEquivalentTo([me, classmate]);
    }

    [Fact]
    public async Task An_institution_announcement_reaches_its_members()
    {
        await using var db = CreateDb();
        var institution = Guid.NewGuid();
        var member = Guid.NewGuid();
        db.InstitutionMemberships.Add(Member(institution, member));
        var announcementType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Legacy.LegacyAnnouncement")!;
        var announcement = Activator.CreateInstance(announcementType)!;
        foreach (var (name, value) in new (string, object?)[]
                 {
                     ("Id", Guid.NewGuid()), ("Title", "Kurum duyurusu"), ("Content", "Yarın deneme var."), ("TargetAudience", "5"),
                     ("TargetInstitutionId", (Guid?)institution), ("IsActive", true), ("CreatedByUserId", Guid.NewGuid()), ("CreatedAt", DateTime.UtcNow)
                 })
            announcementType.GetProperty(name)!.SetValue(announcement, value);
        db.Add(announcement);
        await db.SaveChangesAsync();
        var service = (ISpeedReadingAnnouncements)Activator.CreateInstance(
            typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Legacy.LegacySpeedReadingAnnouncements")!, db)!;

        var mine = await service.GetMyAsync(member, ["Student"], null, false, false, CancellationToken.None);
        var others = await service.GetMyAsync(Guid.NewGuid(), ["Student"], null, false, false, CancellationToken.None);

        mine.Should().ContainSingle();
        others.Should().BeEmpty();
    }

    private static OwnedSpeedReadingDbContext CreateDb() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static SpeedReadingInstitutionMembership Member(Guid institution, Guid user) =>
        SpeedReadingInstitutionMembership.Create(institution, user, SpeedReadingInstitutionMemberRole.Student, Guid.NewGuid(), DateTime.UtcNow);

    private sealed class Directory : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse(
                userIds.Select(id => new SpeedReadingUserDirectoryItem(id, "Öğrenci", "Bir", true)).ToArray()));
        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(string? role, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
