using EduPlatform.Shared.Contracts.Events.Identity;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using FluentValidation;
using Identity.Application.Exceptions;
using Identity.Application.Interfaces;
using Identity.Application.Registration;
using Identity.Application.Validators;
using Identity.Domain.Entities;
using MassTransit;
using MediatR;

namespace Identity.Application.Commands.ChangeEmail;

/// <summary>
/// E5 (#22, live test 06.11.2026): the signed-in account asks to change its e-mail address. The current password is
/// required; a one-time link (24 hours) goes to the new address and nothing changes until it is confirmed.
/// </summary>
public record RequestEmailChangeCommand(string CurrentPassword, string NewEmail) : IRequest<Result<EmailChangeRequested>>;

public record EmailChangeRequested(string PendingEmail, DateTime ExpiresAt);

/// <summary>Withdraws a pending change; its link stops working.</summary>
public record CancelEmailChangeCommand : IRequest<Result>;

/// <summary>The link in the e-mail sent to the new address (anonymous: it may be opened on another device).</summary>
public record ConfirmEmailChangeCommand(Guid UserId, string Token) : IRequest<Result>;

/// <summary>
/// E5 review M1 (b): what the confirmation page shows before the button — only the masked current address of the
/// account the link belongs to. Nothing changes.
/// </summary>
public record GetEmailChangePreviewQuery(Guid UserId, string Token) : IRequest<Result<EmailChangePreview>>;

public record EmailChangePreview(string CurrentEmailMasked);

internal static class EmailChangeErrors
{
    // Review L9: neutral — administrator-provisioned accounts have no password of their own either.
    public static readonly Error PasswordNotSet = new("Account.PasswordNotSet",
        "Hesabın için henüz bir şifre belirlenmemiş. Önce Hesabım sayfasından şifre belirleme bağlantısı iste.");
    public static readonly Error InvalidCredentials = new("Auth.InvalidCredentials", "Mevcut şifre hatalı.");
    public static readonly Error SameEmail = new("Account.SameEmail", "Yeni e-posta adresin şu anki adresinle aynı.");
    public static readonly Error EmailInUse = new("Account.EmailInUse", "Bu e-posta adresi başka bir hesapta kullanılıyor.");
    public static readonly Error TooSoon = new("Account.EmailChangeTooSoon",
        "Az önce bir doğrulama bağlantısı gönderdik. Yenisini istemek için bir dakika bekle.");
    public static readonly Error InvalidLink = new("EmailChange.InvalidLink",
        "Bağlantı geçersiz ya da süresi dolmuş. Hesabım sayfasından yeni bir bağlantı iste.");
    public static readonly Error Unauthorized = new("Auth.Unauthorized", "Oturum açılmamış.");
    public static readonly Error NotFound = new("User.NotFound", "Kullanıcı bulunamadı.");

    // The e-mail and its link are the Coaching site's (a management account gets the admin panel through its role):
    // the confirmation page exists only in the admin-panel application.
    public const string Product = "coaching";
}

public sealed class RequestEmailChangeCommandValidator : AbstractValidator<RequestEmailChangeCommand>
{
    public RequestEmailChangeCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Mevcut şifreni yaz.")
            .MaximumLength(RegistrationPasswordRules.MaximumLength).WithMessage("Mevcut şifre en fazla 128 karakter olabilir.");

        RuleFor(x => x.NewEmail)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Yeni e-posta adresini yaz.")
            .MaximumLength(255).WithMessage("E-posta adresi en fazla 255 karakter olabilir.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi yaz.");
    }
}

public sealed class RequestEmailChangeCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    ICurrentUserService currentUser) : IRequestHandler<RequestEmailChangeCommand, Result<EmailChangeRequested>>
{
    public async Task<Result<EmailChangeRequested>> Handle(RequestEmailChangeCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Result.Failure<EmailChangeRequested>(EmailChangeErrors.Unauthorized);
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) return Result.Failure<EmailChangeRequested>(EmailChangeErrors.NotFound);
        var password = CurrentPassword.Check(user, request.CurrentPassword, passwordHasher);
        if (password is not null) return Result.Failure<EmailChangeRequested>(password);

        // Review L3: the minute limit comes before the address checks, so the form is a slow address probe at best.
        var now = DateTime.UtcNow;
        if (user.EmailChangeRequestedRecently(now)) return Result.Failure<EmailChangeRequested>(EmailChangeErrors.TooSoon);

