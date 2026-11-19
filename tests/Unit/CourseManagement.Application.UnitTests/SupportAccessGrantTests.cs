using CourseManagement.Domain;

namespace CourseManagement.Application.UnitTests;

public sealed class SupportAccessGrantTests
{
    private static readonly Guid WorkspaceId = Guid.Parse("8ae5aaed-08f6-4d4a-a9a3-5cbeb6c9b125");
    private static readonly Guid AdminId = Guid.Parse("5b8d8ad6-2c1d-4a63-9d2c-6c0f9c3c1a11");
    private static readonly Guid OwnerId = Guid.Parse("f9dcfd6b-7932-47e8-9f51-7ca0738fa983");
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static SupportAccessGrant Requested() =>
        SupportAccessGrant.Request(WorkspaceId, AdminId, "Taksit listesi yanlış görünüyor, bakmamız gerekiyor.", Now);

    [Fact]
    public void A_request_opens_nothing_until_the_owner_approves()
    {
        var grant = Requested();

        Assert.Equal(SupportAccessStatus.Requested, grant.Status);
        Assert.False(grant.Opens(SupportAccessSections.Students, AdminId, Now));
    }

    [Fact]
    public void An_approved_grant_opens_only_its_sections_for_its_administrator_until_it_expires()
    {
        var grant = Requested();
        grant.Approve(OwnerId, 24, SupportAccessSections.Students | SupportAccessSections.Finance, Now);

        Assert.True(grant.Opens(SupportAccessSections.Finance, AdminId, Now.AddHours(1)));
        Assert.False(grant.Opens(SupportAccessSections.Schedule, AdminId, Now.AddHours(1)));
        Assert.False(grant.Opens(SupportAccessSections.Students, Guid.NewGuid(), Now.AddHours(1)));
        Assert.False(grant.Opens(SupportAccessSections.Students, AdminId, Now.AddHours(24)));
        Assert.Equal(Now.AddHours(24), grant.ExpiresAt);
    }

    [Fact]
    public void The_owner_can_end_it_at_any_moment()
    {
        var grant = Requested();
        grant.Approve(OwnerId, 168, SupportAccessSections.Settings, Now);

        grant.End("EndedByOwner", Now.AddMinutes(5));

        Assert.False(grant.Opens(SupportAccessSections.Settings, AdminId, Now.AddMinutes(6)));
        Assert.Equal(Now.AddMinutes(5), grant.EndedAt);
        Assert.Equal("EndedByOwner", grant.EndReason);
    }

    [Fact]
    public void Ending_an_expired_grant_records_when_it_expired()
    {
        var grant = Requested();
        grant.Approve(OwnerId, 1, SupportAccessSections.Students, Now);

        grant.End("EndedByOwner", Now.AddHours(5));

        Assert.Equal(Now.AddHours(1), grant.EndedAt);
        Assert.Equal("Expired", grant.EndReason);
    }

    [Fact]
    public void Invalid_requests_and_choices_are_refused()
    {
        Assert.Throws<ArgumentException>(() => SupportAccessGrant.Request(WorkspaceId, AdminId, "kısa", Now));
        var grant = Requested();
        Assert.Throws<ArgumentOutOfRangeException>(() => grant.Approve(OwnerId, 12, SupportAccessSections.Students, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => grant.Approve(OwnerId, 24, SupportAccessSections.None, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => grant.Approve(OwnerId, 24, (SupportAccessSections)64, Now));
        grant.Decline(OwnerId, Now);
        Assert.Throws<InvalidOperationException>(() => grant.Approve(OwnerId, 24, SupportAccessSections.Students, Now));
    }
}
