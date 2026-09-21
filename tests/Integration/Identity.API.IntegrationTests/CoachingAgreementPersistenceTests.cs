using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
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

    [Fact]
    public async Task Repository_ShouldSelectLatestEffectiveGlobalDocumentForLocale()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
        var older = CreateDocument("2026.1", "tr-TR", now.AddDays(-10));
        var current = CreateDocument("2026.2", "tr-TR", now.AddDays(-1));
        var future = CreateDocument("2026.3", "tr-TR", now.AddDays(1));
        var otherLocale = CreateDocument("2026.2", "en-US", now.AddDays(-1));
        context.AddRange(older, current, future, otherLocale);
        await context.SaveChangesAsync();
        var repository = new CoachingAgreementRepository(context);

        var result = await repository.GetCurrentAsync("tr-TR", now);

        result!.Id.Should().Be(current.Id);
    }

    [Fact]
    public async Task Repository_ShouldIgnoreWithdrawnAcknowledgementWhenLookingForActiveEvidence()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
        var studentId = Guid.NewGuid();
        var document = CreateDocument("2026.1", "tr-TR", now.AddDays(-1));
        var evidence = CoachingAgreementAcknowledgement.Create(
            document.Id,
            studentId,
            studentId,
            Coaching.Domain.Enums.CoachingAgreementPartyRole.Self,
            now.AddMinutes(-1));
        evidence.Withdraw(studentId, now);
        context.AddRange(document, evidence);
        await context.SaveChangesAsync();
        var repository = new CoachingAgreementRepository(context);

        var result = await repository.GetActiveSelfAcknowledgementAsync(document.Id, studentId);

        result.Should().BeNull();
    }

    private static CoachingAgreementDocument CreateDocument(
        string version,
        string locale,
        DateTime effectiveAt) => CoachingAgreementDocument.Publish(
            version,
            locale,
            "Koçluk Anlaşması",
            $"https://legal.example.test/coaching/{locale}/{version}",
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            effectiveAt,
            Guid.NewGuid());

    private static CoachingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CoachingDbContext(options);
    }
}
