using MediatR;

namespace Coaching.Application.Commands.CreateAssignment;

/// <summary>
/// Ödev oluşturma komutu
/// </summary>
public record CreateAssignmentCommand : IRequest<CreateAssignmentResponse>
{
    public Guid TeacherId { get; init; }
    public Guid? InstitutionId { get; init; }
    
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Subject { get; init; }
    
    public string AssignmentType { get; init; } = "Individual"; // Individual, Group
    public int? TargetGradeLevel { get; init; }
    
    public DateTime DueDate { get; init; }
    public int? EstimatedDurationMinutes { get; init; }
    
    public decimal? MaxScore { get; init; }
    public decimal? PassingScore { get; init; }
    
    public List<Guid> StudentIds { get; init; } = new();
}

public record CreateAssignmentResponse(
    Guid AssignmentId,
    string Title,
    DateTime DueDate,
    int AssignedStudentCount
);
