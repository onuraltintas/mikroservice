using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Queries.GetMyChildren;

public sealed record ChildSummaryDto(
    Guid UserId,
    string FirstName,
    string LastName,
    string FullName,
    int? GradeLevel,
    Guid? InstitutionId,
    string? InstitutionName,
    string? AvatarUrl);

public sealed record GetMyChildrenQuery : IRequest<Result<IReadOnlyList<ChildSummaryDto>>>;

public sealed class GetMyChildrenQueryHandler(
    IParentRepository parentRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetMyChildrenQuery, Result<IReadOnlyList<ChildSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<ChildSummaryDto>>> Handle(
        GetMyChildrenQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid parentUserId)
        {
            return Result.Failure<IReadOnlyList<ChildSummaryDto>>(
                Error.Unauthorized("Oturum açılmış kullanıcı bulunamadı."));
        }

        if (!currentUserService.Roles.Any(role =>
                string.Equals(role, "Parent", StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<IReadOnlyList<ChildSummaryDto>>(
                Error.Forbidden("Bu kaynak yalnızca veli rolü için kullanılabilir."));
        }

        var children = await parentRepository.GetActiveChildrenByUserIdAsync(
            parentUserId,
            cancellationToken);

        var result = children
            .Select(child => new ChildSummaryDto(
                child.UserId,
                child.FirstName,
                child.LastName,
                child.FullName,
                child.GradeLevel,
                child.InstitutionId,
                child.Institution?.Name,
                child.AvatarUrl))
            .ToList();

        return Result.Success<IReadOnlyList<ChildSummaryDto>>(result);
    }
}
