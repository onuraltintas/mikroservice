using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Queries.GetRoles;

public record GetRolesQuery() : IRequest<Result<List<RoleDto>>>;
