using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Commands.AcceptInvitation;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class InvitationInstitutionScopeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepting_another_institutions_invitation_does_not_silently_transfer_member(bool teacher)
    {
        var oldInstitution = Institution.Create("Current", InstitutionType.School);
        var newInstitution = Institution.Create("New", InstitutionType.School);
        var user = User.Create(Guid.NewGuid(), "member@example.com", "Member", "One");
        var invitation = Invitation.Create(Guid.NewGuid(), user.Email,
            teacher ? InvitationType.TeacherToInstitution : InvitationType.StudentToInstitution,
            newInstitution.Id);
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.AddRange(oldInstitution, newInstitution, user, invitation);
        if (teacher) context.TeacherProfiles.Add(TeacherProfile.Create(user.Id, "Member", "One", oldInstitution.Id));
        else context.StudentProfiles.Add(StudentProfile.Create(user.Id, "Member", "One", oldInstitution.Id));
        await context.SaveChangesAsync();

        var handler = new AcceptInvitationCommandHandler(
            new InvitationRepository(context), new UserRepository(context),
            new TeacherRepository(context), new StudentRepository(context),
            new UnitOfWork(context), new CurrentUser(user.Id, user.Email),
            NullLogger<AcceptInvitationCommandHandler>.Instance);
        var result = await handler.Handle(new AcceptInvitationCommand(invitation.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        (teacher ? context.TeacherProfiles.Single().InstitutionId : context.StudentProfiles.Single().InstitutionId)
            .Should().Be(oldInstitution.Id);
        invitation.Status.Should().Be(InvitationStatus.Pending);
    }

    private sealed class CurrentUser(Guid id, string email) : ICurrentUserService
    {
        public Guid? UserId => id;
        public string? Email => email;
        public string? FullName => null;
        public IEnumerable<string> Roles => [];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
