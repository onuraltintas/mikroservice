using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetUserProfile;
using MediatR;

namespace Identity.Application.Queries.GetAllUsers;

public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, Result<PagedList<UserProfileDto>>>
{
    private readonly IUserRepository _userRepository;

    public GetAllUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<PagedList<UserProfileDto>>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var pagedDtos = await _userRepository.GetAllAsync(request.PageNumber, request.PageSize, request.SearchTerm, request.Role, request.IsActive, cancellationToken);
        return Result.Success(pagedDtos);
    }
}
