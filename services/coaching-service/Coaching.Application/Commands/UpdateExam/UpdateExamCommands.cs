using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Contracts.Events.Coaching;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Commands.UpdateExam;

public record UpdateExamCommand(
    Guid ExamId,
    string Title,
    ExamType Type,
    string? Subject,
    string? Description,
    DateTime ExamDate,
    int? DurationMinutes,
    decimal MaxScore,
    int? TargetGradeLevel
) : IRequest<UpdateExamResponse>;

public record UpdateExamResponse(Guid ExamId, DateTime ExamDate, decimal MaxScore);

public record UpdateExamResultCommand(
    Guid ExamId,
    Guid ResultId,
    decimal Score,
    int CorrectAnswers,
    int WrongAnswers,
    int EmptyAnswers,
    Dictionary<string, decimal>? SubjectScores,
    int? Ranking,
    string? Notes
) : IRequest<UpdateExamResultResponse>;

public record UpdateExamResultResponse(Guid ExamId, Guid ResultId, decimal Score);

public sealed class UpdateExamCommandValidator : AbstractValidator<UpdateExamCommand>
{
    public UpdateExamCommandValidator()
    {
        RuleFor(command => command.ExamId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.ExamDate).NotEqual(DateTime.MinValue);
        RuleFor(command => command.DurationMinutes)
            .InclusiveBetween(1, 480)
            .When(command => command.DurationMinutes.HasValue);
        RuleFor(command => command.MaxScore).InclusiveBetween(0.01m, 999.99m);
        RuleFor(command => command.TargetGradeLevel)
            .InclusiveBetween(1, 12)
            .When(command => command.TargetGradeLevel.HasValue);
        RuleFor(command => command.Subject).MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(2_000);
    }
}

public sealed class UpdateExamResultCommandValidator : AbstractValidator<UpdateExamResultCommand>
{
    public UpdateExamResultCommandValidator()
    {
        RuleFor(command => command.ExamId).NotEmpty();
        RuleFor(command => command.ResultId).NotEmpty();
        RuleFor(command => command.Score).GreaterThanOrEqualTo(0);
        RuleFor(command => command.CorrectAnswers).GreaterThanOrEqualTo(0);
        RuleFor(command => command.WrongAnswers).GreaterThanOrEqualTo(0);
        RuleFor(command => command.EmptyAnswers).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Ranking).GreaterThan(0).When(command => command.Ranking.HasValue);
        RuleFor(command => command.Notes).MaximumLength(2_000);
    }
}

public sealed class UpdateExamCommandHandler
    : IRequestHandler<UpdateExamCommand, UpdateExamResponse>
{
    private readonly IExamRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingEventPublisher _eventPublisher;

    public UpdateExamCommandHandler(
        IExamRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingEventPublisher eventPublisher)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
        _eventPublisher = eventPublisher;
    }

    public async Task<UpdateExamResponse> Handle(
        UpdateExamCommand command,
        CancellationToken cancellationToken)
    {
        var exam = await _repository.GetByIdAsync(command.ExamId, cancellationToken)
            ?? throw new InvalidOperationException($"Exam {command.ExamId} not found");

        _accessPolicy.RequireTeacher(exam.CreatedByTeacherId);
        exam.UpdateEditableDetails(
            command.Title,
            command.Type,
            command.Subject,
            command.Description,
            command.ExamDate,
            command.DurationMinutes,
            command.MaxScore,
            command.TargetGradeLevel);

        await _eventPublisher.PublishAsync(
            new ExamUpdatedEvent(
                exam.Id,
                exam.CreatedByTeacherId,
                exam.InstitutionId,
                exam.Title,
                exam.ExamDate,
                exam.MaxScore,
                exam.Results.Select(result => result.StudentId).ToArray()),
            cancellationToken);
        await _repository.UpdateAsync(exam, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateExamResponse(exam.Id, exam.ExamDate, exam.MaxScore);
    }
}

public sealed class UpdateExamResultCommandHandler
    : IRequestHandler<UpdateExamResultCommand, UpdateExamResultResponse>
{
    private readonly IExamRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingEventPublisher _eventPublisher;

    public UpdateExamResultCommandHandler(
        IExamRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingEventPublisher eventPublisher)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
        _eventPublisher = eventPublisher;
    }

    public async Task<UpdateExamResultResponse> Handle(
        UpdateExamResultCommand command,
        CancellationToken cancellationToken)
    {
        var exam = await _repository.GetByIdAsync(command.ExamId, cancellationToken)
            ?? throw new InvalidOperationException($"Exam {command.ExamId} not found");

        _accessPolicy.RequireTeacher(exam.CreatedByTeacherId);
        exam.UpdateResult(
            command.ResultId,
            command.Score,
            command.CorrectAnswers,
            command.WrongAnswers,
            command.EmptyAnswers,
            command.SubjectScores,
            command.Ranking,
            command.Notes);

        var result = exam.Results.Single(item => item.Id == command.ResultId);
        await _eventPublisher.PublishAsync(
            new ExamResultUpdatedEvent(
                exam.Id,
                result.Id,
                result.StudentId,
                result.Score,
                result.Ranking),
            cancellationToken);
        await _repository.UpdateAsync(exam, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateExamResultResponse(exam.Id, result.Id, result.Score);
    }
}
