using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Queries.GetRolePermissions;

public class GetRolePermissionsQueryHandler : IRequestHandler<GetRolePermissionsQuery, Result<RolePermissionsDto>>
{
    private readonly IRoleRepository _roleRepository;

    public GetRolePermissionsQueryHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<Result<RolePermissionsDto>> Handle(GetRolePermissionsQuery request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdWithPermissionsAsync(request.RoleId, cancellationToken);

        if (role == null)
        {
            return Result.Failure<RolePermissionsDto>(new Error("Role.NotFound", "Rol bulunamadı."));
        }

        var dto = new RolePermissionsDto(
            role.Id,
            role.Name,
            role.Permissions.Select(p => p.Permission).ToList()
        );

        return Result<RolePermissionsDto>.Success(dto);
    }
}
