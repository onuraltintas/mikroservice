using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Queries.GetUserProfile;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Queries.GetAllUsers;

public record GetAllUsersQuery(int PageNumber = 1, int PageSize = 25, string? SearchTerm = null, string? Role = null, bool? IsActive = null) : IRequest<Result<PagedList<UserProfileDto>>>;

public class PagedList<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }

    public PagedList(List<T> items, int count, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = count;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}
