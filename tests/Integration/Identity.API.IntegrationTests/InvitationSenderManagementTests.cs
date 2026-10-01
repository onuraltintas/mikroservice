using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Commands.CancelSentInvitation;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetMySentInvitations;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class InvitationSenderManagementTests
{
    [Fact]
    public async Task Sent_invitation_list_is_limited_to_the_current_senders_pending_invitations()
    {
        var senderId = Guid.NewGuid();
        var otherSenderId = Guid.NewGuid();
        var pending = Invitation.Create(senderId, "pending@example.test", InvitationType.StudentToTeacher);
        var accepted = Invitation.Create(senderId, "accepted@example.test", InvitationType.StudentToTeacher);
        accepted.Accept(Guid.NewGuid());
        var otherPending = Invitation.Create(otherSenderId, "other@example.test", InvitationType.TeacherToInstitution);
        var repository = new StubInvitationRepository([pending, accepted, otherPending]);
        var handler = new GetMySentInvitationsQueryHandler(repository, new StubCurrentUserService(senderId));

        var result = await handler.Handle(new GetMySentInvitationsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle()
            .Which.InvitationId.Should().Be(pending.Id);
    }

    [Fact]
    public async Task Sender_can_cancel_own_pending_invitation_and_recipient_can_no_longer_accept_it()
    {
        var senderId = Guid.NewGuid();
        var invitation = Invitation.Create(senderId, "student@example.test", InvitationType.StudentToTeacher);
        var repository = new StubInvitationRepository([invitation]);
        var handler = new CancelSentInvitationCommandHandler(
            repository,
            new StubUnitOfWork(),
            new StubCurrentUserService(senderId));

        var result = await handler.Handle(new CancelSentInvitationCommand(invitation.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        invitation.Status.Should().Be(InvitationStatus.Cancelled);
        invitation.IsPending().Should().BeFalse();
    }

    [Fact]
    public async Task Sender_cannot_cancel_another_users_invitation()
    {
        var invitation = Invitation.Create(Guid.NewGuid(), "student@example.test", InvitationType.StudentToTeacher);
        var handler = new CancelSentInvitationCommandHandler(
            new StubInvitationRepository([invitation]),
            new StubUnitOfWork(),
            new StubCurrentUserService(Guid.NewGuid()));

        var result = await handler.Handle(new CancelSentInvitationCommand(invitation.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Invitation.NotFound");
        invitation.Status.Should().Be(InvitationStatus.Pending);
    }

    [Fact]
    public async Task Expired_sent_invitation_is_not_listed_or_cancelled_as_pending()
    {
        var senderId = Guid.NewGuid();
        var invitation = Invitation.Create(
            senderId,
            "student@example.test",
            InvitationType.StudentToTeacher,
            expirationDays: 0);
        var repository = new StubInvitationRepository([invitation]);
        var handler = new CancelSentInvitationCommandHandler(
            repository,
            new StubUnitOfWork(),
            new StubCurrentUserService(senderId));

        var result = await handler.Handle(new CancelSentInvitationCommand(invitation.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Invitation.NotPending");
        invitation.Status.Should().Be(InvitationStatus.Expired);
    }

    private sealed class StubInvitationRepository(List<Invitation> invitations) : IInvitationRepository
    {
        public Task AddAsync(Invitation invitation, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(invitations.SingleOrDefault(item => item.Id == id));
        public Task<List<Invitation>> GetPendingByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(invitations.Where(item => item.InviteeEmail == email).ToList());
        public Task<List<PendingInvitationReadModel>> GetPendingWithInviterEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(new List<PendingInvitationReadModel>());
        public Task<List<Invitation>> GetByInviterIdAsync(Guid inviterId, CancellationToken cancellationToken) =>
            Task.FromResult(invitations.Where(item => item.InviterId == inviterId).ToList());
        public Task<List<Invitation>> GetPendingByInviterIdAsync(Guid inviterId, DateTime now, CancellationToken cancellationToken) =>
            Task.FromResult(invitations.Where(item => item.InviterId == inviterId
                && item.Status == InvitationStatus.Pending
                && item.ExpiresAt > now).ToList());
    }

    private sealed class StubCurrentUserService(Guid? userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => "sender@example.test";
        public string? FullName => "Sender";
        public IEnumerable<string> Roles => ["Teacher"];
        public bool IsAuthenticated => userId.HasValue;
        public ClaimsPrincipal? User => null;
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
        public void ClearTracking() { }
    }
}
