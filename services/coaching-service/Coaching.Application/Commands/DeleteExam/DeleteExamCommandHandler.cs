using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Commands.DeleteExam;

public class DeleteExamCommandHandler : IRequestHandler<DeleteExamCommand>
{
    private readonly IExamRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _teacherRecords;

    public DeleteExamCommandHandler(
        IExamRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient teacherRecords)
    {
        _teacherRecords = teacherRecords;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
    }

    public async Task Handle(DeleteExamCommand command, CancellationToken cancellationToken)
    {
        var exam = await _repository.GetByIdAsync(command.ExamId, cancellationToken);
        if (exam == null) throw new InvalidOperationException("Exam not found");

        await _teacherRecords.RequireCurrentTeacherAsync(_accessPolicy, exam.RequireTeacherCreator(), exam.InstitutionId,
            command.IsInstitutionAdminOperation, cancellationToken);

        await _repository.DeleteAsync(exam, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
