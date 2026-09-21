using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

/// <summary>
/// Immutable published text that defines the terms of a coaching relationship.
/// A new legal text is represented by a new document instead of mutating history.
/// </summary>
public sealed class CoachingAgreementDocument : AggregateRoot
{
    public Guid? InstitutionId { get; private set; }
    public string DocumentVersion { get; private set; } = string.Empty;
    public string Locale { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string DocumentReference { get; private set; } = string.Empty;
    public string ContentSha256 { get; private set; } = string.Empty;
    public Guid PublishedByUserId { get; private set; }
    public DateTime EffectiveAt { get; private set; }
    public DateTime PublishedAt { get; private set; }
    public DateTime? SupersededAt { get; private set; }

    private CoachingAgreementDocument() { }

    public static CoachingAgreementDocument Publish(
        string version,
        string locale,
        string title,
        string documentReference,
        string contentSha256,
        DateTime effectiveAt,
        Guid publishedByUserId,
        Guid? institutionId = null)
    {
        ValidateRequired(version, nameof(version), 100);
        ValidateRequired(locale, nameof(locale), 20);
        ValidateRequired(title, nameof(title), 200);
        ValidateHttpsReference(documentReference);
        ValidateSha256(contentSha256);
        ValidateUtc(effectiveAt, nameof(effectiveAt));
        if (publishedByUserId == Guid.Empty)
            throw new ArgumentException("Publishing user is required.", nameof(publishedByUserId));

        return new CoachingAgreementDocument
        {
            InstitutionId = institutionId,
            DocumentVersion = version.Trim(),
            Locale = locale.Trim(),
            Title = title.Trim(),
            DocumentReference = documentReference.Trim(),
            ContentSha256 = contentSha256,
            PublishedByUserId = publishedByUserId,
            EffectiveAt = effectiveAt,
            PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Supersede(DateTime supersededAt)
    {
        ValidateUtc(supersededAt, nameof(supersededAt));
        if (supersededAt < PublishedAt)
            throw new ArgumentOutOfRangeException(nameof(supersededAt));

        SupersededAt ??= supersededAt;
        UpdatedAt = DateTime.UtcNow;
    }

    private static void ValidateRequired(string value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
            throw new ArgumentException($"{parameterName} is required and must not exceed {maximumLength} characters.", parameterName);
    }

    private static void ValidateHttpsReference(string documentReference)
    {
        if (!Uri.TryCreate(documentReference, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Document reference must be an absolute HTTPS URL.", nameof(documentReference));
        }
    }

    private static void ValidateSha256(string contentSha256)
    {
        if (contentSha256 is null
            || contentSha256.Length != 64
            || contentSha256.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("Content hash must be a lowercase SHA-256 hex value.", nameof(contentSha256));
        }
    }

    private static void ValidateUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC.", parameterName);
    }
}

/// <summary>
/// Append-oriented evidence that a party acknowledged one exact agreement version.
/// This record does not by itself assert a legal basis or replace a consent assessment.
/// </summary>
public sealed class CoachingAgreementAcknowledgement : Entity
{
    public Guid AgreementDocumentId { get; private set; }
    public CoachingAgreementDocument AgreementDocument { get; private set; } = null!;
    public Guid SubjectStudentId { get; private set; }
    public Guid AcknowledgedByUserId { get; private set; }
    public CoachingAgreementPartyRole PartyRole { get; private set; }
    public DateTime AcknowledgedAt { get; private set; }
    public DateTime? WithdrawnAt { get; private set; }
    public Guid? WithdrawnByUserId { get; private set; }

    private CoachingAgreementAcknowledgement() { }

    public static CoachingAgreementAcknowledgement Create(
        Guid agreementDocumentId,
        Guid subjectStudentId,
        Guid acknowledgedByUserId,
        CoachingAgreementPartyRole partyRole,
        DateTime acknowledgedAt)
    {
        if (agreementDocumentId == Guid.Empty)
            throw new ArgumentException("Agreement document is required.", nameof(agreementDocumentId));
        if (subjectStudentId == Guid.Empty)
            throw new ArgumentException("Student is required.", nameof(subjectStudentId));
        if (acknowledgedByUserId == Guid.Empty)
            throw new ArgumentException("Acknowledging user is required.", nameof(acknowledgedByUserId));
        if (!Enum.IsDefined(partyRole))
            throw new ArgumentOutOfRangeException(nameof(partyRole));
        if (partyRole == CoachingAgreementPartyRole.Self && subjectStudentId != acknowledgedByUserId)
            throw new ArgumentException("A self acknowledgement can only be recorded by the subject student.", nameof(acknowledgedByUserId));
        if (acknowledgedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Acknowledgement timestamp must be UTC.", nameof(acknowledgedAt));

        return new CoachingAgreementAcknowledgement
        {
            AgreementDocumentId = agreementDocumentId,
            SubjectStudentId = subjectStudentId,
            AcknowledgedByUserId = acknowledgedByUserId,
            PartyRole = partyRole,
            AcknowledgedAt = acknowledgedAt,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Withdraw(Guid withdrawnByUserId, DateTime withdrawnAt)
    {
        if (withdrawnByUserId == Guid.Empty)
            throw new ArgumentException("Withdrawing user is required.", nameof(withdrawnByUserId));
        if (withdrawnAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Withdrawal timestamp must be UTC.", nameof(withdrawnAt));
        if (withdrawnAt < AcknowledgedAt)
            throw new ArgumentOutOfRangeException(nameof(withdrawnAt));

        WithdrawnAt ??= withdrawnAt;
        WithdrawnByUserId ??= withdrawnByUserId;
        UpdatedAt = DateTime.UtcNow;
    }
}
