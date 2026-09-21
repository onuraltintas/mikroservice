using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentValidation;
using MediatR;

namespace Coaching.Application.CoachingAgreements;

public sealed record PublishCoachingAgreementCommand(
    string DocumentVersion,
    string Locale,
    string Title,
    string DocumentReference,
    string ContentSha256,
    DateTime EffectiveAt) : IRequest<PublishCoachingAgreementResponse>, IBypassesCoachingAgreementRequirement;

public sealed record PublishCoachingAgreementResponse(Guid DocumentId);

public sealed class PublishCoachingAgreementValidator : AbstractValidator<PublishCoachingAgreementCommand>
{
    public PublishCoachingAgreementValidator()
    {
        RuleFor(command => command.DocumentVersion).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Locale).NotEmpty().MaximumLength(20);
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.DocumentReference).NotEmpty().Must(reference =>
            Uri.TryCreate(reference, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps);
        RuleFor(command => command.ContentSha256).Matches("^[0-9a-f]{64}$");
        RuleFor(command => command.EffectiveAt).Must(value => value.Kind == DateTimeKind.Utc)
            .WithMessage("EffectiveAt must be UTC.");
    }
}

public sealed class PublishCoachingAgreementHandler(
    ICoachingAgreementRepository repository,
    IUnitOfWork unitOfWork,
    ICoachingAccessPolicy accessPolicy)
    : IRequestHandler<PublishCoachingAgreementCommand, PublishCoachingAgreementResponse>
{
    public async Task<PublishCoachingAgreementResponse> Handle(
        PublishCoachingAgreementCommand command,
        CancellationToken cancellationToken)
    {
        if (!accessPolicy.IsSystemAdministrator || accessPolicy.CurrentUserId is not { } publisherId)
            throw Forbidden("Koçluk anlaşması yalnızca sistem yöneticisi tarafından yayımlanabilir.");

        var document = CoachingAgreementDocument.Publish(
            command.DocumentVersion,
            command.Locale,
            command.Title,
            command.DocumentReference,
            command.ContentSha256,
            command.EffectiveAt,
            publisherId);

        await repository.AddDocumentAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new PublishCoachingAgreementResponse(document.Id);
    }

    private static BusinessRuleException Forbidden(string message) =>
        new("Authorization.Forbidden", message);
}

public sealed record GetCurrentCoachingAgreementQuery(string Locale)
    : IRequest<CurrentCoachingAgreementResponse?>, IBypassesCoachingAgreementRequirement;

public sealed record CurrentCoachingAgreementResponse(
    Guid DocumentId,
    string DocumentVersion,
    string Locale,
    string Title,
    string DocumentReference,
    string ContentSha256,
    DateTime EffectiveAt,
    bool AcknowledgedByCurrentStudent,
    Guid? AcknowledgementId);

public sealed class GetCurrentCoachingAgreementValidator : AbstractValidator<GetCurrentCoachingAgreementQuery>
{
    public GetCurrentCoachingAgreementValidator() =>
        RuleFor(query => query.Locale).NotEmpty().MaximumLength(20);
}

