using EduPlatform.Shared.Contracts.Events.CourseManagement;
using EduPlatform.Shared.Contracts.Events.Identity;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using FluentValidation;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Application.Registration;
using Identity.Application.Services;
using Identity.Application.Validators;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MassTransit;
using MediatR;

namespace Identity.Application.Commands.RegisterCourseManagement;

public sealed class RegisterCourseManagementCommandValidator : AbstractValidator<RegisterCourseManagementCommand>
{
    public RegisterCourseManagementCommandValidator()
    {
        RuleFor(x => x.Kind)
            .Must(kind => CourseManagementRegistration.RoleFor(kind) is not null)
            .WithMessage("\"Kurumum var\" ya da \"Özel ders veriyorum\" seçeneklerinden birini seçin.");
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta alanı zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.")
            .MaximumLength(255).WithMessage("E-posta adresi en fazla 255 karakter olabilir.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Şifre alanı zorunludur.");
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad alanı zorunludur.")
            .MaximumLength(100).WithMessage("Ad en fazla 100 karakter olabilir.");
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad alanı zorunludur.")
            .MaximumLength(100).WithMessage("Soyad en fazla 100 karakter olabilir.");
        RuleFor(x => x.Phone)
            .Must(phone => CourseManagementRegistration.NormalizePhone(phone) is not null)
            .WithMessage("Telefon numaranızı yazın (örnek: 0532 123 45 67).");
        RuleFor(x => x.WorkspaceName)
            .Must((command, name) => CourseManagementRegistration.WorkspaceName(command.Kind, name, command.FirstName ?? "", command.LastName ?? "") is not null)
            .WithMessage("Kurumunuzun adını yazın (en çok 200 karakter).");
    }
}

