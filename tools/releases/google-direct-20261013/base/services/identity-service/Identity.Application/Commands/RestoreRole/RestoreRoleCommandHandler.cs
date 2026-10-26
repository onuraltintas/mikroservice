using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.RestoreRole;

public class RestoreRoleCommandHandler : IRequestHandler<RestoreRoleCommand, Result>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RestoreRoleCommandHandler(IRoleRepository roleRepository, IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RestoreRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role == null)
        {
            return Result.Failure(new Error("Role.NotFound", "Rol bulunamadı."));
        }

        role.Restore();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
