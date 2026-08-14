using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Queries.GetExamResults;

public class GetStudentExamResultsQueryHandler : IRequestHandler<GetStudentExamResultsQuery, List<ExamResultDto>>
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

    public async Task<List<ExamResultDto>> Handle(
        GetStudentExamResultsQuery query,
        CancellationToken cancellationToken)
    {
        await CoachingStudentReadAuthorization.RequireAsync(
            _accessPolicy,
            _identityAuthorizationClient,
            new[] { query.StudentId },
            cancellationToken);
        var exams = await _repository.GetByStudentIdAsync(query.StudentId, cancellationToken);
        
        var results = new List<ExamResultDto>();

        foreach (var exam in exams)
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

        return results;
    }
}
