using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Application.Authorization;
using Coaching.Application.Exceptions;
using Coaching.Application.Idempotency;
using EduPlatform.Shared.Kernel.Exceptions;

using MediatR;

namespace Coaching.Application.Commands.AddExamResult;

public class AddExamResultCommandHandler : IRequestHandler<AddExamResultCommand>
{
    private const string IdempotencyScope = "coaching.exam-results.create";

    private readonly IExamRepository _repository;
    private readonly IIdempotencyRepository _idempotencyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public AddExamResultCommandHandler(
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

    public async Task Handle(AddExamResultCommand command, CancellationToken cancellationToken)
    {
        var exam = await _repository.GetByIdAsync(command.ExamId, cancellationToken);
        
        if (exam == null)
            throw new InvalidOperationException($"Exam {command.ExamId} not found");

        _accessPolicy.RequireTeacher(exam.CreatedByTeacherId);
        await _identityAuthorizationClient.AuthorizeTeacherTargetsAsync(
            exam.CreatedByTeacherId,
            new[] { command.StudentId },
            exam.InstitutionId,
            _accessPolicy.IsSystemAdministrator,
            cancellationToken);

        var key = command.IdempotencyKey?.Trim();
        EnsureKey(key);
        var requestHash = IdempotencyRequestHasher.Create(
            IdempotencyRequestHasher.Format(command.ExamId),
            IdempotencyRequestHasher.Format(command.StudentId),
            IdempotencyRequestHasher.Format(command.Score),
            command.CorrectAnswers.ToString(),
            command.WrongAnswers.ToString(),
            command.EmptyAnswers.ToString(),
            IdempotencyRequestHasher.Format(command.SubjectScores),
            command.Notes);
        var existing = await _idempotencyRepository.GetAsync(IdempotencyScope, key!, cancellationToken);
        if (existing is not null)
        {
            if (!existing.Matches(requestHash))
                throw IdempotencyConflict();

            if (exam.Results.Any(result => result.Id == existing.ResourceId))
                return;

            throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait sınav sonucu bulunamadı; yeni bir anahtar kullanın.");
        }

        if (command.Score < 0)
        {
            throw new EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException(
                "Exam.ScoreInvalid",
                "Sınav sonucu negatif olamaz.");
        }

        if (command.Score > exam.MaxScore)
        {
            throw new EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException(
                "Exam.ScoreExceeded",
                "Sınav sonucu maksimum puanı aşamaz.");
        }

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
        await _idempotencyRepository.AddAsync(
            IdempotencyRecord.Create(IdempotencyScope, key!, requestHash, result.Id),
            cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (IdempotencyConflictException)
        {
            var concurrent = await _idempotencyRepository.GetAsync(IdempotencyScope, key!, cancellationToken);
            if (concurrent is not null && concurrent.Matches(requestHash))
                return;

            throw IdempotencyConflict();
        }

        // TODO: Publish ExamResultAddedEvent (Analytics)
    }

    private static void EnsureKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new BusinessRuleException(
                "Idempotency.Required",
                "Sınav sonucu oluşturma isteği için Idempotency-Key zorunludur.");
        }
    }

    private static BusinessRuleException IdempotencyConflict() => new(
        "Idempotency.Conflict",
        "Aynı Idempotency-Key farklı bir istek gövdesiyle tekrar kullanılamaz.");
}
