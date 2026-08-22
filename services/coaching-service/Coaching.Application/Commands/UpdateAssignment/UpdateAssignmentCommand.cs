using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Contracts.Events.Coaching;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Commands.UpdateAssignment;

public record UpdateAssignmentCommand(
    Guid AssignmentId,
    string Title,
    string? Description,
    string? Subject,
    string AssignmentSource,
    int? TargetGradeLevel,
    string? BookTitle,
    string? BookIsbn,
    string? BookEdition,
    string? BookChapter,
    int? BookStartPage,
    int? BookEndPage,
    int? BookStartQuestion,
    int? BookEndQuestion,
    DateTime DueDate,
    int? EstimatedDurationMinutes,
    decimal? MaxScore,
    decimal? PassingScore,
    IReadOnlyCollection<Guid>? StudentIds = null
) : IRequest<UpdateAssignmentResponse>;

public record UpdateAssignmentResponse(
    Guid AssignmentId,
    DateTime DueDate,
    int AssignedStudentCount);

public sealed class UpdateAssignmentCommandValidator : AbstractValidator<UpdateAssignmentCommand>
{
    public UpdateAssignmentCommandValidator()
    {
        RuleFor(command => command.AssignmentId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.DueDate)
            .NotEqual(DateTime.MinValue)
            .WithMessage("Due date is required.");
        RuleFor(command => command.AssignmentSource)
            .Must(IsValidAssignmentSource)
            .WithMessage("Assignment source must be Digital, Book or Mixed.");
        RuleFor(command => command.TargetGradeLevel)
            .InclusiveBetween(1, 12)
            .When(command => command.TargetGradeLevel.HasValue);
        RuleFor(command => command.EstimatedDurationMinutes)
            .InclusiveBetween(1, 240)
            .When(command => command.EstimatedDurationMinutes.HasValue);
        RuleFor(command => command.MaxScore)
            .InclusiveBetween(0.01m, 999.99m)
            .When(command => command.MaxScore.HasValue);
        RuleFor(command => command.PassingScore)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(command => command.MaxScore!.Value)
            .When(command => command.PassingScore.HasValue && command.MaxScore.HasValue);
        RuleFor(command => command)
            .Must(command => !command.PassingScore.HasValue || command.MaxScore.HasValue)
            .WithMessage("Passing score requires a maximum score.");
        RuleFor(command => command.StudentIds)
            .NotEmpty()
            .Must(ids => ids is not null && ids.Count <= 100)
            .WithMessage("At most 100 students may be assigned.")
            .Must(ids => ids is not null && ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count)
            .WithMessage("Student IDs must be non-empty and unique.")
            .When(command => command.StudentIds is not null);

        When(command => IsBookSource(command.AssignmentSource), () =>
        {
            RuleFor(command => command.BookTitle).NotEmpty().MaximumLength(200);
            RuleFor(command => command.BookStartPage).NotNull().GreaterThan(0);
            RuleFor(command => command.BookEndPage)
                .NotNull()
                .GreaterThanOrEqualTo(command => command.BookStartPage);
            RuleFor(command => command)
                .Must(command => command.BookStartQuestion.HasValue == command.BookEndQuestion.HasValue)
                .WithMessage("Book question range must include both start and end.");
            RuleFor(command => command.BookStartQuestion)
                .GreaterThan(0)
                .When(command => command.BookStartQuestion.HasValue);
            RuleFor(command => command.BookEndQuestion)
                .GreaterThanOrEqualTo(command => command.BookStartQuestion)
                .When(command => command.BookEndQuestion.HasValue);
        });

        When(command => !IsBookSource(command.AssignmentSource), () =>
        {
            RuleFor(command => command.BookTitle).Empty();
            RuleFor(command => command.BookStartPage).Null();
            RuleFor(command => command.BookEndPage).Null();
            RuleFor(command => command.BookStartQuestion).Null();
            RuleFor(command => command.BookEndQuestion).Null();
        });
    }

    private static bool IsBookSource(string source) =>
        IsValidAssignmentSource(source)
        && Enum.TryParse<AssignmentSource>(source, true, out var parsed)
        && parsed is AssignmentSource.Book or AssignmentSource.Mixed;

    private static bool IsValidAssignmentSource(string source) =>
        Enum.TryParse<AssignmentSource>(source, true, out var parsed)
        && Enum.IsDefined(parsed);
}

public sealed class UpdateAssignmentCommandHandler
    : IRequestHandler<UpdateAssignmentCommand, UpdateAssignmentResponse>
{
    private readonly IAssignmentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;
    private readonly ICoachingEventPublisher _eventPublisher;

    public UpdateAssignmentCommandHandler(
        IAssignmentRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient,
        ICoachingEventPublisher eventPublisher)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
        _identityAuthorizationClient = identityAuthorizationClient;
        _eventPublisher = eventPublisher;
    }

    public async Task<UpdateAssignmentResponse> Handle(
        UpdateAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetByIdAsync(command.AssignmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Assignment {command.AssignmentId} not found");

        _accessPolicy.RequireTeacher(assignment.TeacherId);
        assignment.UpdateEditableDetails(
            command.Title,
            command.Description,
            command.Subject,
            command.DueDate,
            command.EstimatedDurationMinutes);

        var source = Enum.Parse<AssignmentSource>(command.AssignmentSource, true);
        if (source is AssignmentSource.Book or AssignmentSource.Mixed)
        {
            assignment.SetBookReference(
                command.BookTitle!,
                command.BookStartPage!.Value,
                command.BookEndPage!.Value,
                command.BookStartQuestion,
                command.BookEndQuestion,
                command.BookIsbn,
                command.BookEdition,
                command.BookChapter,
                source);
        }
        else
        {
            assignment.ClearBookReference();
        }

        if (command.MaxScore.HasValue)
        {
            assignment.SetScoring(command.MaxScore.Value, command.PassingScore);
        }
        else
        {
            assignment.ClearScoring();
        }

        if (command.TargetGradeLevel.HasValue)
        {
            assignment.SetTargetGradeLevel(command.TargetGradeLevel.Value);
        }
        else
        {
            assignment.ClearTargetGradeLevel();
        }

        if (command.StudentIds is not null)
        {
            await _identityAuthorizationClient.AuthorizeTeacherTargetsAsync(
                assignment.TeacherId,
                command.StudentIds,
                assignment.InstitutionId,
                _accessPolicy.IsSystemAdministrator,
                cancellationToken);
            assignment.ReassignStudents(command.StudentIds);
        }

        await _eventPublisher.PublishAsync(
            new AssignmentUpdatedEvent(
                assignment.Id,
                assignment.TeacherId,
                assignment.InstitutionId,
                assignment.Title,
                assignment.DueDate,
                assignment.AssignedStudents.Select(student => student.StudentId).ToArray()),
            cancellationToken);
        await _repository.UpdateAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateAssignmentResponse(
            assignment.Id,
            assignment.DueDate,
            assignment.AssignedStudents.Count);
    }
}
