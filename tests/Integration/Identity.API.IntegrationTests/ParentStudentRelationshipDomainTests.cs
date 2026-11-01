using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class ParentStudentRelationshipDomainTests
{
    private static readonly DateTime RequestedAt =
        new(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Request_ShouldStartPendingWithoutVerificationClaims()
    {
        var relationship = ParentStudentRelationship.Request(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ParentRelationship.Mother,
            Guid.NewGuid(),
            RequestedAt);

        relationship.Status.Should().Be(ParentStudentRelationshipStatus.Pending);
        relationship.VerifiedAt.Should().BeNull();
        relationship.VerifiedByUserId.Should().BeNull();
        relationship.VerificationMethod.Should().BeNull();
    }

    [Fact]
    public void Verify_ShouldCaptureActorMethodAndUtcTimestamp()
    {
        var relationship = CreatePending();
        var verifierId = Guid.NewGuid();
        var verifiedAt = RequestedAt.AddHours(1);

        relationship.Verify(
            ParentStudentVerificationMethod.InstitutionAdministrator,
            verifierId,
            verifiedAt);

        relationship.Status.Should().Be(ParentStudentRelationshipStatus.Verified);
        relationship.VerificationMethod.Should().Be(ParentStudentVerificationMethod.InstitutionAdministrator);
        relationship.VerifiedByUserId.Should().Be(verifierId);
        relationship.VerifiedAt.Should().Be(verifiedAt);
    }

    [Fact]
    public void Verify_ShouldRejectRepeatedVerification()
    {
        var relationship = CreatePending();
        relationship.Verify(
            ParentStudentVerificationMethod.ManualReview,
            Guid.NewGuid(),
            RequestedAt.AddHours(1));

        var act = () => relationship.Verify(
            ParentStudentVerificationMethod.ManualReview,
            Guid.NewGuid(),
            RequestedAt.AddHours(2));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Revoke_ShouldPreserveVerificationEvidence()
    {
        var relationship = CreatePending();
        var verifierId = Guid.NewGuid();
        relationship.Verify(
            ParentStudentVerificationMethod.InstitutionAdministrator,
            verifierId,
            RequestedAt.AddHours(1));

        relationship.Revoke(Guid.NewGuid(), RequestedAt.AddHours(2), "Veli yetkisi sona erdi.");

        relationship.Status.Should().Be(ParentStudentRelationshipStatus.Revoked);
        relationship.VerifiedByUserId.Should().Be(verifierId);
        relationship.VerifiedAt.Should().NotBeNull();
        relationship.RevokedAt.Should().Be(RequestedAt.AddHours(2));
        relationship.RevocationReason.Should().Be("Veli yetkisi sona erdi.");
    }

    [Fact]
    public void Request_ShouldRejectSelfRelationshipAndLocalTimestamp()
    {
        var userId = Guid.NewGuid();

        var selfAct = () => ParentStudentRelationship.Request(
            userId,
            userId,
            ParentRelationship.Guardian,
            Guid.NewGuid(),
            RequestedAt);
        var localTimeAct = () => ParentStudentRelationship.Request(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ParentRelationship.Guardian,
            Guid.NewGuid(),
            DateTime.Now);

        selfAct.Should().Throw<ArgumentException>();
        localTimeAct.Should().Throw<ArgumentException>();
    }

    // V1 (parent panel): the student chooses per product what the parent sees; both start visible.
    [Fact]
    public void ProductVisibility_StartsVisibleAndOnlyTheStudentChangesItOnAnOpenLink()
    {
        var relationship = CreatePending();
        (relationship.CoachingVisible, relationship.SpeedReadingVisible).Should().Be((true, true));
        var at = RequestedAt.AddHours(1);

        var byParent = () => relationship.SetProductVisibility(false, false, relationship.ParentUserId, at);
        var localTime = () => relationship.SetProductVisibility(false, false, relationship.StudentUserId, DateTime.Now);
        byParent.Should().Throw<InvalidOperationException>();
        localTime.Should().Throw<ArgumentException>();

        relationship.SetProductVisibility(false, true, relationship.StudentUserId, at);
        (relationship.CoachingVisible, relationship.SpeedReadingVisible, relationship.UpdatedAt).Should().Be((false, true, (DateTime?)at));

        relationship.Revoke(relationship.StudentUserId, at, "Öğrenci kaldırdı.");
        var afterRevoke = () => relationship.SetProductVisibility(true, true, relationship.StudentUserId, at);
        afterRevoke.Should().Throw<InvalidOperationException>();
        (relationship.CoachingVisible, relationship.SpeedReadingVisible).Should().Be((false, true));
    }

    private static ParentStudentRelationship CreatePending() =>
        ParentStudentRelationship.Request(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ParentRelationship.Father,
            Guid.NewGuid(),
            RequestedAt);
}
