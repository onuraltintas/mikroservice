using EduPlatform.Shared.Contracts.Events.Identity;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using FluentValidation;
using Identity.Application.Interfaces;
using Identity.Application.Registration;
using Identity.Application.Validators;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MassTransit;
using MediatR;

namespace Identity.Application.Commands.ChangeEmail;

/// <summary>
/// E5 (#22, live test 06.11.2026): the signed-in account asks to change its e-mail address. The current password is
/// required; a one-time link (24 hours) goes to the new address and nothing changes until it is confirmed.
/// </summary>
public record RequestEmailChangeCommand(string CurrentPassword, string NewEmail, PlatformProduct? Product = null)
    : IRequest<Result<EmailChangeRequested>>;

public record EmailChangeRequested(string PendingEmail, DateTime ExpiresAt);

/// <summary>Withdraws a pending change; its link stops working.</summary>
public record CancelEmailChangeCommand : IRequest<Result>;

/// <summary>The link in the e-mail sent to the new address (anonymous: it may be opened on another device).</summary>
public record ConfirmEmailChangeCommand(Guid UserId, string Token) : IRequest<Result>;

internal static class EmailChangeErrors
{
    public static readonly Error PasswordNotSet = new("Account.PasswordNotSet",
        "Hesabında şifre yok: Google ile giriş yapıyorsun. Önce Hesabım sayfasından şifre belirleme bağlantısı iste.");
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
        if (!user.HasPassword) return Result.Failure<EmailChangeRequested>(EmailChangeErrors.PasswordNotSet);
        if (!passwordHasher.VerifyPasswordHash(request.CurrentPassword ?? string.Empty, user.PasswordHash, user.PasswordSalt))
            return Result.Failure<EmailChangeRequested>(EmailChangeErrors.InvalidCredentials);

        var newEmail = User.NormalizeEmail(request.NewEmail ?? string.Empty);
        if (newEmail == User.NormalizeEmail(user.Email)) return Result.Failure<EmailChangeRequested>(EmailChangeErrors.SameEmail);
        var owner = await users.GetByEmailAsync(newEmail, cancellationToken);
        if (owner is not null && owner.Id != user.Id) return Result.Failure<EmailChangeRequested>(EmailChangeErrors.EmailInUse);

        var now = DateTime.UtcNow;
        if (user.EmailChangeRequestedRecently(now)) return Result.Failure<EmailChangeRequested>(EmailChangeErrors.TooSoon);

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
        await publishEndpoint.Publish(new UserEmailChangedEvent(
            user.Id, previous, user.Email, user.FirstName, user.LastName,
            EmailProduct.Role(user, EmailChangeErrors.Product, "User")) { Product = EmailChangeErrors.Product }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
