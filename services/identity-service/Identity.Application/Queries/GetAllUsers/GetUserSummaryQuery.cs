using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Authorization;
using Identity.Application.Interfaces;
using MediatR;

namespace Identity.Application.Queries.GetAllUsers;

public sealed record UserSummaryDto(int TotalUsers, int ActiveUsers, int InactiveUsers);

public sealed record GetUserSummaryQuery : IRequest<Result<UserSummaryDto>>;

public sealed class GetUserSummaryQueryHandler
    : IRequestHandler<GetUserSummaryQuery, Result<UserSummaryDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IInstitutionRepository _institutionRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetUserSummaryQueryHandler(
        IUserRepository userRepository,
        IInstitutionRepository institutionRepository,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _institutionRepository = institutionRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<UserSummaryDto>> Handle(
        GetUserSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var institutionId = _currentUserService.UserId is { } userId
            ? await _institutionRepository.GetPrimaryInstitutionIdByUserIdAsync(userId, cancellationToken)
            : null;
        var scope = InstitutionAccessScopeResolver.Resolve(
            _currentUserService.UserId,
            _currentUserService.Roles,
            institutionId);

        if (scope.IsFailure)
        {
            return Result.Failure<UserSummaryDto>(scope.Error);
        }

        return Result.Success(await _userRepository.GetSummaryAsync(
            scope.Value.InstitutionId,
            cancellationToken));
    }
}
