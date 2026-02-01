using EduPlatform.Shared.Kernel.Results;
using MediatR;
using Identity.Application.Interfaces;
using EduPlatform.Shared.Kernel.Primitives;
using MassTransit;
using EduPlatform.Shared.Contracts.Events.Identity;

namespace Identity.Application.Commands.ConfirmEmail;

public record ConfirmEmailCommand(Guid UserId, string? Token = null) : IRequest<Result>;

public class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand, Result>
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

    public async Task<Result> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return Result.Failure(new Error("User.NotFound", "Kullanıcı bulunamadı."));

        if (user.EmailConfirmed)
            return Result.Success();

        if (request.Token != null)
        {
            if (user.EmailVerificationToken != request.Token)
                return Result.Failure(new Error("User.InvalidToken", "Geçersiz veya süresi dolmuş doğrulama kodu."));

            if (user.EmailVerificationTokenExpiresAt < DateTime.UtcNow)
                return Result.Failure(new Error("User.TokenExpired", "Doğrulama kodunun süresi dolmuş. Lütfen yeni bir kod isteyin."));
        }

        // 1. Confirm Email
        user.ConfirmEmail();
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 2. Publish Event for Notification Service (Welcome Email)
        var primaryRole = user.Roles.OrderBy(r => r.Role.Name).FirstOrDefault()?.Role?.Name ?? "User";
        
        await _publishEndpoint.Publish(new UserEmailConfirmedEvent(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            primaryRole
        ), cancellationToken);

        return Result.Success();
    }
}
