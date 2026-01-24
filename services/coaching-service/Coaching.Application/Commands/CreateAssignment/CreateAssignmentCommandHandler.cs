using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Application.Interfaces;

using MediatR;

namespace Coaching.Application.Commands.CreateAssignment;

/// <summary>
/// CreateAssignmentCommand Handler
/// </summary>
public class CreateAssignmentCommandHandler : IRequestHandler<CreateAssignmentCommand, CreateAssignmentResponse>
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAssignmentCommandHandler(
        IAssignmentRepository assignmentRepository,
        IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateAssignmentResponse> Handle(CreateAssignmentCommand request, CancellationToken cancellationToken)
    {
        // Parse AssignmentType
        var assignmentType = Enum.Parse<AssignmentType>(request.AssignmentType, ignoreCase: true);

        // Create Assignment aggregate
        var assignment = Assignment.Create(
            teacherId: request.TeacherId,
            title: request.Title,
            dueDate: request.DueDate,
            type: assignmentType,
            institutionId: request.InstitutionId
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
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // TODO: Publish AssignmentCreatedEvent via MassTransit (for notifications)

        return new CreateAssignmentResponse(
            AssignmentId: assignment.Id,
            Title: assignment.Title,
            DueDate: assignment.DueDate,
            AssignedStudentCount: assignment.AssignedStudents.Count
        );
    }
}
