using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;

namespace Identity.Application.Commands.CreatePermission;

public class CreatePermissionCommandHandler : IRequestHandler<CreatePermissionCommand, Result<Guid>>
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePermissionCommandHandler(IPermissionRepository permissionRepository, IUnitOfWork unitOfWork)
    {
        _permissionRepository = permissionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreatePermissionCommand request, CancellationToken cancellationToken)
    {
        if (await _permissionRepository.ExistsAsync(request.Key, cancellationToken))
        {
            return Result.Failure<Guid>(new Error("Permission.Exists", $"Permission with key '{request.Key}' already exists."));
        }

        var permission = Permission.Create(request.Key, request.Description, request.Group);

        await _permissionRepository.AddAsync(permission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(permission.Id);
    }
}
