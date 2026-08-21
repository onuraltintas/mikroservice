using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using MediatR;

namespace Coaching.Application.Queries.GetStudentProgress;

public sealed class GetStudentProgressQueryHandler(
    ICoachingStudentProgressRepository repository,
    ICoachingAccessPolicy accessPolicy,
    ICoachingIdentityAuthorizationClient identityAuthorizationClient)
    : IRequestHandler<GetStudentProgressQuery, StudentProgressSummaryDto>
{
    public async Task<StudentProgressSummaryDto> Handle(
        GetStudentProgressQuery request,
        CancellationToken cancellationToken)
    {
        await CoachingStudentReadAuthorization.RequireAsync(
            accessPolicy,
            identityAuthorizationClient,
            [request.StudentId],
            cancellationToken);

        return await repository.GetStudentSummaryAsync(request.StudentId, cancellationToken);
    }
}
