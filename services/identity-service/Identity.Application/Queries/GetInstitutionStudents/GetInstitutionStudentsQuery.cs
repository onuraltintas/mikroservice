using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetAllUsers;
using MediatR;

namespace Identity.Application.Queries.GetInstitutionStudents;

public sealed record GetInstitutionStudentsQuery(
    int PageNumber = 1,
    int PageSize = 100,
    string? SearchTerm = null,
    int? GradeLevel = null,
    bool? IsActive = null,
    Guid? TeacherUserId = null) : IRequest<Result<PagedList<InstitutionStudentRosterItem>>>;

public sealed class GetInstitutionStudentsQueryHandler : IRequestHandler<GetInstitutionStudentsQuery, Result<PagedList<InstitutionStudentRosterItem>>>
{
    private readonly IInstitutionRepository _institutionRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetInstitutionStudentsQueryHandler(
        IInstitutionRepository institutionRepository,
        ICurrentUserService currentUserService)
    {
        _institutionRepository = institutionRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<InstitutionStudentRosterItem>>> Handle(
        GetInstitutionStudentsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId is not { } userId)
        {
            return Result.Failure<PagedList<InstitutionStudentRosterItem>>(
                new Error("Auth.Unauthorized", "User is not authenticated"));
        }

        var institutionId = await _institutionRepository.GetInstitutionIdByAdminIdAsync(userId, cancellationToken);
        if (institutionId is null)
        {
            return Result.Failure<PagedList<InstitutionStudentRosterItem>>(
                new Error("Institution.Forbidden", "You are not an administrator of an institution"));
        }

        var pageNumber = Math.Clamp(request.PageNumber, 1, 10_000);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var students = await _institutionRepository.GetStudentRosterAsync(
            institutionId.Value,
            pageNumber,
            pageSize,
            request.SearchTerm,
            request.GradeLevel,
            request.IsActive,
            request.TeacherUserId,
            cancellationToken);

        return Result.Success(students);
    }
}
