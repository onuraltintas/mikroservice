using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Contracts.Events.Identity;
using Identity.Application.Registration;
using MassTransit;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Services;

public sealed class ExistingAccountEnrollment(
    IUserRepository users,
    IInstitutionRepository institutions, IRoleRepository roles, IPasswordHasher passwords,
    IMultiFactorService multiFactor, IUnitOfWork unitOfWork,
    IRegistrationLegalConsentService legalConsent, TimeProvider timeProvider, IAuthenticationLock authenticationLock, IPublishEndpoint publisher,
    IParentRepository parents, ILoginAttemptLimiter? loginAttempts = null)
{
    private readonly ILoginAttemptLimiter _loginAttempts = loginAttempts ?? NoLoginAttemptLimiter.Instance;

    public async Task<Result<RegistrationOutcome>> JoinAsync(string email, string password, PlatformProduct target,
        string account, IReadOnlyList<PlatformLegalPageDto> documents, string? mfaCode,
        CancellationToken cancellationToken, Guid? institutionId = null)
    {
        if (target is not (PlatformProduct.Coaching or PlatformProduct.SpeedReading)
            || account is not ("student" or "teacher" or "institution" or "parent")
            || account == "parent" && target != PlatformProduct.Coaching
            || institutionId.HasValue && (account != "institution" || institutionId == Guid.Empty))
            return Failure("Auth.InvalidProduct", "Geçersiz servis veya hesap türü.");

        // Joining verifies the account password, so it shares the sign-in lockout.
        if (await _loginAttempts.GetLockoutAsync(email, cancellationToken) is { } remaining)
            return Result.Failure<RegistrationOutcome>(Commands.Login.LoginCommandHandler.TooManyAttempts(remaining));
        var user = await users.GetByEmailAsync(email.Trim(), cancellationToken);
        if (user is null || !passwords.VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt))
        {
            await _loginAttempts.RecordFailureAsync(email, cancellationToken);
            return Failure("Auth.ExistingAccountPasswordRequired", "Mevcut hesabınızın e-posta adresini ve şifresini kullanın.");
        }
        await _loginAttempts.ResetAsync(email, cancellationToken);
        var lockedUserId = user.Id;
        await using var lease = await authenticationLock.AcquireAsync(lockedUserId, cancellationToken);
        unitOfWork.ClearTracking();
        user = await users.GetByEmailAsync(email.Trim(), cancellationToken);
        if (user is null || user.Id != lockedUserId || !passwords.VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt))
            return Failure("Auth.ExistingAccountPasswordRequired", "Mevcut hesabınızın e-posta adresini ve şifresini kullanın. Bu işlem şifrenizi değiştirmez.");
        // Paket A (05.11.2026): an unverified address no longer blocks joining. The existing password
        // is still required (above), sign-in still requires a verified address (LoginCommandHandler),
        // and the join sends the verification e-mail below, so access is usable only after verification.
        if (!user.IsActive)
            return Failure("Auth.ExistingAccountNotEligible", "Mevcut hesabınız aktif olmalıdır.");

        var sourceRole = account switch
        {
            "student" => "Student",
            "teacher" => "Teacher",
            "institution" => "InstitutionAdmin",
            _ => "Parent"
        };
        // Review #8: a retry (e.g. after a lost response) of a join that already happened is idempotent.
        // Nothing is granted; after MFA (below) an unverified address gets a fresh link, a verified one "sign in".
        var alreadyJoined = user.HasProductAccess(target)
            && await HasTargetMembershipAsync(user, target, account, sourceRole, cancellationToken);
        // F4: an account whose last role in the target was removed (its access row left active, as the admin
        // panel keeps access and roles separate) may join again. A closed access or any role in the target still blocks.
        if (!alreadyJoined && (user.ProductAccesses.Any(a => a.Product == target && !a.IsActive)
                || user.Roles.Any(r => r.Product == target)))
            return Failure("Identity.UserExists", "Bu serviste kaydınız zaten var. Giriş yapın; erişim kapatılmışsa servis yöneticisiyle görüşün.");
        InstitutionAdmin? sourceAdmin = null;
        var institutionChoices = new List<InstitutionEnrollmentChoice>();
        var targetRoles = new List<Role>();
        if (!alreadyJoined)
        {
            var source = target == PlatformProduct.Coaching ? PlatformProduct.SpeedReading : PlatformProduct.Coaching;
            bool HasSourceRole(string name) => user.Roles.Any(r => r.Product == source
                && r.Role is not null && !r.Role.IsDeleted && r.Role.Name == name);
            // Review #1: a Coaching parent needs no parent role elsewhere; the verified password (and MFA) is the proof.
            if (account != "parent" && (!user.HasProductAccess(source)
                || !(HasSourceRole(sourceRole) || account == "institution" && HasSourceRole("InstitutionOwner"))))
                return Failure("Auth.SourceRoleRequired", "Diğer serviste bu hesap türüne ait aktif yetkiniz bulunmuyor. Yetki değişikliği için yöneticinizle görüşün.");

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

            foreach (var name in targetRoleNames)
            {
                var role = await roles.GetByNameAsync(name, cancellationToken);
                if (role is null || role.IsDeleted)
                    return Failure("Identity.RoleNotFound", "Katılım rolü şu anda kullanılamıyor.");
                targetRoles.Add(role);
            }
        }

        // Second review L2: MFA comes before anything is sent or rotated, also for a retry.
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

        // Second review M2a: only an account that is active in the other product (speed reading) may make
        // itself a Coaching parent; anyone else (revoked, product-less, staff-only) gets the old answer.
        // Third review N1: only after the password and MFA, so it reveals nothing to someone without them.
        if (account == "parent" && !IsEligibleParentSource(user))
            return Failure("Identity.UserExists", "Bu e-posta adresiyle zaten kayıtlı bir hesap var. Lütfen giriş yapın veya şifrenizi sıfırlayın.");
        if (alreadyJoined)
        {
            if (acceptedMfaStep.HasValue && !user.TryAcceptMfaTimeStep(acceptedMfaStep.Value))
                return Failure("Auth.InvalidMfaCode", "Doğrulama kodu kullanılmış. Yeni kodla tekrar deneyin.");
            return await CompleteAsync(user, target, sourceRole, newProductAccess: false, cancellationToken);
        }
        if (account == "institution" && sourceAdmin is null)
            return Result.Failure<RegistrationOutcome>(new InstitutionSelectionRequiredError(institutionChoices
                .OrderBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase).ThenBy(choice => choice.Id).ToArray()));
        if (acceptedMfaStep.HasValue && !user.TryAcceptMfaTimeStep(acceptedMfaStep.Value))
            return Failure("Auth.InvalidMfaCode", "Doğrulama kodu kullanılmış. Yeni kodla tekrar deneyin.");

        // Product profiles and memberships are provisioned by their owning service.
        if (sourceAdmin is not null)
            await institutions.AddAdminAsync(
                InstitutionAdmin.CreateByExistingAccountJoin(user.Id, sourceAdmin.InstitutionId, sourceAdmin.Role, target), cancellationToken);
        foreach (var role in targetRoles)
            user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id, target));
        if (user.GrantProductAccess(target, UserProductAccessSource.SelfRegistration, user.Id, timeProvider.GetUtcNow()))
            users.TrackProductAccessIfNew(user, target);
        legalConsent.TrackAcceptedDocuments(user.Id, target, documents, "password");
        if (target == PlatformProduct.Coaching && account is "student" or "teacher")
            await publisher.Publish(new CoachingProfileProvisioningRequested(user.Id, sourceRole, DateTime.UtcNow), cancellationToken);
        if (account == "parent" && await parents.GetByUserIdAsync(user.Id, cancellationToken) is null)
            await parents.AddAsync(ParentProfile.Create(user.Id, user.FirstName, user.LastName), cancellationToken);
        return await CompleteAsync(user, target, sourceRole, newProductAccess: true, cancellationToken);
    }

    private static bool IsEligibleParentSource(User user) =>
        user.ProductAccesses.Any(access => access.IsActive && access.Product == PlatformProduct.SpeedReading);

    /// <summary>
    /// The account already has <paramref name="target"/> with the membership this registration asks for.
    /// Roles are compared by id because a freshly added membership has no loaded <see cref="Role"/>.
    /// </summary>
    private async Task<bool> HasTargetMembershipAsync(User user, PlatformProduct target, string account, string roleName,
        CancellationToken cancellationToken)
    {
        var role = await roles.GetByNameAsync(roleName, cancellationToken);
        if (role is null || role.IsDeleted
            || !user.Roles.Any(r => r.Product == target && (r.RoleId == role.Id || r.Role?.Name == roleName)))
            return false;
        if (account != "institution")
            return true;
        var links = await institutions.GetAdminsForRegistrationAsync(user.Id, cancellationToken);
        return links.Any(link => link.UserId == user.Id && link.Product == target && link.IsActive);
    }

    /// <summary>
    /// Paket A: an unverified address gets a fresh 24-hour link (the previous one stops working), named for
    /// the joined product; a verified address needs no e-mail. Repeats are limited by the registration
    /// rate limit at the gateway and the shared sign-in lockout above.
    /// </summary>
    private async Task<Result<RegistrationOutcome>> CompleteAsync(User user, PlatformProduct target, string roleName,
        bool newProductAccess, CancellationToken cancellationToken)
    {
        var verificationEmailSent = false;
        if (!user.EmailConfirmed)
        {
            user.GenerateEmailVerificationToken();
            await publisher.Publish(new UserRegisteredEvent(user.Id, user.Email, user.FirstName, user.LastName,
                user.EmailVerificationToken ?? "", roleName) { Product = target.ToRouteValue() }, cancellationToken);
            verificationEmailSent = true;
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(verificationEmailSent
            ? RegistrationOutcome.EmailSent(user.Id, newProductAccess)
            : RegistrationOutcome.AlreadyVerified(user.Id, newProductAccess));
    }

    private static Result<RegistrationOutcome> Failure(string code, string description) =>
        Result.Failure<RegistrationOutcome>(new Error(code, description));
}
