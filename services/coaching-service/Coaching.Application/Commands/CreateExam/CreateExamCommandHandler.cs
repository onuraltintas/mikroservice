using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Commands.CreateExam;

public class CreateExamCommandHandler : IRequestHandler<CreateExamCommand, CreateExamResponse>
{
    private readonly IExamRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public CreateExamCommandHandler(
        IExamRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
        _identityAuthorizationClient = identityAuthorizationClient;
    }

    public async Task<CreateExamResponse> Handle(CreateExamCommand command, CancellationToken cancellationToken)
    {
        _accessPolicy.RequireTeacher(command.TeacherId);
        var institutionId = await _identityAuthorizationClient.AuthorizeTeacherTargetsAsync(
            command.TeacherId,
            Array.Empty<Guid>(),
            command.InstitutionId,
            _accessPolicy.IsSystemAdministrator,
            cancellationToken);

        var exam = Exam.Create(
            command.TeacherId,
            command.Title,
            command.Type,
            command.ExamDate,
            command.MaxScore,
            institutionId,
            command.Description
        );

        await _repository.AddAsync(exam, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateExamResponse(exam.Id);
    }
}
