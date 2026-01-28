using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Queries.GetPermissions;

public record GetPermissionsQuery() : IRequest<Result<List<PermissionDto>>>;