        var newEmail = User.NormalizeEmail(request.NewEmail ?? string.Empty);
        if (newEmail == User.NormalizeEmail(user.Email)) return Result.Failure<EmailChangeRequested>(EmailChangeErrors.SameEmail);
        var owner = await users.GetByEmailAsync(newEmail, cancellationToken);
        if (owner is not null && owner.Id != user.Id) return Result.Failure<EmailChangeRequested>(EmailChangeErrors.EmailInUse);

        var token = user.RequestEmailChange(newEmail, now);
        await publishEndpoint.Publish(new UserEmailChangeRequestedEvent(
            user.Id, newEmail, user.FirstName, user.LastName, token,
            EmailProduct.Role(user, EmailChangeErrors.Product, "User")) { Product = EmailChangeErrors.Product }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new EmailChangeRequested(newEmail, user.PendingEmailExpiresAt!.Value));
    }
}

public sealed class CancelEmailChangeCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<CancelEmailChangeCommand, Result>
{
    public async Task<Result> Handle(CancelEmailChangeCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Result.Failure(EmailChangeErrors.Unauthorized);
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return Result.Failure(EmailChangeErrors.NotFound);
        if (user.PendingEmail is null) return Result.Success();
        user.CancelEmailChange();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ConfirmEmailChangeCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint) : IRequestHandler<ConfirmEmailChangeCommand, Result>
{
    public async Task<Result> Handle(ConfirmEmailChangeCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null || !user.IsActive || !user.EmailChangeTokenMatches(request.Token))
            return Result.Failure(EmailChangeErrors.InvalidLink);

        // The address may have been registered by someone else after the request.
        var owner = await users.GetByEmailAsync(user.PendingEmail!, cancellationToken);
        if (owner is not null && owner.Id != user.Id) return Result.Failure(EmailChangeErrors.EmailInUse);

        var previous = user.ConfirmEmailChange();
        // Review M1: whoever held a session or the Google sign-in of the old address loses it. Only an account with a
        // password can ask for a change, so it can still sign in (with the new address and its password).
        await users.RevokeActiveRefreshTokensAsync(user.Id, "e-mail address changed", cancellationToken);
        await users.RemoveExternalLoginsAsync(user.Id, cancellationToken);
        // Published before the save: with the bus outbox the notice is stored in the same transaction.
        await publishEndpoint.Publish(new UserEmailChangedEvent(
            user.Id, previous, user.Email, user.FirstName, user.LastName,
            EmailProduct.Role(user, EmailChangeErrors.Product, "User")) { Product = EmailChangeErrors.Product }, cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (users.IsDuplicateEmailViolation(exception))
        {
            // Review L1: registered by someone else between the check above and the save.
            return Result.Failure(EmailChangeErrors.EmailInUse);
        }
        catch (Exception exception) when (exception is IdentityConcurrencyConflictException
            or EduPlatform.Shared.Kernel.Exceptions.ConcurrencyException)
        {
            // Review L2: the same link was confirmed at the same moment; the other confirmation won.
            return Result.Failure(EmailChangeErrors.InvalidLink);
        }

        return Result.Success();
    }
}

public sealed class GetEmailChangePreviewQueryHandler(IUserRepository users) : IRequestHandler<GetEmailChangePreviewQuery, Result<EmailChangePreview>>
{
    public async Task<Result<EmailChangePreview>> Handle(GetEmailChangePreviewQuery request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        return user is null || !user.IsActive || !user.EmailChangeTokenMatches(request.Token)
            ? Result.Failure<EmailChangePreview>(EmailChangeErrors.InvalidLink)
            : Result.Success(new EmailChangePreview(User.MaskEmail(user.Email)));
    }
}

/// <summary>
/// The current-password check of Hesabım (e-mail change, password change). An account without a password, or whose
/// stored password was generated and is not the owner's (review M2), is told to set one; a stored password the owner
/// does enter counts as theirs from then on.
/// </summary>
public static class CurrentPassword
{
    public static Error? Check(User user, string? presented, IPasswordHasher passwordHasher)
    {
        if (user.PasswordHash.Length == 0 || user.PasswordSalt.Length == 0) return EmailChangeErrors.PasswordNotSet;
        if (passwordHasher.VerifyPasswordHash(presented ?? string.Empty, user.PasswordHash, user.PasswordSalt))
        {
            user.MarkPasswordKnownToOwner();
            return null;
        }

        return user.PasswordKnownToOwner ? EmailChangeErrors.InvalidCredentials : EmailChangeErrors.PasswordNotSet;
    }
}
