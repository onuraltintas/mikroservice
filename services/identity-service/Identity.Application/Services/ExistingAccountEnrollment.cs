using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Contracts.Coaching;
using MassTransit;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Services;

public sealed class ExistingAccountEnrollment(
    IUserRepository users, IStudentRepository students, ITeacherRepository teachers,
    IInstitutionRepository institutions, IRoleRepository roles, IPasswordHasher passwords,
    IMultiFactorService multiFactor, IUnitOfWork unitOfWork,
    IRegistrationLegalConsentService legalConsent, TimeProvider timeProvider, IAuthenticationLock authenticationLock, IPublishEndpoint publisher)
{
    public async Task<Result<Guid>> JoinAsync(string email, string password, PlatformProduct target,
        string account, IReadOnlyList<PlatformLegalPageDto> documents, string? mfaCode,
        CancellationToken cancellationToken, Guid? institutionId = null)
    {
        if (target is not (PlatformProduct.Coaching or PlatformProduct.SpeedReading)
            || account is not ("student" or "teacher" or "institution")
            || institutionId.HasValue && (account != "institution" || institutionId == Guid.Empty))
            return Failure("Auth.InvalidProduct", "Geçersiz servis veya hesap türü.");

        var user = await users.GetByEmailAsync(email.Trim(), cancellationToken);
        if (user is null || !passwords.VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt))
            return Failure("Auth.ExistingAccountPasswordRequired", "Mevcut hesabınızın e-posta adresini ve şifresini kullanın.");
        var lockedUserId = user.Id;
        await using var lease = await authenticationLock.AcquireAsync(lockedUserId, cancellationToken);
        unitOfWork.ClearTracking();
        user = await users.GetByEmailAsync(email.Trim(), cancellationToken);
        if (user is null || user.Id != lockedUserId || !passwords.VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt))
            return Failure("Auth.ExistingAccountPasswordRequired", "Mevcut hesabınızın e-posta adresini ve şifresini kullanın. Bu işlem şifrenizi değiştirmez.");
        if (!user.IsActive || !user.EmailConfirmed)
            return Failure("Auth.ExistingAccountNotEligible", "Mevcut hesabınız aktif ve e-posta adresiniz doğrulanmış olmalıdır.");
        if (user.ProductAccesses.Any(a => a.Product == target) || user.Roles.Any(r => r.Product == target))
            return Failure("Identity.UserExists", "Bu serviste kaydınız zaten var. Giriş yapın; erişim kapatılmışsa servis yöneticisiyle görüşün.");

        var source = target == PlatformProduct.Coaching ? PlatformProduct.SpeedReading : PlatformProduct.Coaching;
        bool HasSourceRole(string name) => user.Roles.Any(r => r.Product == source
            && r.Role is not null && !r.Role.IsDeleted && r.Role.Name == name);
        var sourceRole = account == "student" ? "Student" : account == "teacher" ? "Teacher" : "InstitutionAdmin";
        if (!user.HasProductAccess(source)
            || !(HasSourceRole(sourceRole) || account == "institution" && HasSourceRole("InstitutionOwner")))
            return Failure("Auth.SourceRoleRequired", "Diğer serviste bu hesap türüne ait aktif yetkiniz bulunmuyor. Yetki değişikliği için yöneticinizle görüşün.");

        InstitutionAdmin? sourceAdmin = null;
        var institutionChoices = new List<InstitutionEnrollmentChoice>();
        var targetRoleNames = new List<string> { sourceRole };
        if (account == "institution")
        {
            var links = await institutions.GetAdminsForRegistrationAsync(user.Id, cancellationToken);
            if (links.Any(a => a.Product == target))
                return Failure("Identity.UserExists", "Bu servis için mevcut kurum bağlantınız var. Yöneticinizle görüşün.");
            var sourceLinks = new List<InstitutionAdmin>();
            foreach (var link in links.Where(a => a.UserId == user.Id && a.Product == source && a.IsActive))
            {
                var institution = await institutions.GetByIdAsync(link.InstitutionId, cancellationToken);
                if (institution is not { IsActive: true }) continue;
                sourceLinks.Add(link);
                institutionChoices.Add(new(link.InstitutionId, institution.Name, link.Role.ToString()));
            }
            if (sourceLinks.Count == 0)
                return Failure("Auth.ExistingAccountNotEligible", "Katılım için aktif bir kurum bağlantısı gerekir.");
            sourceAdmin = institutionId.HasValue
                ? sourceLinks.SingleOrDefault(link => link.InstitutionId == institutionId)
                : sourceLinks.Count == 1 ? sourceLinks[0] : null;
            if (institutionId.HasValue && sourceAdmin is null)
                return Failure("Auth.InvalidInstitutionSelection", "Seçilen kuruma ait aktif yetkiniz bulunmuyor. Kurum listesini yeniden yükleyin.");
            if (sourceAdmin?.Role == InstitutionAdminRole.Owner)
            {
                if (!HasSourceRole("InstitutionOwner"))
                    return Failure("Auth.SourceRoleRequired", "Kurum sahibi yetkisi doğrulanamadı.");
                targetRoleNames.Add("InstitutionOwner");
            }
        }

        var targetRoles = new List<Role>();
        foreach (var name in targetRoleNames)
        {
            var role = await roles.GetByNameAsync(name, cancellationToken);
            if (role is null || role.IsDeleted)
                return Failure("Identity.RoleNotFound", "Katılım rolü şu anda kullanılamıyor.");
            targetRoles.Add(role);
        }
        long? acceptedMfaStep = null;
        if (user.MfaEnabled)
        {
            var now = timeProvider.GetUtcNow();
            if (user.IsMfaVerificationLocked(now))
                return Failure("Auth.MfaLocked", "Çok fazla hatalı doğrulama denemesi yapıldı. Daha sonra tekrar deneyin.");
            if (string.IsNullOrWhiteSpace(mfaCode))
                return Failure("Auth.ExistingAccountMfaRequired", "Mevcut hesabınızın doğrulama uygulamasındaki altı haneli kodu girin.");
            var step = string.IsNullOrWhiteSpace(user.MfaSecretProtected) ? null
                : multiFactor.FindMatchingTimeStep(user.MfaSecretProtected, mfaCode);
            if (!step.HasValue || user.LastAcceptedMfaTimeStep >= step.Value)
            {
                user.RecordFailedMfaAttempt(now);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Failure("Auth.InvalidMfaCode", "Doğrulama kodu geçersiz veya kullanılmış. Yeni kodla tekrar deneyin.");
            }
            acceptedMfaStep = step;
        }

        if (account == "institution" && sourceAdmin is null)
            return Result.Failure<Guid>(new InstitutionSelectionRequiredError(institutionChoices
                .OrderBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase).ThenBy(choice => choice.Id).ToArray()));
        if (acceptedMfaStep.HasValue && !user.TryAcceptMfaTimeStep(acceptedMfaStep.Value))
            return Failure("Auth.InvalidMfaCode", "Doğrulama kodu kullanılmış. Yeni kodla tekrar deneyin.");

        // Product profiles and memberships are provisioned by their owning service.
        if (sourceAdmin is not null)
            await institutions.AddAdminAsync(InstitutionAdmin.Create(user.Id, sourceAdmin.InstitutionId, sourceAdmin.Role, target), cancellationToken);
        foreach (var role in targetRoles)
            user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id, target));
        if (user.GrantProductAccess(target, UserProductAccessSource.SelfRegistration, user.Id, timeProvider.GetUtcNow()))
            users.TrackProductAccessIfNew(user, target);
        legalConsent.TrackAcceptedDocuments(user.Id, target, documents, "password");
        if (target == PlatformProduct.Coaching && account is "student" or "teacher")
            await publisher.Publish(new CoachingProfileProvisioningRequested(user.Id, sourceRole, DateTime.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(user.Id);
    }

    private async Task<bool> IsActiveInstitution(Guid id, CancellationToken ct) =>
        (await institutions.GetByIdAsync(id, ct)) is { IsActive: true };
    private static Result<Guid> Failure(string code, string description) => Result.Failure<Guid>(new Error(code, description));
}
