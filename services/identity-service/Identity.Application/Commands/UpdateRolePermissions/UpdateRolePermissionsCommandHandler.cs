using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;

namespace Identity.Application.Commands.UpdateRolePermissions;

public class UpdateRolePermissionsCommandHandler : IRequestHandler<UpdateRolePermissionsCommand, Result>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRolePermissionsCommandHandler(IRoleRepository roleRepository, IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdWithPermissionsAsync(request.RoleId, cancellationToken);

        if (role == null)
        {
            return Result.Failure(new Error("Role.NotFound", "Rol bulunamadı."));
        }

        // Get current permissions
        var currentPermissions = role.Permissions.Select(p => p.Permission).ToHashSet();
        var newPermissions = request.Permissions.ToHashSet();

        // Find permissions to remove
        var toRemove = role.Permissions.Where(p => !newPermissions.Contains(p.Permission)).ToList();
        
        // Find permissions to add
        var toAdd = newPermissions.Except(currentPermissions).ToList();

        // Remove old permissions
        foreach (var perm in toRemove)
        {
            _roleRepository.RemoveRolePermission(perm);
        }

        // Add new permissions
        foreach (var permKey in toAdd)
        {
            _roleRepository.AddRolePermission(new RolePermission(role.Id, permKey));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
