using CourseManagement.Domain;

namespace CourseManagement.Application.UnitTests;

public sealed class WorkspaceSubscriptionAccessPolicyTests
{
    private static readonly DateTimeOffset StartsAt = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EndsAt = new(2027, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Workspace_writes_require_active_status_and_an_open_subscription_period()
    {
        var subscription = WorkspaceSubscription.Create(Guid.NewGuid(), "Kurum Plus", 40,
            StartsAt, EndsAt, WorkspaceAccessStatus.Active);

        Assert.False(subscription.CanWriteAt(StartsAt.AddTicks(-1)));
        Assert.True(subscription.CanWriteAt(StartsAt));
        Assert.True(subscription.CanWriteAt(EndsAt.AddTicks(-1)));
        Assert.False(subscription.CanWriteAt(EndsAt));

        subscription.Update("Kurum Plus", 40, StartsAt, EndsAt, WorkspaceAccessStatus.Suspended);
        Assert.False(subscription.CanWriteAt(StartsAt.AddDays(1)));
    }
}
