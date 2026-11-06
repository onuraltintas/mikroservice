using EduPlatform.Shared.Kernel.Results;
using MediatR;
using Identity.Application.Interfaces;
using EduPlatform.Shared.Kernel.Primitives;
using MassTransit;
using EduPlatform.Shared.Contracts.Events.Identity;

namespace Identity.Application.Commands.ConfirmEmail;

/// <param name="Product">Third review N2: the site confirming ("coaching" / "speed-reading"), so the welcome
/// e-mail is that product's; see <see cref="Identity.Application.Registration.EmailProduct"/>.</param>
public record ConfirmEmailCommand(Guid UserId, string? Token = null, string? Product = null) : IRequest<Result<ConfirmEmailOutcome>>;

/// <summary>
/// Paket F #10: whether this request verified the address or it already was verified (an old link opened again), so
/// the page can say "zaten doğrulanmış" and offer sign-in. Failures keep their codes: <c>User.InvalidToken</c>,
/// <c>User.TokenExpired</c>, <c>User.NotFound</c>.
/// </summary>
public enum ConfirmEmailOutcome
{
    Confirmed,
    AlreadyConfirmed
}

public static class ConfirmEmailErrors
{
    public const string InvalidTokenCode = "User.InvalidToken";
    public const string TokenExpiredCode = "User.TokenExpired";
    public static readonly Error InvalidToken = new(InvalidTokenCode, "Geçersiz veya süresi dolmuş doğrulama kodu.");
    public static readonly Error TokenExpired = new(TokenExpiredCode, "Doğrulama kodunun süresi dolmuş. Lütfen yeni bir kod isteyin.");
}

public class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand, Result<ConfirmEmailOutcome>>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;

    public ConfirmEmailCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork, IPublishEndpoint publishEndpoint)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Result<ConfirmEmailOutcome>> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return Result.Failure<ConfirmEmailOutcome>(new Error("User.NotFound", "Kullanıcı bulunamadı."));

        if (user.EmailConfirmed)
            return Result.Success(ConfirmEmailOutcome.AlreadyConfirmed);

        if (request.Token != null)
        {
            if (user.EmailVerificationToken != request.Token)
                return Result.Failure<ConfirmEmailOutcome>(ConfirmEmailErrors.InvalidToken);

            if (user.EmailVerificationTokenExpiresAt < DateTime.UtcNow)
                return Result.Failure<ConfirmEmailOutcome>(ConfirmEmailErrors.TokenExpired);
        }

        // 1. Confirm Email
        user.ConfirmEmail();

        // 2. Publish Event for Notification Service (Welcome Email)
        var product = Identity.Application.Registration.EmailProduct.Resolve(user, request.Product);
        var primaryRole = Identity.Application.Registration.EmailProduct.Role(user, product, "User");
        
        await _publishEndpoint.Publish(new UserEmailConfirmedEvent(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            primaryRole
        ) { Product = product }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ConfirmEmailOutcome.Confirmed);
    }
}
