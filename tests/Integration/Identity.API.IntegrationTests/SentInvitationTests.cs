using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Queries.GetSentInvitations;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class SentInvitationTests
{
    [Fact]
    public async Task Sender_only_sees_own_invitation_status_and_recipient()
    {
        var senderId = Guid.NewGuid();
        var otherSenderId = Guid.NewGuid();
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.Invitations.AddRange(
            Invitation.Create(senderId, "teacher@example.com", InvitationType.TeacherToInstitution, institutionId: Guid.NewGuid()),
            Invitation.Create(otherSenderId, "private@example.com", InvitationType.StudentToInstitution, institutionId: Guid.NewGuid()));
        await context.SaveChangesAsync();

        var handler = new GetSentInvitationsQueryHandler(new InvitationRepository(context), new CurrentUser(senderId));
        var result = await handler.Handle(new GetSentInvitationsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(item => item.InviteeEmail == "teacher@example.com" && item.Status == "Pending");
    }

    private sealed class CurrentUser(Guid id) : ICurrentUserService
    {
        public Guid? UserId => id;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => ["InstitutionAdmin"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
