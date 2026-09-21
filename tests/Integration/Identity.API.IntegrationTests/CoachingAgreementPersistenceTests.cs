using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAgreementPersistenceTests
{
    [Fact]
    public async Task AgreementEvidence_ShouldRoundTripWithoutMutatingDocumentContent()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var document = CoachingAgreementDocument.Publish(
            "2026.1",
            "tr-TR",
            "Öğrenci Koçluk Anlaşması",
            "https://legal.example.test/coaching/2026.1",
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            DateTime.UtcNow,
            Guid.NewGuid());
        var acknowledgement = CoachingAgreementAcknowledgement.Create(
            document.Id,
            studentId,
            studentId,
            Coaching.Domain.Enums.CoachingAgreementPartyRole.Self,
            DateTime.UtcNow);

        context.CoachingAgreementDocuments.Add(document);
        context.CoachingAgreementAcknowledgements.Add(acknowledgement);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var saved = await context.CoachingAgreementAcknowledgements
            .Include(item => item.AgreementDocument)
            .SingleAsync();
        saved.SubjectStudentId.Should().Be(studentId);
        saved.AgreementDocument.DocumentVersion.Should().Be("2026.1");
        saved.AgreementDocument.ContentSha256.Should().Be(document.ContentSha256);
    }

    [Fact]
    public void AgreementModel_ShouldPreventDuplicateActiveEvidence()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(CoachingAgreementAcknowledgement));

        var activeEvidenceIndex = entityType!.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
            [
                nameof(CoachingAgreementAcknowledgement.AgreementDocumentId),
                nameof(CoachingAgreementAcknowledgement.SubjectStudentId),
                nameof(CoachingAgreementAcknowledgement.AcknowledgedByUserId),
                nameof(CoachingAgreementAcknowledgement.PartyRole)
            ]));

        activeEvidenceIndex.IsUnique.Should().BeTrue();
        activeEvidenceIndex.GetFilter().Should().Be("withdrawn_at IS NULL");
    }

    private static CoachingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CoachingDbContext(options);
    }
}
