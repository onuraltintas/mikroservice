using FluentAssertions;
using Identity.API.Controllers;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using IdentityUserRole = Identity.Domain.Enums.UserRole;

namespace Identity.API.IntegrationTests;

public sealed class ParentStudentRelationshipManagementTests
{
    // V1 (audit finding): the admin parent-student controller had no gateway route and no client; links are made by the
    // student and parent themselves (ParentLinksController).
    [Fact]
    public void TheUnroutedLegacyAdminControllerAndItsCommandsAreGone()
    {
        var api = typeof(ParentLinksController).Assembly;
        api.GetType("Identity.API.Controllers.ParentStudentRelationshipsController").Should().BeNull();
        var application = typeof(Identity.Application.ParentLinks.IParentLinkService).Assembly;
        application.GetTypes().Where(type => type.Namespace is "Identity.Application.Commands.ManageParentStudentRelationships"
                or "Identity.Application.Queries.GetParentStudentRelationships")
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(ParentRelationship.Mother, "Parent")]
    [InlineData(ParentRelationship.Father, "Parent")]
    [InlineData(ParentRelationship.Guardian, "LegalGuardian")]
    public async Task IdentityAuthorization_ShouldMapOnlyVerifiedRepresentativeRoles(
        ParentRelationship relationshipType,
        string expectedPartyRole)
    {
        await using var context = CreateContext();
        var (parent, student) = AddEligibleUsers(context);
        var actorId = Guid.NewGuid();
        var relationship = ParentStudentRelationship.Request(
            parent.Id, student.Id, relationshipType, actorId, DateTime.UtcNow.AddMinutes(-1));
        relationship.Verify(ParentStudentVerificationMethod.ManualReview, actorId, DateTime.UtcNow);
        context.ParentStudentRelationships.Add(relationship);
        await context.SaveChangesAsync();

        var result = await new ParentStudentRelationshipRepository(context)
            .AuthorizeCoachingAgreementRepresentativeAsync(parent.Id, student.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.RelationshipId.Should().Be(relationship.Id);
        result.PartyRole.Should().Be(expectedPartyRole);
    }

    [Theory]
    [InlineData(ParentRelationship.Other, true)]
    [InlineData(ParentRelationship.Guardian, false)]
    public async Task IdentityAuthorization_ShouldRejectOtherAndUnverifiedRelationships(
        ParentRelationship relationshipType,
        bool verify)
    {
        await using var context = CreateContext();
        var (parent, student) = AddEligibleUsers(context);
        var actorId = Guid.NewGuid();
        var relationship = ParentStudentRelationship.Request(
            parent.Id, student.Id, relationshipType, actorId, DateTime.UtcNow.AddMinutes(-1));
        if (verify)
            relationship.Verify(ParentStudentVerificationMethod.ManualReview, actorId, DateTime.UtcNow);
        context.ParentStudentRelationships.Add(relationship);
        await context.SaveChangesAsync();

        var result = await new ParentStudentRelationshipRepository(context)
            .AuthorizeCoachingAgreementRepresentativeAsync(parent.Id, student.Id, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task IdentityAuthorization_ShouldRejectRevokedRelationship()
    {
        await using var context = CreateContext();
        var (parent, student) = AddEligibleUsers(context);
        var actorId = Guid.NewGuid();
        var requestedAt = DateTime.UtcNow.AddMinutes(-2);
        var relationship = ParentStudentRelationship.Request(
            parent.Id, student.Id, ParentRelationship.Guardian, actorId, requestedAt);
        relationship.Verify(ParentStudentVerificationMethod.ManualReview, actorId, requestedAt.AddMinutes(1));
        relationship.Revoke(actorId, DateTime.UtcNow, "Yetki sona erdi.");
        context.ParentStudentRelationships.Add(relationship);
        await context.SaveChangesAsync();

        var result = await new ParentStudentRelationshipRepository(context)
            .AuthorizeCoachingAgreementRepresentativeAsync(parent.Id, student.Id, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public void InternalRepresentativeAuthorization_MustRequireServiceKey()
    {
        typeof(InternalCoachingController)
            .GetMethod(nameof(InternalCoachingController.AuthorizeAgreementRepresentative))!
            .GetCustomAttributes(typeof(InternalServiceKeyAttribute), true)
            .Should().NotBeEmpty();
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
}