/// <summary>
/// Opens a maxkampus.com account (<see cref="RegisterCourseManagementCommand"/>). A new e-mail gets a new account with
/// the password policy and a verification e-mail; an existing eduivme account joins after its own password (and code)
/// is checked, as on the other products. Either way Course Management is asked to open the named workspace.
/// </summary>
public sealed class RegisterCourseManagementCommandHandler(
    IIdentityService identityService,
    IUserRepository users,
    IRoleRepository roles,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publisher,
    IConfigurationService configuration,
    IRegistrationLegalConsentService legalConsent,
    IPasswordHasher passwords,
    IMultiFactorService multiFactor,
    TimeProvider timeProvider,
    ILoginAttemptLimiter? loginAttempts = null)
    : IRequestHandler<RegisterCourseManagementCommand, Result<RegistrationOutcome>>
{
    private const PlatformProduct Product = PlatformProduct.CourseManagement;
    private readonly ILoginAttemptLimiter _loginAttempts = loginAttempts ?? NoLoginAttemptLimiter.Instance;

    public async Task<Result<RegistrationOutcome>> Handle(RegisterCourseManagementCommand request, CancellationToken cancellationToken)
    {
        var roleName = CourseManagementRegistration.RoleFor(request.Kind)!;
        var phone = CourseManagementRegistration.NormalizePhone(request.Phone)!;
        var workspaceName = CourseManagementRegistration.WorkspaceName(request.Kind, request.WorkspaceName, request.FirstName, request.LastName)!;

        var allowRegistration = await configuration.GetConfigurationValueAsync("auth.allowregistration", cancellationToken);
        if (!string.Equals(allowRegistration, "true", StringComparison.OrdinalIgnoreCase))
            return Failure("Identity.RegistrationDisabled", "Yeni kullanıcı kayıtları sistem yöneticisi tarafından geçici olarak durdurulmuştur.");

        var legal = await legalConsent.ValidateAsync(Product, request.LegalAcceptances, cancellationToken);
        if (legal.IsFailure) return Result.Failure<RegistrationOutcome>(legal.Error);

        var existing = await users.GetByEmailAsync(request.Email.Trim(), cancellationToken);
        if (existing is not null)
            return await JoinAsync(existing, request, roleName, phone, workspaceName, legal.Value, cancellationToken);

        // The password policy applies to new accounts only; an existing account keeps its own password.
        if (!RegistrationPasswordRules.IsValid(request.Password))
            return Failure("Validation.Password", RegistrationPasswordRules.Message);

        var created = await identityService.RegisterUserAsync(request.Email, request.Password, request.FirstName, request.LastName, cancellationToken);
        if (created.IsFailure) return Result.Failure<RegistrationOutcome>(created.Error);
        var userId = created.Value;
        var user = await users.GetByIdAsync(userId, cancellationToken);
        var role = await CourseManagementRoles.EnsureAsync(roles, roleName, cancellationToken);
        if (user is null || role is null)
        {
            await identityService.DeleteUserAsync(userId, cancellationToken);
            return Failure("Registration.Failed", "Hesap kaydı tamamlanamadı. Lütfen biraz sonra yeniden deneyin.");
        }

        try
        {
            user.AddRole(new Identity.Domain.Entities.UserRole(userId, role.Id, Product));
            if (user.GrantProductAccess(Product, UserProductAccessSource.SelfRegistration, null, timeProvider.GetUtcNow()))
                users.TrackProductAccessIfNew(user, Product);
            user.SetPhoneNumber(phone);
            user.GenerateEmailVerificationToken();
            legalConsent.TrackAcceptedDocuments(userId, Product, legal.Value, "password");
            legalConsent.TrackSource(userId, Product, request.Utm, "password");
            await PublishWorkspaceAsync(user, request.Kind, workspaceName, cancellationToken);
            await publisher.Publish(new UserRegisteredEvent(userId, user.Email, user.FirstName, user.LastName,
                user.EmailVerificationToken ?? "", roleName) { Product = Product.ToRouteValue() }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(RegistrationOutcome.EmailSent(userId));
        }
        catch (Exception)
        {
            await identityService.DeleteUserAsync(userId, cancellationToken);
            return Failure("Registration.Failed", "Kayıt tamamlanamadı. Lütfen biraz sonra yeniden deneyin.");
        }
    }

    /// <summary>An eduivme account (Koçluk, Hızlı Okuma) joins maxkampüs with its own password and, if it has one, its code.</summary>
    private async Task<Result<RegistrationOutcome>> JoinAsync(User user, RegisterCourseManagementCommand request, string roleName,
        string phone, string workspaceName, IReadOnlyList<PlatformLegalPageDto> documents, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (await _loginAttempts.GetLockoutAsync(email, cancellationToken) is { } remaining)
            return Result.Failure<RegistrationOutcome>(Commands.Login.LoginCommandHandler.TooManyAttempts(remaining));
        if (!passwords.VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            await _loginAttempts.RecordFailureAsync(email, cancellationToken);
            return Failure("Auth.ExistingAccountPasswordRequired",
                "Bu e-postayla bir eduivme hesabı var. maxkampüs'e katılmak için o hesabın şifresini yazın; şifreniz değişmez.");
        }
        await _loginAttempts.ResetAsync(email, cancellationToken);
        if (!user.IsActive)
            return Failure("Auth.ExistingAccountNotEligible", "Mevcut hesabınız aktif olmalıdır.");
        // An access an admin closed, or a maxkampüs role the account already has, is not opened again here.
        if (user.ProductAccesses.Any(access => access.Product == Product) || user.Roles.Any(item => item.Product == Product && item.Role is not { IsDeleted: true }))
            return Failure("Identity.UserExists", "Bu e-postayla maxkampüs hesabınız var. Giriş yapın; şifrenizi hatırlamıyorsanız yenileyin.");

        if (user.MfaEnabled)
        {
            var now = timeProvider.GetUtcNow();
            if (user.IsMfaVerificationLocked(now))
                return Failure("Auth.MfaLocked", "Çok fazla hatalı doğrulama denemesi yapıldı. Daha sonra tekrar deneyin.");
            if (string.IsNullOrWhiteSpace(request.MfaCode))
                return Failure("Auth.ExistingAccountMfaRequired", "Mevcut hesabınızın doğrulama uygulamasındaki altı haneli kodu girin.");
            var step = string.IsNullOrWhiteSpace(user.MfaSecretProtected) ? null : multiFactor.FindMatchingTimeStep(user.MfaSecretProtected, request.MfaCode);
            if (!step.HasValue || user.LastAcceptedMfaTimeStep >= step.Value || !user.TryAcceptMfaTimeStep(step.Value))
            {
                user.RecordFailedMfaAttempt(now);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Failure("Auth.InvalidMfaCode", "Doğrulama kodu geçersiz veya kullanılmış. Yeni kodla tekrar deneyin.");
            }
        }

        var role = await CourseManagementRoles.EnsureAsync(roles, roleName, cancellationToken);
        if (role is null) return Failure("Identity.RoleNotFound", "Katılım rolü şu anda kullanılamıyor.");
        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id, Product));
        if (user.GrantProductAccess(Product, UserProductAccessSource.SelfRegistration, user.Id, timeProvider.GetUtcNow()))
            users.TrackProductAccessIfNew(user, Product);
        if (string.IsNullOrWhiteSpace(user.PhoneNumber)) user.SetPhoneNumber(phone);
        legalConsent.TrackAcceptedDocuments(user.Id, Product, documents, "password");
        await PublishWorkspaceAsync(user, request.Kind, workspaceName, cancellationToken);
        var verificationSent = false;
        if (!user.EmailConfirmed)
        {
            user.GenerateEmailVerificationToken();
            await publisher.Publish(new UserRegisteredEvent(user.Id, user.Email, user.FirstName, user.LastName,
                user.EmailVerificationToken ?? "", roleName) { Product = Product.ToRouteValue() }, cancellationToken);
            verificationSent = true;
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(verificationSent ? RegistrationOutcome.EmailSent(user.Id) : RegistrationOutcome.AlreadyVerified(user.Id));
    }

    private Task PublishWorkspaceAsync(User user, string kind, string workspaceName, CancellationToken cancellationToken) =>
        publisher.Publish(new CourseManagementWorkspaceRequestedV1(user.Id, CourseManagementRegistration.WorkspaceIdFor(user.Id), workspaceName,
            kind, $"{user.FirstName} {user.LastName}".Trim(), timeProvider.GetUtcNow()), cancellationToken);

    private static Result<RegistrationOutcome> Failure(string code, string message) =>
        Result.Failure<RegistrationOutcome>(new Error(code, message));
}

/// <summary>The maxkampüs roles carry no administration permissions; they are made when first needed (as in joining).</summary>
public static class CourseManagementRoles
{
    public static async Task<Role?> EnsureAsync(IRoleRepository roles, string roleName, CancellationToken cancellationToken)
    {
        var role = await roles.GetByNameAsync(roleName, cancellationToken);
        if (role is null)
        {
            role = Role.Create(roleName, "Kurs yönetimi ürün katılımı", isSystemRole: true);
            await roles.AddAsync(role, cancellationToken);
        }
        return role.IsDeleted || role.Name != roleName || role.Permissions.Count != 0 ? null : role;
    }
}
