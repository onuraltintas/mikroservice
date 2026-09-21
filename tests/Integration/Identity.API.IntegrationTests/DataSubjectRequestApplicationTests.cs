using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.API.Controllers;
using Identity.Application.DataSubjectRequests;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Identity.API.IntegrationTests;

public sealed class DataSubjectRequestApplicationTests
{
    [Fact]
    public async Task Submit_ShouldUseAuthenticatedUserAndPersistRequest()
    {
        var userId = Guid.NewGuid();
        var repository = new StubRepository();
        var handler = new SubmitDataSubjectRequestCommandHandler(
            repository,
            new StubCurrentUser(userId),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero)));

        var result = await handler.Handle(
            new SubmitDataSubjectRequestCommand(
                DataSubjectRequestType.Erasure,
                "Koçluk verilerimi silmek istiyorum."),
            CancellationToken.None);

        result.RequesterUserId.Should().Be(userId);
        repository.Added.Should().BeSameAs(result);
    }

    [Fact]
    public async Task Submit_ShouldRejectDuplicateActiveRequestOfSameType()
    {
        var repository = new StubRepository { HasActive = true };
        var handler = new SubmitDataSubjectRequestCommandHandler(
            repository,
            new StubCurrentUser(Guid.NewGuid()),
            TimeProvider.System);

        var action = () => handler.Handle(
            new SubmitDataSubjectRequestCommand(DataSubjectRequestType.Erasure, "Tekrar talep."),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void ErasureEndpoint_ShouldRequireMfa()
    {
        var method = typeof(DataSubjectRequestsController)
            .GetMethod(nameof(DataSubjectRequestsController.SubmitErasure));

        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == "MfaRequired");
    }

    private sealed class StubRepository : IDataSubjectRequestRepository
    {
        public bool HasActive { get; init; }
        public DataSubjectRequest? Added { get; private set; }

        public Task<bool> HasActiveAsync(Guid userId, DataSubjectRequestType requestType, CancellationToken cancellationToken) =>
            Task.FromResult(HasActive);

        public Task AddAsync(DataSubjectRequest request, CancellationToken cancellationToken)
        {
            Added = request;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DataSubjectRequest>> GetByRequesterAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DataSubjectRequest>>([]);
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => ["Student"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
