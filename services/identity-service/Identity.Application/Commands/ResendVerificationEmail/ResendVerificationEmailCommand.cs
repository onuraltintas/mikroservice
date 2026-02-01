using EduPlatform.Shared.Kernel.Results;
using MediatR;
using Identity.Application.Interfaces;
using EduPlatform.Shared.Kernel.Primitives;
using MassTransit;
using EduPlatform.Shared.Contracts.Events.Identity;

namespace Identity.Application.Commands.ResendVerificationEmail;

public record ResendVerificationEmailCommand(string Email) : IRequest<Result>;

public class ResendVerificationEmailCommandHandler : IRequestHandler<ResendVerificationEmailCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;

    public ResendVerificationEmailCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork, IPublishEndpoint publishEndpoint)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Result> Handle(ResendVerificationEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user == null)
            return Result.Failure(new Error("User.NotFound", "Kullanıcı bulunamadı."));

        if (user.EmailConfirmed)
            return Result.Failure(new Error("User.EmailAlreadyConfirmed", "E-posta adresi zaten doğrulanmış."));

        // Generate new token
        user.GenerateEmailVerificationToken();
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish Event
        await _publishEndpoint.Publish(new UserRegisteredEvent(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.EmailVerificationToken ?? ""
        ), cancellationToken);

        return Result.Success();
    }
}
