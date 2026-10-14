using CourseManagement.Domain;

namespace CourseManagement.Application.UnitTests;

public sealed class MembershipAndInvitationTests
{
    [Fact]
    public void MembershipRevocationDoesNotChangeSharedIdentity()
    {
        var userId = Guid.NewGuid();
        var membership = WorkspaceMembership.Create(Guid.NewGuid(), userId, WorkspaceRole.Teacher);
        membership.Revoke();
        Assert.False(membership.IsActive);
        Assert.Equal(userId, membership.UserId);
        membership.Activate(WorkspaceRole.Student);
        Assert.True(membership.IsActive);
        Assert.Equal(WorkspaceRole.Student, membership.Role);
    }

    [Fact]
    public void OwnerMembershipCannotBeRevokedOrReassigned()
    {
        var membership = WorkspaceMembership.Create(Guid.NewGuid(), Guid.NewGuid(), WorkspaceRole.Owner);
        Assert.Throws<InvalidOperationException>(() => membership.Revoke());
        Assert.Throws<InvalidOperationException>(() => membership.Activate(WorkspaceRole.Teacher));
    }

    [Fact]
    public void InvitationOnlyAuthorizesItsTargetAndCannotGrantOwnership()
    {
        var target = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var invitation = WorkspaceInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), target, WorkspaceRole.Teacher, now);
        Assert.False(invitation.CanAccept(Guid.NewGuid(), now));
        Assert.True(invitation.CanAccept(target, now));
        invitation.Accept(target, now);
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Throws<ArgumentException>(() => WorkspaceInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), target, WorkspaceRole.Owner, now));
    }

    [Fact]
    public void ExistingStudentRecordCanOnlyBeAttachedToStudentInvitation()
    {
        var studentRecordId = Guid.NewGuid();
        var invitation = WorkspaceInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), WorkspaceRole.Student,
            DateTimeOffset.UtcNow, studentRecordId);

        Assert.Equal(studentRecordId, invitation.StudentRecordId);
        Assert.Throws<ArgumentException>(() => WorkspaceInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            WorkspaceRole.Teacher, DateTimeOffset.UtcNow, studentRecordId));
    }

    [Fact]
    public void ExpiredAndCancelledInvitationsCannotBeAccepted()
    {
        var target = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expired = WorkspaceInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), target, WorkspaceRole.Student, now);
        Assert.False(expired.CanAccept(target, now.AddDays(7)));
        Assert.Throws<InvalidOperationException>(() => expired.Accept(target, now.AddDays(7)));
        var cancelled = WorkspaceInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), target, WorkspaceRole.Student, now);
        cancelled.Cancel();
        Assert.False(cancelled.CanAccept(target, now));
    }
}
