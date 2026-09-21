using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Commands.ManageParentStudentRelationships;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetAllUsers;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Queries.GetParentStudentRelationships;

public sealed record ParentStudentRelationshipDto(
    Guid Id, Guid ParentUserId, string ParentName, string ParentEmail,
    Guid StudentUserId, string StudentName, string StudentEmail,
    string Relationship, string Status, DateTime RequestedAt,
    DateTime? VerifiedAt, DateTime? RevokedAt, string? RevocationReason);

public sealed record GetParentStudentRelationshipsQuery(
    ParentStudentRelationshipStatus? Status = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 25) : IRequest<Result<PagedList<ParentStudentRelationshipDto>>>;

public sealed class GetParentStudentRelationshipsQueryHandler(
    IParentStudentRelationshipRepository repository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetParentStudentRelationshipsQuery, Result<PagedList<ParentStudentRelationshipDto>>>
{
    public async Task<Result<PagedList<ParentStudentRelationshipDto>>> Handle(
        GetParentStudentRelationshipsQuery request,
        CancellationToken cancellationToken)
    {
        var actor = ParentStudentRelationshipManagement.GetSystemAdministrator(currentUser);
        if (actor.IsFailure)
            return Result.Failure<PagedList<ParentStudentRelationshipDto>>(actor.Error);

        var pageNumber = Math.Clamp(request.PageNumber, 1, GetAllUsersQuery.MaxPageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, GetAllUsersQuery.MaxPageSize);
        var page = await repository.SearchAsync(
            request.Status, request.Search, pageNumber, pageSize, cancellationToken);
        var items = page.Items.Select(item => new ParentStudentRelationshipDto(
            item.Id, item.ParentUserId, item.ParentName, item.ParentEmail,
            item.StudentUserId, item.StudentName, item.StudentEmail,
            item.Relationship.ToString(), item.Status.ToString(), item.RequestedAt,
            item.VerifiedAt, item.RevokedAt, item.RevocationReason)).ToList();

        return Result.Success(new PagedList<ParentStudentRelationshipDto>(
            items, page.TotalCount, pageNumber, pageSize));
    }
}
