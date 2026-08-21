using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using Coaching.Application.Queries;

using MediatR;

namespace Coaching.Application.Queries.GetExamResults;

public class GetStudentExamResultsQueryHandler : IRequestHandler<GetStudentExamResultsQuery, PagedResponse<ExamResultDto>>
{
    private readonly IExamRepository _repository;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public GetStudentExamResultsQueryHandler(
        IExamRepository repository,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient)
    {
        _repository = repository;
        _accessPolicy = accessPolicy;
        _identityAuthorizationClient = identityAuthorizationClient;
    }

    public async Task<PagedResponse<ExamResultDto>> Handle(
        GetStudentExamResultsQuery query,
        CancellationToken cancellationToken)
    {
        await CoachingStudentReadAuthorization.RequireAsync(
            _accessPolicy,
            _identityAuthorizationClient,
            new[] { query.StudentId },
            cancellationToken);
        var page = await _repository.GetByStudentIdAsync(
            query.StudentId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);
        
        var results = new List<ExamResultDto>();

        foreach (var exam in page.Items)
        {
            var studentResult = exam.Results.FirstOrDefault(r => r.StudentId == query.StudentId);
            if (studentResult == null) continue;

            results.Add(new ExamResultDto(
                ExamId: exam.Id,
                ExamTitle: exam.Title,
                ExamDate: exam.ExamDate,
                ExamType: exam.ExamType.ToString(),
                Score: studentResult.Score,
                MaxScore: exam.MaxScore,
                CorrectAnswers: studentResult.CorrectAnswers,
                WrongAnswers: studentResult.WrongAnswers,
                EmptyAnswers: studentResult.EmptyAnswers,
                SubjectScores: studentResult.GetSubjectScores()
            ));
        }

        return new PagedResponse<ExamResultDto>(
            results,
            query.PageNumber,
            query.PageSize,
            page.TotalCount);
    }
}
