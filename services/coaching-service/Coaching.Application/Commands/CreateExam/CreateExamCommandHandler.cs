using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;

using MediatR;

namespace Coaching.Application.Commands.CreateExam;

public class CreateExamCommandHandler : IRequestHandler<CreateExamCommand, CreateExamResponse>
{
    private readonly IExamRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateExamCommandHandler(IExamRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateExamResponse> Handle(CreateExamCommand command, CancellationToken cancellationToken)
    {
        var exam = Exam.Create(
            command.TeacherId,
            command.Title,
            command.Type,
            command.ExamDate,
            command.MaxScore,
            command.InstitutionId
        );

        if (!string.IsNullOrEmpty(command.Description))
        {
            // UpdateDetails description parametresini almıyor entity'de (Subject update details alıyor). 
            // Ama CreateExam açıklama alanı nerede saklanıyor? Entity'de Description yok!
            // Wait, entity de 'Subject' parametresi var. Belki Description yerine Subject kullanmalıyım.
            // Let's use Subject for description or general explanation.
            exam.UpdateDetails(subject: command.Description);
        }

        await _repository.AddAsync(exam, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateExamResponse(exam.Id);
    }
}
