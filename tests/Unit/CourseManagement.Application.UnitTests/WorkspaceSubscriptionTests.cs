using CourseManagement.Domain;

namespace CourseManagement.Application.UnitTests;

public sealed class WorkspaceSubscriptionTests
{
    private static readonly DateTimeOffset StartsAt = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EndsAt = new(2027, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreationKeepsPlanAndAccessMetadataLocalToWorkspace()
    {
        var workspaceId = Guid.NewGuid();

        var subscription = WorkspaceSubscription.Create(workspaceId, "  Kurum Plus  ", 80,
            StartsAt, EndsAt, WorkspaceAccessStatus.Active);

        Assert.Equal(workspaceId, subscription.WorkspaceId);
        Assert.Equal("Kurum Plus", subscription.PlanName);
        Assert.Equal(80, subscription.ActiveStudentLimit);
        Assert.Equal(StartsAt, subscription.StartsAt);
        Assert.Equal(EndsAt, subscription.EndsAt);
        Assert.Equal(WorkspaceAccessStatus.Active, subscription.AccessStatus);
    }

    [Fact]
    public void PlanChangeUpdatesOnlySubscriptionFields()
    {
        var subscription = WorkspaceSubscription.Create(Guid.NewGuid(), "Başlangıç", 20,
            StartsAt, EndsAt, WorkspaceAccessStatus.Active);
        var nextEnd = EndsAt.AddYears(1);

        subscription.Update("  Kurum Plus ", 80, StartsAt.AddMonths(1), nextEnd, WorkspaceAccessStatus.Suspended);

        Assert.Equal("Kurum Plus", subscription.PlanName);
        Assert.Equal(80, subscription.ActiveStudentLimit);
        Assert.Equal(StartsAt.AddMonths(1), subscription.StartsAt);
        Assert.Equal(nextEnd, subscription.EndsAt);
        Assert.Equal(WorkspaceAccessStatus.Suspended, subscription.AccessStatus);
    }

    [Fact]
    public void InvalidSubscriptionValuesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => WorkspaceSubscription.Create(Guid.Empty, "Plan", 10,
            StartsAt, EndsAt, WorkspaceAccessStatus.Active));
        Assert.Throws<ArgumentException>(() => WorkspaceSubscription.Create(Guid.NewGuid(), " ", 10,
            StartsAt, EndsAt, WorkspaceAccessStatus.Active));
        Assert.Throws<ArgumentException>(() => WorkspaceSubscription.Create(Guid.NewGuid(), new string('x', 81), 10,
            StartsAt, EndsAt, WorkspaceAccessStatus.Active));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkspaceSubscription.Create(Guid.NewGuid(), "Plan", 0,
            StartsAt, EndsAt, WorkspaceAccessStatus.Active));
        Assert.Throws<ArgumentException>(() => WorkspaceSubscription.Create(Guid.NewGuid(), "Plan", 10,
            EndsAt, StartsAt, WorkspaceAccessStatus.Active));
        Assert.Throws<ArgumentException>(() => WorkspaceSubscription.Create(Guid.NewGuid(), "Plan", 10,
            StartsAt, EndsAt, (WorkspaceAccessStatus)99));
    }
}
