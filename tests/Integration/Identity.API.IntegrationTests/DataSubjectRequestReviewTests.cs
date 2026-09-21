using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.API.Controllers;
using Identity.Application.DataSubjectRequests;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Identity.API.IntegrationTests;

public sealed class DataSubjectRequestReviewTests
{
    [Fact]
    public async Task Review_ShouldRecordIdentityVerificationAndReviewerDecision()
    {
        var reviewerId = Guid.NewGuid();
        var request = DataSubjectRequest.Create(
            Guid.NewGuid(), DataSubjectRequestType.Erasure, "Silme talebi", Utc(12));
        var repository = new StubRepository(request);
        var currentUser = new StubCurrentUser(reviewerId, ["SystemAdmin"]);
        var clock = new FixedTimeProvider(new DateTimeOffset(Utc(13)));

        await new VerifyDataSubjectRequestIdentityCommandHandler(repository, currentUser, clock)
            .Handle(new VerifyDataSubjectRequestIdentityCommand(request.Id), CancellationToken.None);
        clock.UtcNow = new DateTimeOffset(Utc(14));
        var result = await new DecideDataSubjectRequestCommandHandler(repository, currentUser, clock)
            .Handle(new DecideDataSubjectRequestCommand(request.Id, true, "Saklama yükümlülüğü yok."), CancellationToken.None);

        result.Status.Should().Be(DataSubjectRequestStatus.Approved);
        request.DecidedByUserId.Should().Be(reviewerId);
        repository.SaveCount.Should().Be(2);
    }

    [Fact]
    public async Task Review_ShouldRejectNonSystemAdministrator()
    {
        var request = DataSubjectRequest.Create(
            Guid.NewGuid(), DataSubjectRequestType.Erasure, "Silme talebi", Utc(12));
        var handler = new VerifyDataSubjectRequestIdentityCommandHandler(
            new StubRepository(request),
            new StubCurrentUser(Guid.NewGuid(), ["InstitutionAdmin"]),
            TimeProvider.System);

        var action = () => handler.Handle(
            new VerifyDataSubjectRequestIdentityCommand(request.Id), CancellationToken.None);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData(nameof(DataSubjectRequestsController.AdminList), PlatformPermissions.Privacy.View)]
    [InlineData(nameof(DataSubjectRequestsController.VerifyIdentity), PlatformPermissions.Privacy.Manage)]
    [InlineData(nameof(DataSubjectRequestsController.Decide), PlatformPermissions.Privacy.Manage)]
    public void ReviewEndpoints_ShouldRequireSystemAdminMfaAndPrivacyPermission(
        string actionName,
        string expectedPermission)
    {
        var method = typeof(DataSubjectRequestsController).GetMethod(actionName);

        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .Contain(attribute => attribute.Roles == "SystemAdmin")
            .And.Contain(attribute => attribute.Policy == "MfaRequired");
        method.GetCustomAttributes(typeof(HasPermissionAttribute), true)
            .Cast<HasPermissionAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == expectedPermission);
    }

    private static DateTime Utc(int hour) =>
        new(2026, 9, 21, hour, 0, 0, DateTimeKind.Utc);

    private sealed class StubRepository(DataSubjectRequest request) : IDataSubjectRequestRepository
    {
        public int SaveCount { get; private set; }
        public Task<DataSubjectRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<DataSubjectRequest?>(request.Id == id ? request : null);
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
        public Task<bool> HasActiveAsync(Guid userId, DataSubjectRequestType requestType, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(DataSubjectRequest value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<DataSubjectRequest>> GetByRequesterAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DataSubjectRequest>>([]);
        public Task<(IReadOnlyList<DataSubjectRequest> Items, int TotalCount)> GetForReviewAsync(DataSubjectRequestStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) => Task.FromResult<(IReadOnlyList<DataSubjectRequest>, int)>(([request], 1));
    }

    private sealed class StubCurrentUser(Guid userId, string[] roles) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => roles;
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
