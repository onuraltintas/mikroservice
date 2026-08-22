using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetAllUsers;
using MediatR;

namespace Identity.Application.Queries.GetTeacherStudents;

public sealed record TeacherStudentDto(
    Guid UserId,
    string FirstName,
    string LastName,
    string FullName,
    int? GradeLevel,
    Guid? InstitutionId,
    string? InstitutionName,
    string? AvatarUrl,
    string? Subject,
    DateTime AssignmentStartDate);

public sealed record GetTeacherStudentsQuery(
    int PageNumber = 1,
    int PageSize = 25,
    string? SearchTerm = null) : IRequest<Result<PagedList<TeacherStudentDto>>>;

public sealed class GetTeacherStudentsQueryHandler(
    ITeacherRepository teacherRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetTeacherStudentsQuery, Result<PagedList<TeacherStudentDto>>>
{
    public async Task<Result<PagedList<TeacherStudentDto>>> Handle(
        GetTeacherStudentsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid teacherUserId)
        {
            return Result.Failure<PagedList<TeacherStudentDto>>(
                Error.Unauthorized("Oturum açılmış kullanıcı bulunamadı."));
        }

        if (!currentUserService.Roles.Any(role =>
                string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<PagedList<TeacherStudentDto>>(
                Error.Forbidden("Bu kaynak yalnızca öğretmen rolü için kullanılabilir."));
        }

        var pageNumber = Math.Clamp(request.PageNumber, 1, GetAllUsersQuery.MaxPageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, GetAllUsersQuery.MaxPageSize);
        var searchTerm = string.IsNullOrWhiteSpace(request.SearchTerm)
            ? null
            : request.SearchTerm.Trim();

        var students = await teacherRepository.GetStudentsByTeacherUserIdAsync(
            teacherUserId,
            pageNumber,
            pageSize,
            searchTerm,
            cancellationToken);

        return Result.Success(students);
    }
}
