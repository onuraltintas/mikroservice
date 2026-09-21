using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Repositories;

public sealed class CoachingAgreementRepository(CoachingDbContext context)
    : ICoachingAgreementRepository
{
    public Task<CoachingAgreementDocument?> GetDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        context.CoachingAgreementDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(document => document.Id == documentId, cancellationToken);

    public Task<CoachingAgreementDocument?> GetCurrentAsync(
        string locale,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedLocale = locale.Trim();
        return context.CoachingAgreementDocuments
            .AsNoTracking()
            .Where(document => document.InstitutionId == null)
            .Where(document => document.Locale == normalizedLocale)
            .Where(document => document.EffectiveAt <= asOfUtc)
            .Where(document => document.SupersededAt == null || document.SupersededAt > asOfUtc)
            .OrderByDescending(document => document.EffectiveAt)
            .ThenByDescending(document => document.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<CoachingAgreementAcknowledgement?> GetActiveSelfAcknowledgementAsync(
        Guid documentId,
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        context.CoachingAgreementAcknowledgements
            .AsNoTracking()
            .SingleOrDefaultAsync(
                acknowledgement => acknowledgement.AgreementDocumentId == documentId
                    && acknowledgement.SubjectStudentId == studentId
                    && acknowledgement.AcknowledgedByUserId == studentId
                    && acknowledgement.PartyRole == CoachingAgreementPartyRole.Self
                    && acknowledgement.WithdrawnAt == null,
                cancellationToken);

    public Task<CoachingAgreementAcknowledgement?> GetActiveAcknowledgementForStudentAsync(
        Guid documentId,
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        context.CoachingAgreementAcknowledgements
            .AsNoTracking()
            .Where(acknowledgement => acknowledgement.AgreementDocumentId == documentId)
            .Where(acknowledgement => acknowledgement.SubjectStudentId == studentId)
            .Where(acknowledgement => acknowledgement.WithdrawnAt == null)
            .OrderByDescending(acknowledgement => acknowledgement.AcknowledgedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<CoachingAgreementAcknowledgement?> GetAcknowledgementAsync(
        Guid acknowledgementId,
        CancellationToken cancellationToken = default) =>
        context.CoachingAgreementAcknowledgements
            .SingleOrDefaultAsync(
                acknowledgement => acknowledgement.Id == acknowledgementId,
                cancellationToken);

    public Task<CoachingAgreementAcknowledgement?> GetActiveAcknowledgementAsync(
        Guid documentId,
        Guid studentId,
        Guid acknowledgedByUserId,
        CoachingAgreementPartyRole partyRole,
        CancellationToken cancellationToken = default) =>
        context.CoachingAgreementAcknowledgements
            .AsNoTracking()
            .SingleOrDefaultAsync(
                acknowledgement => acknowledgement.AgreementDocumentId == documentId
                    && acknowledgement.SubjectStudentId == studentId
                    && acknowledgement.AcknowledgedByUserId == acknowledgedByUserId
                    && acknowledgement.PartyRole == partyRole
                    && acknowledgement.WithdrawnAt == null,
                cancellationToken);

    public Task AddDocumentAsync(
        CoachingAgreementDocument document,
        CancellationToken cancellationToken = default) =>
        context.CoachingAgreementDocuments.AddAsync(document, cancellationToken).AsTask();

    public Task AddAcknowledgementAsync(
        CoachingAgreementAcknowledgement acknowledgement,
        CancellationToken cancellationToken = default) =>
        context.CoachingAgreementAcknowledgements.AddAsync(acknowledgement, cancellationToken).AsTask();
}
