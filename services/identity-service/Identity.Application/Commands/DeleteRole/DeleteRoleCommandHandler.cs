using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.DeleteRole;

public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, Result>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteRoleCommandHandler(IRoleRepository roleRepository, IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role == null)
        {
            return Result.Failure(new Error("Role.NotFound", "Role not found."));
        }

        if (role.IsSystemRole)
        {
             return Result.Failure(new Error("Role.System", "System roles cannot be deleted."));
        }

        if (request.Permanent)
        {
             _roleRepository.Delete(role);
        }
        else
        {
             try 
             {
                 role.MarkAsDeleted();
             }
             catch(InvalidOperationException ex)
             {
                 return Result.Failure(new Error("Role.System", ex.Message));
             }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
