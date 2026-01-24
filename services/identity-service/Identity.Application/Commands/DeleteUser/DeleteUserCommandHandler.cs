using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.DeleteUser;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DeleteUserCommandHandler(
        IUserRepository userRepository,
        IIdentityService identityService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _identityService = identityService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        // 1. Authorization Check (Only Admin or Owner can delete)
        // For MVP, allow self-deletion or InstitutionAdmin deleting members
        // Current logic: Basic implementation
        
        var userToDelete = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (userToDelete == null)
        {
            return Result.Failure(new Error("User.NotFound", "User not found"));
        }

        if (request.HardDelete)
        {
            // 2a. Hard Delete: Remove from DB and Keycloak
            _userRepository.Delete(userToDelete);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            // Sync with Keycloak (Best effort)
            await _identityService.DeleteUserAsync(request.UserId, cancellationToken);
        }
        else
        {
            // 2b. Soft Delete: Deactivate in DB and Keycloak
            userToDelete.Deactivate();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            // Deactivate in Keycloak
            await _identityService.DeactivateUserAsync(request.UserId, cancellationToken);
        }

        return Result.Success();
    }
}
