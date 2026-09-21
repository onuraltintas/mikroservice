using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Queries.GetMyChildren;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class ParentChildrenQueryTests
{
    [Fact]
    public async Task Returns_only_active_children_of_the_current_parent()
    {
        await using var context = CreateContext();
        var parentUser = User.Create(Guid.NewGuid(), "parent@test.local", "Parent", "User");
        var otherParent = User.Create(Guid.NewGuid(), "other-parent@test.local", "Other", "Parent");
        var activeChild = User.Create(Guid.NewGuid(), "child@test.local", "Active", "Child");
        var inactiveChild = User.Create(Guid.NewGuid(), "inactive-child@test.local", "Inactive", "Child");
        var otherChild = User.Create(Guid.NewGuid(), "other-child@test.local", "Other", "Child");
        var parentRole = Role.Create(Identity.Domain.Enums.UserRole.Parent.ToString(), "Parent", true);
        var studentRole = Role.Create(Identity.Domain.Enums.UserRole.Student.ToString(), "Student", true);
        parentUser.AddRole(new Identity.Domain.Entities.UserRole(parentUser.Id, parentRole.Id));
        otherParent.AddRole(new Identity.Domain.Entities.UserRole(otherParent.Id, parentRole.Id));
        activeChild.AddRole(new Identity.Domain.Entities.UserRole(activeChild.Id, studentRole.Id));
        inactiveChild.AddRole(new Identity.Domain.Entities.UserRole(inactiveChild.Id, studentRole.Id));
        otherChild.AddRole(new Identity.Domain.Entities.UserRole(otherChild.Id, studentRole.Id));

        context.AddRange(parentRole, studentRole, parentUser, otherParent, activeChild, inactiveChild, otherChild);
        context.ParentProfiles.Add(ParentProfile.Create(parentUser.Id, "Parent", "User"));
        context.ParentProfiles.Add(ParentProfile.Create(otherParent.Id, "Other", "Parent"));
        var activeProfile = StudentProfile.Create(activeChild.Id, "Active", "Child", parentId: parentUser.Id);
        var inactiveProfile = StudentProfile.Create(inactiveChild.Id, "Inactive", "Child", parentId: parentUser.Id);
        inactiveProfile.Deactivate();
        var otherProfile = StudentProfile.Create(otherChild.Id, "Other", "Child", parentId: otherParent.Id);
        context.StudentProfiles.AddRange(activeProfile, inactiveProfile, otherProfile);
        var verified = ParentStudentRelationship.Request(
            parentUser.Id, activeChild.Id, ParentRelationship.Mother, parentUser.Id, DateTime.UtcNow.AddMinutes(-1));
        verified.Verify(ParentStudentVerificationMethod.ManualReview, Guid.NewGuid(), DateTime.UtcNow);
        var inactiveVerified = ParentStudentRelationship.Request(
            parentUser.Id, inactiveChild.Id, ParentRelationship.Guardian, parentUser.Id, DateTime.UtcNow.AddMinutes(-1));
        inactiveVerified.Verify(ParentStudentVerificationMethod.ManualReview, Guid.NewGuid(), DateTime.UtcNow);
        var unsupportedVerified = ParentStudentRelationship.Request(
            parentUser.Id, otherChild.Id, ParentRelationship.Other, parentUser.Id, DateTime.UtcNow.AddMinutes(-1));
        unsupportedVerified.Verify(ParentStudentVerificationMethod.ManualReview, Guid.NewGuid(), DateTime.UtcNow);
        context.ParentStudentRelationships.AddRange(verified, inactiveVerified, unsupportedVerified);
        await context.SaveChangesAsync();

        var handler = new GetMyChildrenQueryHandler(
            new ParentStudentRelationshipRepository(context),
            new StubCurrentUserService(parentUser.Id, "Parent"));

        var result = await handler.Handle(new GetMyChildrenQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(child => child.UserId == activeChild.Id);
        result.Value.Single().RelationshipId.Should().Be(verified.Id);
        result.Value.Single().Relationship.Should().Be("Mother");
        result.Value.Should().NotContain(child => child.UserId == inactiveChild.Id);
        result.Value.Should().NotContain(child => child.UserId == otherChild.Id);
    }

    [Fact]
    public async Task Rejects_non_parent_roles_without_querying_children()
    {
        await using var context = CreateContext();
        var student = User.Create(Guid.NewGuid(), "student@test.local", "Student", "User");
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var handler = new GetMyChildrenQueryHandler(
            new ParentStudentRelationshipRepository(context),
            new StubCurrentUserService(student.Id, "Student"));

        var result = await handler.Handle(new GetMyChildrenQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Error.Forbidden");
    }

    [Fact]
    public async Task CoachingReadAuthorization_RequiresVerifiedSupportedRelationshipInsteadOfLegacyParentId()
    {
        await using var context = CreateContext();
        var parentRole = Role.Create(Identity.Domain.Enums.UserRole.Parent.ToString(), "Parent", true);
        var studentRole = Role.Create(Identity.Domain.Enums.UserRole.Student.ToString(), "Student", true);
        var parent = User.Create(Guid.NewGuid(), "parent@test.local");
        var verifiedChild = User.Create(Guid.NewGuid(), "verified@test.local");
        var legacyOnlyChild = User.Create(Guid.NewGuid(), "legacy@test.local");
        parent.AddRole(new Identity.Domain.Entities.UserRole(parent.Id, parentRole.Id));
        verifiedChild.AddRole(new Identity.Domain.Entities.UserRole(verifiedChild.Id, studentRole.Id));
        legacyOnlyChild.AddRole(new Identity.Domain.Entities.UserRole(legacyOnlyChild.Id, studentRole.Id));
        context.AddRange(parentRole, studentRole, parent, verifiedChild, legacyOnlyChild);
        context.ParentProfiles.Add(ParentProfile.Create(parent.Id, "Parent", "User"));
        context.StudentProfiles.AddRange(
            StudentProfile.Create(verifiedChild.Id, "Verified", "Child"),
            StudentProfile.Create(legacyOnlyChild.Id, "Legacy", "Child", parentId: parent.Id));
        var relationship = ParentStudentRelationship.Request(
            parent.Id, verifiedChild.Id, ParentRelationship.Father, parent.Id, DateTime.UtcNow.AddMinutes(-1));
        relationship.Verify(ParentStudentVerificationMethod.ManualReview, Guid.NewGuid(), DateTime.UtcNow);
        var unsupportedRelationship = ParentStudentRelationship.Request(
            parent.Id, legacyOnlyChild.Id, ParentRelationship.Other, parent.Id, DateTime.UtcNow.AddMinutes(-1));
        unsupportedRelationship.Verify(ParentStudentVerificationMethod.ManualReview, Guid.NewGuid(), DateTime.UtcNow);
        context.ParentStudentRelationships.AddRange(relationship, unsupportedRelationship);
        await context.SaveChangesAsync();

        var result = await new InstitutionRepository(context).AuthorizeCoachingStudentReadAsync(
            parent.Id,
            [verifiedChild.Id, legacyOnlyChild.Id],
            null,
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.AllowedStudentUserIds.Should().Equal(verifiedChild.Id);
    }

    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        private readonly Guid _userId;
        private readonly string[] _roles;

        public StubCurrentUserService(Guid userId, params string[] roles)
        {
            _userId = userId;
            _roles = roles;
        }

        public Guid? UserId => _userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => _roles;
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
