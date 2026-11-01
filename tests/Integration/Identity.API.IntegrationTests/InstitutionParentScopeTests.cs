using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

/// <summary>
/// V1 (parent panel, audit finding): a parent is not an
/// institution member, so a child's institution never becomes the parent's own scope; where an institution's people
/// include parents, only verified Mother/Father/Guardian links of its students count.
/// </summary>
public sealed class InstitutionParentScopeTests
{
    [Fact]
    public async Task AChildsInstitutionIsNeverTheParentsOwnScope()
    {
        await using var context = CreateContext();
        var s = Seed(context); await context.SaveChangesAsync();
        var institutions = new InstitutionRepository(context);

        (await institutions.GetPrimaryInstitutionIdByUserIdAsync(s.UnlinkedParent.Id, PlatformProduct.Coaching, default))
            .Should().BeNull("a parent without a link has no institution scope");
        (await institutions.GetPrimaryInstitutionIdByUserIdAsync(s.VerifiedParent.Id, PlatformProduct.Coaching, default))
            .Should().BeNull("a parent is not an institution member");
        (await institutions.GetPrimaryInstitutionIdByUserIdAsync(s.Student.Id, PlatformProduct.Coaching, default))
            .Should().Be(s.Institution.Id);
    }

    [Fact]
    public async Task TheInstitutionsPeopleIncludeOnlyVerifiedRepresentativesOfItsStudents()
    {
        await using var context = CreateContext();
        var s = Seed(context); await context.SaveChangesAsync();
        var institutions = new InstitutionRepository(context);
        var users = new UserRepository(context);

        (await institutions.IsUserInInstitutionAsync(s.VerifiedParent.Id, s.Institution.Id, PlatformProduct.Coaching, default)).Should().BeTrue();
        (await institutions.IsUserInInstitutionAsync(s.UnlinkedParent.Id, s.Institution.Id, PlatformProduct.Coaching, default)).Should().BeFalse();
        (await institutions.IsUserInInstitutionAsync(s.OtherRelative.Id, s.Institution.Id, PlatformProduct.Coaching, default)).Should().BeFalse();

        var page = await users.GetAllAsync(1, 50, null, null, null, s.Institution.Id, default);
        page.Items.Select(x => x.UserId).Should().BeEquivalentTo([s.Student.Id, s.VerifiedParent.Id]);
        (await users.GetSummaryAsync(s.Institution.Id, default)).TotalUsers.Should().Be(2);
    }

    [Fact]
    public async Task EndingAnInstitutionDoesNotSignOutParents()
    {
        await using var context = CreateContext();
        var s = Seed(context);
        var tokens = new[] { s.Student, s.UnlinkedParent, s.VerifiedParent }
            .ToDictionary(x => x.Id, x => RefreshToken.Create(x.Id, $"token-{x.Id:N}", DateTime.UtcNow.AddDays(1), null, product: PlatformProduct.Coaching));
        context.RefreshTokens.AddRange(tokens.Values);
        await context.SaveChangesAsync();

        await new UserRepository(context).RevokeActiveRefreshTokensForInstitutionAsync(s.Institution.Id, "Kurum kapatıldı.", default);

        tokens[s.Student.Id].RevokedAt.Should().NotBeNull();
        tokens[s.UnlinkedParent.Id].RevokedAt.Should().BeNull();
        tokens[s.VerifiedParent.Id].RevokedAt.Should().BeNull();
    }

    private sealed record Seeded(Institution Institution, User Student, User UnlinkedParent, User VerifiedParent, User OtherRelative);

    private static Seeded Seed(IdentityDbContext context)
    {
        var institution = Institution.Create("Kurum", InstitutionType.School);
        var student = User.Create(Guid.NewGuid(), "ogrenci@example.test", "Ada", "Öğrenci");
        var unlinked = User.Create(Guid.NewGuid(), "eski@example.test", "Eski", "Veli");
        var verified = User.Create(Guid.NewGuid(), "veli@example.test", "Ayşe", "Veli");
        var other = User.Create(Guid.NewGuid(), "akraba@example.test", "Can", "Akraba");
        context.AddRange(institution, student, unlinked, verified, other);
        context.StudentProfiles.Add(StudentProfile.Create(student.Id, "Ada", "Öğrenci", institution.Id));
        var at = DateTime.UtcNow.AddMinutes(-5);
        foreach (var (parent, relationship) in new[] { (verified, ParentRelationship.Mother), (other, ParentRelationship.Other) })
        {
            var link = ParentStudentRelationship.Request(parent.Id, student.Id, relationship, parent.Id, at);
            link.Verify(ParentStudentVerificationMethod.InvitationAcceptance, student.Id, at.AddMinutes(1));
            context.ParentStudentRelationships.Add(link);
        }
        return new Seeded(institution, student, unlinked, verified, other);
    }

    private static IdentityDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