public sealed class GetCurrentCoachingAgreementHandler(
    ICoachingAgreementRepository repository,
    ICoachingAccessPolicy accessPolicy,
    TimeProvider timeProvider)
    : IRequestHandler<GetCurrentCoachingAgreementQuery, CurrentCoachingAgreementResponse?>
{
    public async Task<CurrentCoachingAgreementResponse?> Handle(
        GetCurrentCoachingAgreementQuery query,
        CancellationToken cancellationToken)
    {
        var document = await repository.GetCurrentAsync(
            query.Locale.Trim(),
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        if (document is null)
            return null;

        CoachingAgreementAcknowledgement? acknowledgement = null;
        if (accessPolicy.CurrentUserId is { } userId && accessPolicy.IsCurrentStudent(userId))
        {
            acknowledgement = await repository.GetActiveSelfAcknowledgementAsync(
                document.Id,
                userId,
                cancellationToken);
        }

        return new CurrentCoachingAgreementResponse(
            document.Id,
            document.DocumentVersion,
            document.Locale,
            document.Title,
            document.DocumentReference,
            document.ContentSha256,
            document.EffectiveAt,
            acknowledgement is not null,
            acknowledgement?.Id);
    }
}

public sealed record AcknowledgeCurrentCoachingAgreementCommand(Guid AgreementDocumentId)
    : IRequest<CoachingAgreementAcknowledgementResponse>, IBypassesCoachingAgreementRequirement;

public sealed class AcknowledgeCurrentCoachingAgreementValidator
    : AbstractValidator<AcknowledgeCurrentCoachingAgreementCommand>
{
    public AcknowledgeCurrentCoachingAgreementValidator() =>
        RuleFor(command => command.AgreementDocumentId).NotEmpty();
}

public sealed record CoachingAgreementAcknowledgementResponse(
    Guid AcknowledgementId,
    Guid AgreementDocumentId,
    string Locale,
    DateTime AcknowledgedAt,
    DateTime? WithdrawnAt);

public sealed class AcknowledgeCurrentCoachingAgreementHandler(
    ICoachingAgreementRepository repository,
    IUnitOfWork unitOfWork,
    ICoachingAccessPolicy accessPolicy,
    TimeProvider timeProvider)
    : IRequestHandler<AcknowledgeCurrentCoachingAgreementCommand, CoachingAgreementAcknowledgementResponse>
{
    public async Task<CoachingAgreementAcknowledgementResponse> Handle(
        AcknowledgeCurrentCoachingAgreementCommand command,
        CancellationToken cancellationToken)
    {
        var studentId = RequireCurrentStudent(accessPolicy);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var requested = await repository.GetDocumentAsync(command.AgreementDocumentId, cancellationToken);
        var current = requested is null
            ? null
            : await repository.GetCurrentAsync(requested.Locale, now, cancellationToken);
        if (current is null || current.Id != command.AgreementDocumentId)
        {
            throw new BusinessRuleException(
                "CoachingAgreement.NotCurrent",
                "Yalnızca yürürlükteki koçluk anlaşması kabul edilebilir.");
        }

        var existing = await repository.GetActiveSelfAcknowledgementAsync(
            current.Id,
            studentId,
            cancellationToken);
        if (existing is not null)
            return ToResponse(existing, current.Locale);

        var acknowledgement = CoachingAgreementAcknowledgement.Create(
            current.Id,
            studentId,
            studentId,
            CoachingAgreementPartyRole.Self,
            now);
        await repository.AddAcknowledgementAsync(acknowledgement, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(acknowledgement, current.Locale);
    }

    private static CoachingAgreementAcknowledgementResponse ToResponse(
        CoachingAgreementAcknowledgement acknowledgement,
        string locale) => new(
            acknowledgement.Id,
            acknowledgement.AgreementDocumentId,
            locale,
            acknowledgement.AcknowledgedAt,
            acknowledgement.WithdrawnAt);

    internal static Guid RequireCurrentStudent(ICoachingAccessPolicy accessPolicy)
    {
        if (accessPolicy.CurrentUserId is not { } studentId || !accessPolicy.IsCurrentStudent(studentId))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu işlem yalnızca öğrenci tarafından yapılabilir.");
        return studentId;
    }
}

public sealed record WithdrawCoachingAgreementAcknowledgementCommand(Guid AcknowledgementId)
    : IRequest<CoachingAgreementAcknowledgementResponse>, IBypassesCoachingAgreementRequirement;

public sealed class WithdrawCoachingAgreementAcknowledgementValidator
    : AbstractValidator<WithdrawCoachingAgreementAcknowledgementCommand>
{
    public WithdrawCoachingAgreementAcknowledgementValidator() =>
        RuleFor(command => command.AcknowledgementId).NotEmpty();
}

public sealed class WithdrawCoachingAgreementAcknowledgementHandler(
    ICoachingAgreementRepository repository,
    IUnitOfWork unitOfWork,
    ICoachingAccessPolicy accessPolicy,
    TimeProvider timeProvider)
    : IRequestHandler<WithdrawCoachingAgreementAcknowledgementCommand, CoachingAgreementAcknowledgementResponse>
{
    public async Task<CoachingAgreementAcknowledgementResponse> Handle(
        WithdrawCoachingAgreementAcknowledgementCommand command,
        CancellationToken cancellationToken)
    {
        var studentId = AcknowledgeCurrentCoachingAgreementHandler.RequireCurrentStudent(accessPolicy);
        var acknowledgement = await repository.GetAcknowledgementAsync(
            command.AcknowledgementId,
            cancellationToken)
            ?? throw new NotFoundException("CoachingAgreementAcknowledgement", command.AcknowledgementId);

        if (acknowledgement.SubjectStudentId != studentId
            || acknowledgement.AcknowledgedByUserId != studentId
            || acknowledgement.PartyRole != CoachingAgreementPartyRole.Self)
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Yalnızca kendi kabul kaydınızı geri çekebilirsiniz.");
        }

        var document = await repository.GetDocumentAsync(
            acknowledgement.AgreementDocumentId,
            cancellationToken)
            ?? throw new NotFoundException(
                "CoachingAgreementDocument",
                acknowledgement.AgreementDocumentId);
        acknowledgement.Withdraw(studentId, timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CoachingAgreementAcknowledgementResponse(
            acknowledgement.Id,
            acknowledgement.AgreementDocumentId,
            document.Locale,
            acknowledgement.AcknowledgedAt,
            acknowledgement.WithdrawnAt);
    }
}
