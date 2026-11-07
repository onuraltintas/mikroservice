using System.Reflection;
using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Commands.AcceptInvitation;
using Identity.Application.Commands.RejectInvitation;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetMyInvitations;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Paket F L5 (E5 leftover): after a confirmed e-mail change the token's <c>email</c> claim keeps the old address until
/// the session is refreshed. Invitations are matched against the account's current address, read by the token's
/// subject, so the new address sees and answers its invitations and the old one no longer does.
/// </summary>
public sealed class InvitationCurrentEmailTests
{
    private const string OldEmail = "eski@example.test";
    private const string NewEmail = "yeni@example.test";

    [Fact]
    public async Task MyInvitationsAreListedForTheCurrentAddressNotTheStaleClaim()
    {
        var f = new Fixture();
        var asked = new List<string>();
        var invitations = Stub<IInvitationRepository>((m, args) => m.Name switch
        {
            nameof(IInvitationRepository.GetPendingWithInviterEmailAsync) => Record(asked, (string)args![0]!),
            _ => throw new NotSupportedException(m.Name)
        });

        var result = await new GetMyInvitationsQueryHandler(invitations, f.Current, f.Users)
            .Handle(new GetMyInvitationsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        asked.Should().Equal(NewEmail);
    }

    [Fact]
    public async Task MyInvitationsNeedAnExistingAccount()
    {
        var f = new Fixture(userExists: false);
        var invitations = Stub<IInvitationRepository>((m, _) => throw new NotSupportedException(m.Name));

        var result = await new GetMyInvitationsQueryHandler(invitations, f.Current, f.Users)
            .Handle(new GetMyInvitationsQuery(), CancellationToken.None);

        result.Error.Code.Should().Be("Auth.Unauthorized");
    }

    // Review L3: an inactive (or erased) account has no current address; the read loads no roles or permissions.
    [Fact]
    public async Task TheCurrentAddressIsReadOnlyForAnActiveAccount()
    {
        await using var context = new Identity.Infrastructure.Persistence.IdentityDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Identity.Infrastructure.Persistence.IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var active = User.Create(Guid.NewGuid(), NewEmail, "Ada", "Kaya");
        var inactive = User.Create(Guid.NewGuid(), OldEmail, "Eski", "Hesap");
        inactive.Deactivate();
        context.Users.AddRange(active, inactive);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var users = new Identity.Infrastructure.Repositories.UserRepository(context);

        (await users.GetActiveEmailAsync(active.Id, CancellationToken.None)).Should().Be(NewEmail);
        (await users.GetActiveEmailAsync(inactive.Id, CancellationToken.None)).Should().BeNull();
        (await users.GetActiveEmailAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
        context.ChangeTracker.Entries().Should().BeEmpty("the read does not track the account");
    }

    [Theory]
    [InlineData(NewEmail, true)]
    [InlineData(OldEmail, false)]
    public async Task RejectingFollowsTheCurrentAddress(string inviteeEmail, bool allowed)
    {
        var f = new Fixture();
        var invitation = Invitation.Create(Guid.NewGuid(), inviteeEmail, InvitationType.StudentToTeacher, teacherId: Guid.NewGuid());

        var result = await new RejectInvitationCommandHandler(f.Invitations(invitation), f.UnitOfWork, f.Current, f.Users)
            .Handle(new RejectInvitationCommand(invitation.Id), CancellationToken.None);

        if (allowed)
        {
            result.IsSuccess.Should().BeTrue();
            invitation.Status.Should().Be(InvitationStatus.Rejected);
        }
        else
        {
            result.Error.Code.Should().Be("Invitation.Forbidden");
            invitation.Status.Should().Be(InvitationStatus.Pending);
        }
    }

    [Theory]
    [InlineData(NewEmail, true)]
    [InlineData(OldEmail, false)]
    public async Task AcceptingFollowsTheCurrentAddress(string inviteeEmail, bool allowed)
    {
        var f = new Fixture();
        var institutionId = Guid.NewGuid();
        var invitation = Invitation.Create(Guid.NewGuid(), inviteeEmail, InvitationType.TeacherToInstitution, institutionId: institutionId);
        var teacher = TeacherProfile.Create(f.User.Id, "Ada", "Kaya");
        var teachers = Stub<ITeacherRepository>((m, args) => m.Name switch
        {
            nameof(ITeacherRepository.GetByUserIdAsync) when args!.Length == 2 => Task.FromResult<TeacherProfile?>(teacher),
            _ => throw new NotSupportedException(m.Name)
        });
        var students = Stub<IStudentRepository>((m, _) => throw new NotSupportedException(m.Name));

        var result = await new AcceptInvitationCommandHandler(
                f.Invitations(invitation), f.Users, teachers, students, f.UnitOfWork, f.Current,
                NullLogger<AcceptInvitationCommandHandler>.Instance)
            .Handle(new AcceptInvitationCommand(invitation.Id), CancellationToken.None);

        if (allowed)
        {
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : "");
            invitation.Status.Should().Be(InvitationStatus.Accepted);
            teacher.InstitutionId.Should().Be(institutionId);
        }
        else
        {
            result.Error.Code.Should().Be("Invitation.Forbidden");
            invitation.Status.Should().Be(InvitationStatus.Pending);
        }
    }

    private static Task<List<PendingInvitationReadModel>> Record(List<string> asked, string email)
    {
        asked.Add(email);
        return Task.FromResult(new List<PendingInvitationReadModel>());
    }

    private sealed class Fixture
    {
        public readonly User User = User.Create(Guid.NewGuid(), NewEmail, "Ada", "Kaya");
        public readonly IUserRepository Users;
        public readonly IUnitOfWork UnitOfWork = Stub<IUnitOfWork>((m, _) => m.Name switch
        {
            nameof(IUnitOfWork.SaveChangesAsync) => Task.FromResult(1),
            _ => null
        });

        /// <summary>The session still carries the address from before the change.</summary>
        public ICurrentUserService Current => new StaleClaim(User.Id);

        public Fixture(bool userExists = true)
        {
            Users = Stub<IUserRepository>((m, args) => m.Name switch
            {
                nameof(IUserRepository.GetByIdAsync) => Task.FromResult<User?>(userExists && (Guid)args![0]! == User.Id ? User : null),
                nameof(IUserRepository.GetActiveEmailAsync) => Task.FromResult<string?>(userExists && (Guid)args![0]! == User.Id ? User.Email : null),
                nameof(IUserRepository.TrackProductAccessIfNew) => null,
                _ => throw new NotSupportedException(m.Name)
            });
        }

        public IInvitationRepository Invitations(Invitation invitation) => Stub<IInvitationRepository>((m, args) => m.Name switch
        {
            nameof(IInvitationRepository.GetByIdAsync) => Task.FromResult<Invitation?>((Guid)args![0]! == invitation.Id ? invitation : null),
            _ => throw new NotSupportedException(m.Name)
        });
    }

    private sealed class StaleClaim(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => OldEmail;
        public string? FullName => "Ada Kaya";
        public IEnumerable<string> Roles => ["Teacher"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }

    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> call) where T : class
    {
        var proxy = DispatchProxy.Create<T, StubProxy>();
        ((StubProxy)(object)proxy).Call = call;
        return proxy;
    }

    public class StubProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Call = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Call(targetMethod!, args);
    }
}
