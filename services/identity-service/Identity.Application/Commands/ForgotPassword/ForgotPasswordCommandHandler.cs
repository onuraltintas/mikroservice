using EduPlatform.Shared.Contracts.Events.Identity;
using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MassTransit;
using MediatR;

namespace Identity.Application.Commands.ForgotPassword;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;

    public ForgotPasswordCommandHandler(
        IUserRepository userRepository, 
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        // Security Best Practice: Don't reveal if user exists
        if (user == null)
        {
            return Result.Success();
        }

        user.GeneratePasswordResetToken();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish event to send email
        await _publishEndpoint.Publish(new UserForgotPasswordEvent(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PasswordResetToken!), cancellationToken);

        return Result.Success();
    }
}
