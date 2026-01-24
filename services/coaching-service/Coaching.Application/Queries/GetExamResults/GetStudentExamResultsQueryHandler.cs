using Coaching.Application.Interfaces;

using MediatR;

namespace Coaching.Application.Queries.GetExamResults;

public class GetStudentExamResultsQueryHandler : IRequestHandler<GetStudentExamResultsQuery, List<ExamResultDto>>
{
    private readonly IExamRepository _repository;

    public GetStudentExamResultsQueryHandler(IExamRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ExamResultDto>> Handle(
        GetStudentExamResultsQuery query,
        CancellationToken cancellationToken)
    {
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
