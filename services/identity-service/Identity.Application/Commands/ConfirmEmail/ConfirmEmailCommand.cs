using EduPlatform.Shared.Kernel.Results;
using MediatR;
using Identity.Application.Interfaces;
using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Application.Commands.ConfirmEmail;

public record ConfirmEmailCommand(Guid UserId) : IRequest<Result>;

public class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmEmailCommandHandler(IUserRepository userRepository, IIdentityService identityService, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _identityService = identityService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return Result.Failure(new Error("User.NotFound", "Kullanıcı bulunamadı."));

        if (user.EmailConfirmed)
            return Result.Success();

        // 1. Database Update
        user.ConfirmEmail();
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
