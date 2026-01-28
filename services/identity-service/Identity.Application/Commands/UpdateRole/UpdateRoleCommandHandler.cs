using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.UpdateRole;

public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, Result>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRoleCommandHandler(IRoleRepository roleRepository, IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role == null)
        {
            return Result.Failure(new Error("Role.NotFound", "Role not found."));
        }

        if (role.Name != request.Name)
        {
            var existingRoleWithName = await _roleRepository.GetByNameAsync(request.Name, cancellationToken);
            if (existingRoleWithName != null && existingRoleWithName.Id != role.Id)
            {
                return Result.Failure(new Error("Role.Exists", $"Role name '{request.Name}' is already taken."));
            }
        }

        try 
        {
            role.Update(request.Name, request.Description);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(new Error("Role.SystemRole", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
