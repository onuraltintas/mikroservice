using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using Coaching.Application.Exceptions;
using Coaching.Application.Idempotency;
using EduPlatform.Shared.Kernel.Exceptions;

using MediatR;

namespace Coaching.Application.Commands.CreateAssignment;

/// <summary>
/// CreateAssignmentCommand Handler
/// </summary>
public class CreateAssignmentCommandHandler : IRequestHandler<CreateAssignmentCommand, CreateAssignmentResponse>
{
    private const string IdempotencyScope = "coaching.assignments.create";

    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IIdempotencyRepository _idempotencyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public CreateAssignmentCommandHandler(
        IAssignmentRepository assignmentRepository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient,
        IIdempotencyRepository idempotencyRepository)
    {
        _assignmentRepository = assignmentRepository;
        _idempotencyRepository = idempotencyRepository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
        _identityAuthorizationClient = identityAuthorizationClient;
    }

    public async Task<CreateAssignmentResponse> Handle(CreateAssignmentCommand request, CancellationToken cancellationToken)
    {
        _accessPolicy.RequireTeacher(request.TeacherId);

        var key = request.IdempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new BusinessRuleException(
                "Idempotency.Required",
                "Ödev oluşturma isteği için Idempotency-Key zorunludur.");
        }

        var requestHash = CreateRequestHash(request);
        var existing = await _idempotencyRepository.GetAsync(
            IdempotencyScope,
            key,
            cancellationToken);
        if (existing is not null)
        {
            if (!existing.Matches(requestHash))
            {
                throw IdempotencyConflict();
            }

            return await GetReplayResponseAsync(existing.ResourceId, cancellationToken);
        }

        var institutionId = await _identityAuthorizationClient.AuthorizeTeacherTargetsAsync(
            request.TeacherId,
            request.StudentIds,
            request.InstitutionId,
            _accessPolicy.IsSystemAdministrator,
            cancellationToken);

        // Parse AssignmentType
        var assignmentType = Enum.Parse<AssignmentType>(request.AssignmentType, ignoreCase: true);

        // Create Assignment aggregate
        var assignment = Assignment.Create(
            teacherId: request.TeacherId,
            title: request.Title,
            dueDate: request.DueDate,
            type: assignmentType,
            institutionId: institutionId
        );

        // Set optional details
        if (!string.IsNullOrWhiteSpace(request.Description) || 
            !string.IsNullOrWhiteSpace(request.Subject) ||
            request.EstimatedDurationMinutes.HasValue)
        {
            assignment.UpdateDetails(
                description: request.Description,
                subject: request.Subject,
                estimatedDurationMinutes: request.EstimatedDurationMinutes
            );
        }

        // Set scoring if provided
        if (request.MaxScore.HasValue)
        {
            assignment.SetScoring(request.MaxScore.Value, request.PassingScore);
        }

        // Set target grade level
        if (request.TargetGradeLevel.HasValue)
        {
            assignment.SetTargetGradeLevel(request.TargetGradeLevel.Value);
        }

        // Assign to students
        if (request.StudentIds.Any())
        {
            assignment.AssignToStudents(request.StudentIds);
        }

        // Save to repository
        await _assignmentRepository.AddAsync(assignment, cancellationToken);
        await _idempotencyRepository.AddAsync(
            IdempotencyRecord.Create(
                IdempotencyScope,
                key,
                requestHash,
                assignment.Id),
            cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (IdempotencyConflictException)
        {
            var concurrent = await _idempotencyRepository.GetAsync(
                IdempotencyScope,
                key,
                cancellationToken);
            if (concurrent is not null && concurrent.Matches(requestHash))
            {
                return await GetReplayResponseAsync(concurrent.ResourceId, cancellationToken);
            }

            throw IdempotencyConflict();
        }

        // TODO: Publish AssignmentCreatedEvent via MassTransit (for notifications)

        return new CreateAssignmentResponse(
            AssignmentId: assignment.Id,
            Title: assignment.Title,
            DueDate: assignment.DueDate,
            AssignedStudentCount: assignment.AssignedStudents.Count
        );
    }

    private async Task<CreateAssignmentResponse> GetReplayResponseAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await _assignmentRepository.GetByIdAsync(assignmentId, cancellationToken);
        if (assignment is null)
        {
            throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait ödev bulunamadı; yeni bir anahtar kullanın.");
        }

        return new CreateAssignmentResponse(
            assignment.Id,
            assignment.Title,
            assignment.DueDate,
            assignment.AssignedStudents.Count);
    }

    private static BusinessRuleException IdempotencyConflict() => new(
        "Idempotency.Conflict",
        "Aynı Idempotency-Key farklı bir istek gövdesiyle tekrar kullanılamaz.");

    private static string CreateRequestHash(CreateAssignmentCommand request)
    {
        var assignmentType = Enum.Parse<AssignmentType>(request.AssignmentType, ignoreCase: true);
        return IdempotencyRequestHasher.Create(
            IdempotencyRequestHasher.Format(request.TeacherId),
            IdempotencyRequestHasher.Format(request.InstitutionId),
            request.Title,
            request.Description,
            request.Subject,
            assignmentType.ToString(),
            request.TargetGradeLevel?.ToString(),
            IdempotencyRequestHasher.Format(request.DueDate),
            request.EstimatedDurationMinutes?.ToString(),
            IdempotencyRequestHasher.Format(request.MaxScore),
            IdempotencyRequestHasher.Format(request.PassingScore),
            IdempotencyRequestHasher.Format(request.StudentIds));
    }
}
