using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Application.Authorization;
using Coaching.Application.Exceptions;
using Coaching.Application.Idempotency;
using EduPlatform.Shared.Kernel.Exceptions;

using MediatR;

namespace Coaching.Application.Commands.CreateExam;

public class CreateExamCommandHandler : IRequestHandler<CreateExamCommand, CreateExamResponse>
{
    private const string IdempotencyScope = "coaching.exams.create";

    private readonly IExamRepository _repository;
    private readonly IIdempotencyRepository _idempotencyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public CreateExamCommandHandler(
        IExamRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient,
        IIdempotencyRepository idempotencyRepository)
    {
        _repository = repository;
        _idempotencyRepository = idempotencyRepository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
        _identityAuthorizationClient = identityAuthorizationClient;
    }

    public async Task<CreateExamResponse> Handle(CreateExamCommand command, CancellationToken cancellationToken)
    {
        _accessPolicy.RequireTeacher(command.TeacherId);

        var key = command.IdempotencyKey?.Trim();
        EnsureKey(key);
        var requestHash = IdempotencyRequestHasher.Create(
            IdempotencyRequestHasher.Format(command.TeacherId),
            command.Title,
            command.Type.ToString(),
            IdempotencyRequestHasher.Format(command.ExamDate),
            IdempotencyRequestHasher.Format(command.MaxScore),
            IdempotencyRequestHasher.Format(command.InstitutionId),
            command.Description);
        var existing = await _idempotencyRepository.GetAsync(IdempotencyScope, key!, cancellationToken);
        if (existing is not null)
        {
            if (!existing.Matches(requestHash))
                throw IdempotencyConflict();

            return await ReplayAsync(existing.ResourceId, cancellationToken);
        }

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
        await _idempotencyRepository.AddAsync(
            IdempotencyRecord.Create(IdempotencyScope, key!, requestHash, exam.Id),
            cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (IdempotencyConflictException)
        {
            var concurrent = await _idempotencyRepository.GetAsync(IdempotencyScope, key!, cancellationToken);
            if (concurrent is not null && concurrent.Matches(requestHash))
                return await ReplayAsync(concurrent.ResourceId, cancellationToken);

            throw IdempotencyConflict();
        }

        return new CreateExamResponse(exam.Id);
    }

    private async Task<CreateExamResponse> ReplayAsync(Guid examId, CancellationToken cancellationToken)
    {
        if (await _repository.GetByIdAsync(examId, cancellationToken) is null)
        {
            throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait sınav bulunamadı; yeni bir anahtar kullanın.");
        }

        return new CreateExamResponse(examId);
    }

    private static void EnsureKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new BusinessRuleException(
                "Idempotency.Required",
                "Sınav oluşturma isteği için Idempotency-Key zorunludur.");
        }
    }

    private static BusinessRuleException IdempotencyConflict() => new(
        "Idempotency.Conflict",
        "Aynı Idempotency-Key farklı bir istek gövdesiyle tekrar kullanılamaz.");
}
