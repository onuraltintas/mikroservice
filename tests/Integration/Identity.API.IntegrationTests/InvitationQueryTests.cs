using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetMyInvitations;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class InvitationQueryTests
{
    [Fact]
    public async Task GetMyInvitations_ReturnsTheActualInviterEmail()
    {
        var repository = new StubInvitationRepository
        {
            PendingWithInviter =
            [
                new PendingInvitationReadModel(
                    Guid.NewGuid(),
                    "teacher@example.com",
                    InvitationType.StudentToTeacher,
                    InvitationStatus.Pending,
                    "Welcome",
                    DateTime.UtcNow.AddMinutes(-5),
                    DateTime.UtcNow.AddDays(2))
            ]
        };
        var handler = new GetMyInvitationsQueryHandler(
            repository,
            new StubCurrentUserService("student@example.com"));

        var result = await handler.Handle(new GetMyInvitationsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle()
            .Which.InviterEmail.Should().Be("teacher@example.com");
    }

    private sealed class StubInvitationRepository : IInvitationRepository
    {
        public List<PendingInvitationReadModel> PendingWithInviter { get; init; } = [];

        public Task AddAsync(Invitation invitation, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Invitation?>(null);
        public Task<List<Invitation>> GetPendingByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(new List<Invitation>());
        public Task<List<PendingInvitationReadModel>> GetPendingWithInviterEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(PendingWithInviter);
        public Task<List<Invitation>> GetByInviterIdAsync(Guid inviterId, CancellationToken cancellationToken) =>
            Task.FromResult(new List<Invitation>());
    }

    private sealed class StubCurrentUserService(string email) : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public string? Email => email;
        public string? FullName => "Student";
        public IEnumerable<string> Roles => ["Student"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
