using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.RevokeEmailConfirmation;

public sealed record RevokeEmailConfirmationCommand(Guid UserId) : IRequest<Result>;

public sealed class RevokeEmailConfirmationCommandHandler : IRequestHandler<RevokeEmailConfirmationCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeEmailConfirmationCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        RevokeEmailConfirmationCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(new Error("User.NotFound", "Kullanıcı bulunamadı."));
        }

        user.RevokeEmailConfirmation();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
