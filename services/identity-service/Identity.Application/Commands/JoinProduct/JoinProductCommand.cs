using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Application.Services;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.JoinProduct;

public sealed record JoinProductCommand(string Email, string Password, string Account,
    string? MfaCode = null, IReadOnlyList<LegalPageAcceptance>? LegalAcceptances = null,
    PlatformProduct? Product = null, Guid? InstitutionId = null) : IRequest<Result<Guid>>;

public sealed class JoinProductCommandHandler(ExistingAccountEnrollment enrollment,
    IConfigurationService configuration, IRegistrationLegalConsentService legalConsent)
    : IRequestHandler<JoinProductCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(JoinProductCommand request, CancellationToken cancellationToken)
    {
        if (request.Product is not (PlatformProduct.Coaching or PlatformProduct.SpeedReading)
            || request.Account is not ("student" or "teacher" or "institution")
            || string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254
            || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 1024
            || request.MfaCode is { Length: > 6 }
            || request.InstitutionId.HasValue && (request.Account != "institution" || request.InstitutionId == Guid.Empty))
            return Result.Failure<Guid>(new Error("Auth.InvalidEnrollment", "E-posta, mevcut şifre ve hesap türünü kontrol edin."));
        var allowed = await configuration.GetConfigurationValueAsync("auth.allowregistration", cancellationToken);
        if (!string.Equals(allowed, "true", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<Guid>(new Error("Identity.RegistrationDisabled", "Yeni servis katılımı geçici olarak kapalıdır."));
        var documents = await legalConsent.ValidateAsync(request.Product.Value, request.LegalAcceptances, cancellationToken);
        if (documents.IsFailure) return Result.Failure<Guid>(documents.Error);
        return await enrollment.JoinAsync(request.Email, request.Password, request.Product.Value,
            request.Account, documents.Value, request.MfaCode, cancellationToken, request.InstitutionId);
    }
}
