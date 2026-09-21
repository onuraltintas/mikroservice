using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAgreementDomainTests
{
    private const string ContentSha256 =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Publish_ShouldCreateImmutableVersionedDocument()
    {
        var effectiveAt = DateTime.UtcNow.AddDays(1);

        var document = CoachingAgreementDocument.Publish(
            "2026.1",
            "tr-TR",
            "Öğrenci Koçluk Anlaşması",
            "https://legal.example.test/coaching/2026.1",
            ContentSha256,
            effectiveAt);

        document.DocumentVersion.Should().Be("2026.1");
        document.Locale.Should().Be("tr-TR");
        document.ContentSha256.Should().Be(ContentSha256);
        document.EffectiveAt.Should().Be(effectiveAt);
        document.SupersededAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-sha")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    public void Publish_ShouldRejectInvalidContentHash(string contentSha256)
    {
        var action = () => CoachingAgreementDocument.Publish(
            "2026.1",
            "tr-TR",
            "Öğrenci Koçluk Anlaşması",
            "https://legal.example.test/coaching/2026.1",
            contentSha256,
            DateTime.UtcNow);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateAcknowledgement_ShouldRejectSelfAcknowledgementForAnotherStudent()
    {
        var action = () => CoachingAgreementAcknowledgement.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            CoachingAgreementPartyRole.Self,
            DateTime.UtcNow);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateAcknowledgement_ShouldRecordGuardianEvidenceWithoutPretendingItIsConsent()
    {
        var studentId = Guid.NewGuid();
        var guardianId = Guid.NewGuid();

        var acknowledgement = CoachingAgreementAcknowledgement.Create(
            Guid.NewGuid(),
            studentId,
            guardianId,
            CoachingAgreementPartyRole.LegalGuardian,
            DateTime.UtcNow);

        acknowledgement.SubjectStudentId.Should().Be(studentId);
        acknowledgement.AcknowledgedByUserId.Should().Be(guardianId);
        acknowledgement.PartyRole.Should().Be(CoachingAgreementPartyRole.LegalGuardian);
        acknowledgement.WithdrawnAt.Should().BeNull();
    }

    [Fact]
    public void Withdraw_ShouldPreserveAcceptanceEvidenceAndRecordWhoWithdrewIt()
    {
        var studentId = Guid.NewGuid();
        var acknowledgement = CoachingAgreementAcknowledgement.Create(
            Guid.NewGuid(),
            studentId,
            studentId,
            CoachingAgreementPartyRole.Self,
            DateTime.UtcNow.AddDays(-1));
        var withdrawnBy = Guid.NewGuid();
        var withdrawnAt = DateTime.UtcNow;

        acknowledgement.Withdraw(withdrawnBy, withdrawnAt);

        acknowledgement.WithdrawnAt.Should().Be(withdrawnAt);
        acknowledgement.WithdrawnByUserId.Should().Be(withdrawnBy);
        acknowledgement.AcknowledgedAt.Should().BeBefore(withdrawnAt);
    }
}
