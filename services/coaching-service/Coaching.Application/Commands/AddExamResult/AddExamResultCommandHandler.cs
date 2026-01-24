using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;

using MediatR;

namespace Coaching.Application.Commands.AddExamResult;

public class AddExamResultCommandHandler : IRequestHandler<AddExamResultCommand>
{
    private readonly IExamRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddExamResultCommandHandler(IExamRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(AddExamResultCommand command, CancellationToken cancellationToken)
    {
        var exam = await _repository.GetByIdAsync(command.ExamId, cancellationToken);
        
        if (exam == null)
            throw new InvalidOperationException($"Exam {command.ExamId} not found");

        var result = ExamResult.Create(command.ExamId, command.StudentId, command.Score);
        
        result.SetAnswerStatistics(command.CorrectAnswers, command.WrongAnswers, command.EmptyAnswers);
        
        if (command.SubjectScores != null)
        {
            result.SetSubjectScores(command.SubjectScores);
        }

        if (!string.IsNullOrEmpty(command.Notes))
        {
            result.AddTeacherNotes(command.Notes);
        }

        exam.AddResult(result);

        await _repository.UpdateAsync(exam, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // TODO: Publish ExamResultAddedEvent (Analytics)
    }
}
