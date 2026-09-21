using System.Security.Claims;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.API.Controllers;
using Identity.Application.Commands.ManageParentStudentRelationships;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IdentityUserRole = Identity.Domain.Enums.UserRole;

namespace Identity.API.IntegrationTests;

public sealed class ParentStudentRelationshipManagementTests
{
    [Theory]
    [InlineData(nameof(ParentStudentRelationshipsController.RequestRelationship))]
    [InlineData(nameof(ParentStudentRelationshipsController.VerifyRelationship))]
    [InlineData(nameof(ParentStudentRelationshipsController.RevokeRelationship))]
    public void MutationActions_MustRequireSystemAdminMfaAndUserEditPermission(string actionName)
    {
        var method = typeof(ParentStudentRelationshipsController).GetMethod(actionName);

        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .Contain(attribute => attribute.Roles == "SystemAdmin")
            .And.Contain(attribute => attribute.Policy == "MfaRequired");
        method.GetCustomAttributes(typeof(HasPermissionAttribute), true)
            .Cast<HasPermissionAttribute>()
            .Should()
            .Contain(attribute => attribute.Policy == "Permissions.Users.Edit");
    }

    [Fact]
    public async Task Lifecycle_ShouldPersistActorAndEvidenceWithoutLosingVerificationHistory()
    {
        await using var context = CreateContext();
        var (parent, student) = AddEligibleUsers(context);
        await context.SaveChangesAsync();
        var actorId = Guid.NewGuid();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero));
        var repository = new ParentStudentRelationshipRepository(context);
        var unitOfWork = new UnitOfWork(context);
        var currentUser = new StubCurrentUserService(actorId, "SystemAdmin");

        var requestResult = await new RequestParentStudentRelationshipCommandHandler(
            repository, new UserRepository(context), unitOfWork, currentUser, clock)
            .Handle(new(parent.Id, student.Id, ParentRelationship.Mother), CancellationToken.None);

        requestResult.IsSuccess.Should().BeTrue();
        var relationship = await context.ParentStudentRelationships.SingleAsync();
        relationship.RequestedByUserId.Should().Be(actorId);
        relationship.Status.Should().Be(ParentStudentRelationshipStatus.Pending);

        clock.SetUtcNow(new DateTimeOffset(2026, 9, 21, 12, 5, 0, TimeSpan.Zero));
        var verifyResult = await new VerifyParentStudentRelationshipCommandHandler(
            repository, unitOfWork, currentUser, clock)
            .Handle(new(relationship.Id), CancellationToken.None);

        verifyResult.IsSuccess.Should().BeTrue();
        relationship.VerifiedByUserId.Should().Be(actorId);
        relationship.VerifiedAt.Should().Be(clock.GetUtcNow().UtcDateTime);

        clock.SetUtcNow(new DateTimeOffset(2026, 9, 21, 12, 10, 0, TimeSpan.Zero));
        var revokeResult = await new RevokeParentStudentRelationshipCommandHandler(
            repository, unitOfWork, currentUser, clock)
            .Handle(new(relationship.Id, "Mahkeme kararı güncellendi."), CancellationToken.None);

        revokeResult.IsSuccess.Should().BeTrue();
        relationship.Status.Should().Be(ParentStudentRelationshipStatus.Revoked);
        relationship.VerifiedByUserId.Should().Be(actorId);
        relationship.RevocationReason.Should().Be("Mahkeme kararı güncellendi.");
    }

    [Fact]
    public async Task Request_ShouldRejectDuplicateActiveRelationship()
    {
        await using var context = CreateContext();
        var (parent, student) = AddEligibleUsers(context);
        var actorId = Guid.NewGuid();
        context.ParentStudentRelationships.Add(ParentStudentRelationship.Request(
            parent.Id, student.Id, ParentRelationship.Father, actorId, DateTime.UtcNow));
        await context.SaveChangesAsync();
        var handler = new RequestParentStudentRelationshipCommandHandler(
            new ParentStudentRelationshipRepository(context),
            new UserRepository(context),
            new UnitOfWork(context),
            new StubCurrentUserService(actorId, "SystemAdmin"),
            TimeProvider.System);

        var result = await handler.Handle(
            new(parent.Id, student.Id, ParentRelationship.Father), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ParentStudentRelationship.AlreadyActive");
    }

    [Fact]
    public async Task Request_ShouldRejectUsersWithoutRequiredRoles()
    {
        await using var context = CreateContext();
        var first = User.Create(Guid.NewGuid(), "first@example.test");
        var second = User.Create(Guid.NewGuid(), "second@example.test");
        context.Users.AddRange(first, second);
        await context.SaveChangesAsync();
        var handler = new RequestParentStudentRelationshipCommandHandler(
            new ParentStudentRelationshipRepository(context),
            new UserRepository(context),
            new UnitOfWork(context),
            new StubCurrentUserService(Guid.NewGuid(), "SystemAdmin"),
            TimeProvider.System);

        var result = await handler.Handle(
            new(first.Id, second.Id, ParentRelationship.Guardian), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ParentStudentRelationship.InvalidSubjects");
    }

    [Fact]
    public async Task Request_ShouldRejectNonAdministratorAtCommandBoundary()
    {
        await using var context = CreateContext();
        var (parent, student) = AddEligibleUsers(context);
        await context.SaveChangesAsync();
        var handler = new RequestParentStudentRelationshipCommandHandler(
            new ParentStudentRelationshipRepository(context),
            new UserRepository(context),
            new UnitOfWork(context),
            new StubCurrentUserService(Guid.NewGuid(), "Teacher"),
            TimeProvider.System);

        var result = await handler.Handle(
            new(parent.Id, student.Id, ParentRelationship.Guardian), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ParentStudentRelationship.Forbidden");
        context.ParentStudentRelationships.Should().BeEmpty();
    }

    [Fact]
    public async Task Revoke_ShouldRequireMeaningfulBoundedReason()
    {
        await using var context = CreateContext();
        var (parent, student) = AddEligibleUsers(context);
        var actorId = Guid.NewGuid();
        var relationship = ParentStudentRelationship.Request(
            parent.Id, student.Id, ParentRelationship.Guardian, actorId, DateTime.UtcNow);
        context.ParentStudentRelationships.Add(relationship);
        await context.SaveChangesAsync();
        var handler = new RevokeParentStudentRelationshipCommandHandler(
            new ParentStudentRelationshipRepository(context),
            new UnitOfWork(context),
            new StubCurrentUserService(actorId, "SystemAdmin"),
            TimeProvider.System);

        var result = await handler.Handle(new(relationship.Id, "   "), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ParentStudentRelationship.InvalidRevocationReason");
        relationship.Status.Should().Be(ParentStudentRelationshipStatus.Pending);
    }

    private static (User Parent, User Student) AddEligibleUsers(IdentityDbContext context)
    {
        var parentRole = Role.Create(IdentityUserRole.Parent.ToString(), "Parent", true);
        var studentRole = Role.Create(IdentityUserRole.Student.ToString(), "Student", true);
        var parent = User.Create(Guid.NewGuid(), "parent@example.test");
        var student = User.Create(Guid.NewGuid(), "student@example.test");
        parent.AddRole(new Identity.Domain.Entities.UserRole(parent.Id, parentRole.Id));
        student.AddRole(new Identity.Domain.Entities.UserRole(student.Id, studentRole.Id));
        context.AddRange(parentRole, studentRole, parent, student);
        return (parent, student);
    }

    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }

    private sealed class StubCurrentUserService(Guid userId, params string[] roles) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => "admin@example.test";
        public string? FullName => "System Admin";
        public IEnumerable<string> Roles => roles;
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void SetUtcNow(DateTimeOffset value) => _utcNow = value;
    }
}
