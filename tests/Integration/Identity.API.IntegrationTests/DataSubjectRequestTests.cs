using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class DataSubjectRequestTests
{
    [Fact]
    public void Create_ShouldRecordVerifiedRequesterAndSubmissionTime()
    {
        var userId = Guid.NewGuid();
        var submittedAt = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

        var request = DataSubjectRequest.Create(
            userId,
            DataSubjectRequestType.Erasure,
            "Koçluk verilerimin silinmesini istiyorum.",
            submittedAt);

        request.RequesterUserId.Should().Be(userId);
        request.Status.Should().Be(DataSubjectRequestStatus.Submitted);
        request.SubmittedAt.Should().Be(submittedAt);
    }

    [Fact]
    public void Create_ShouldRejectEmptyReason()
    {
        var action = () => DataSubjectRequest.Create(
            Guid.NewGuid(),
            DataSubjectRequestType.Erasure,
            " ",
            DateTime.UtcNow);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void VerifyIdentity_ThenApprove_ShouldPreserveDecisionEvidence()
    {
        var request = DataSubjectRequest.Create(
            Guid.NewGuid(),
            DataSubjectRequestType.Erasure,
            "Hesabımı kapatıyorum.",
            DateTime.UtcNow);
        var reviewerId = Guid.NewGuid();
        var verifiedAt = DateTime.UtcNow.AddMinutes(1);
        var decidedAt = verifiedAt.AddMinutes(1);

        request.VerifyIdentity(verifiedAt);
        request.Approve(reviewerId, "Yasal saklama kapsamı dışındaki veriler.", decidedAt);

        request.Status.Should().Be(DataSubjectRequestStatus.Approved);
        request.IdentityVerifiedAt.Should().Be(verifiedAt);
        request.DecidedByUserId.Should().Be(reviewerId);
        request.DecisionReason.Should().Be("Yasal saklama kapsamı dışındaki veriler.");
        request.DecidedAt.Should().Be(decidedAt);
    }

    [Fact]
    public void Complete_ShouldRequireProcessingState()
    {
        var request = DataSubjectRequest.Create(
            Guid.NewGuid(),
            DataSubjectRequestType.Erasure,
            "Hesabımı kapatıyorum.",
            DateTime.UtcNow);

        var action = () => request.Complete(DateTime.UtcNow.AddMinutes(1));

        action.Should().Throw<InvalidOperationException>();
    }
}
