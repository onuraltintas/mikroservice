using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Commands.DeletePermission;

public class DeletePermissionCommandHandler : IRequestHandler<DeletePermissionCommand, Result>
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeletePermissionCommandHandler(IPermissionRepository permissionRepository, IUnitOfWork unitOfWork)
    {
        _permissionRepository = permissionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeletePermissionCommand request, CancellationToken cancellationToken)
    {
        var permission = await _permissionRepository.GetByIdAsync(request.Id, cancellationToken);
        if (permission == null)
        {
            return Result.Failure(new Error("Permission.NotFound", "İzin bulunamadı."));
        }

        if (request.Permanent)
        {
            if (permission.IsSystem)
            {
                return Result.Failure(new Error("Permission.SystemDelete", "Sistem izinleri silinemez."));
            }
             await _permissionRepository.DeleteAsync(permission, cancellationToken);
        }
        else
        {
            try
            {
                permission.MarkAsDeleted();
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure(new Error("Permission.SystemDelete", ex.Message));
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
