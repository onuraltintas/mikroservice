using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MediatR;
using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Application.Commands.DeleteUser;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUserCommandHandler(IUserRepository userRepository, IIdentityService identityService, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _identityService = identityService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return Result.Failure(new Error("User.NotFound", "Kullanıcı bulunamadı."));

        if (request.Permanent)
        {
            // Kalıcı Silme
            _userRepository.Delete(user);
        }
        else
        {
            // Pasif Yapma (Soft Delete)
            user.Deactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
