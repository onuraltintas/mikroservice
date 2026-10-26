using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.RestorePermission;

public class RestorePermissionCommandHandler : IRequestHandler<RestorePermissionCommand, Result>
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RestorePermissionCommandHandler(IPermissionRepository permissionRepository, IUnitOfWork unitOfWork)
    {
        _permissionRepository = permissionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RestorePermissionCommand request, CancellationToken cancellationToken)
    {
        var permission = await _permissionRepository.GetByIdAsync(request.Id, cancellationToken);
        if (permission == null)
        {
            return Result.Failure(new Error("Permission.NotFound", "İzin bulunamadı."));
        }

        permission.Restore();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
