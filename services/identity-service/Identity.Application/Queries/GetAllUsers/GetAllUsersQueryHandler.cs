using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Authorization;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetUserProfile;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Queries.GetAllUsers;

public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, Result<PagedList<UserProfileDto>>>
{
    private readonly IUserRepository _userRepository;
    private readonly IInstitutionRepository _institutionRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetAllUsersQueryHandler(
        IUserRepository userRepository,
        IInstitutionRepository institutionRepository,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _institutionRepository = institutionRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<UserProfileDto>>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var institutionId = _currentUserService.UserId is { } userId
            ? await _institutionRepository.GetPrimaryInstitutionIdByUserIdAsync(
                userId,
                request.Product ?? PlatformProduct.Coaching,
                cancellationToken)
            : null;
        var scope = InstitutionAccessScopeResolver.Resolve(
            _currentUserService.UserId,
            _currentUserService.Roles,
            institutionId);

        if (scope.IsFailure)
        {
            return Result.Failure<PagedList<UserProfileDto>>(scope.Error);
        }

        var pagedDtos = await _userRepository.GetAllAsync(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.Role,
            request.IsActive,
            scope.Value.InstitutionId,
            cancellationToken,
            request.Product);
        return Result.Success(pagedDtos);
    }
}
