using MediatR;

namespace Coaching.Application.Queries.GetAssignment;

/// <summary>
/// Get Assignment By ID Query
/// </summary>
public record GetAssignmentQuery(Guid AssignmentId) : IRequest<AssignmentResponse?>;

/// <summary>
/// Assignment Response DTO
/// </summary>
public record AssignmentResponse(
    Guid Id,
    Guid TeacherId,
    Guid? InstitutionId,
    string Title,
    string? Description,
    string? Subject,
    string Type,
    string Source,
    string? BookTitle,
    string? BookIsbn,
    string? BookEdition,
    string? BookChapter,
    int? BookStartPage,
    int? BookEndPage,
    int? BookStartQuestion,
    int? BookEndQuestion,
    int? TargetGradeLevel,
    DateTime DueDate,
    int? EstimatedDurationMinutes,
    decimal? MaxScore,
    decimal? PassingScore,
    string Status,
    List<AssignedStudentDto> AssignedStudents,
    DateTime CreatedAt
);

public record AssignedStudentDto(
    Guid StudentId,
    DateTime? SubmittedAt,
    decimal? Score,
    string? TeacherFeedback,
    string Status,
    IReadOnlyList<AssignmentAttachmentDto>? Attachments = null
);

public record AssignmentAttachmentDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Status,
    DateTime? UploadedAt,
    DateTime? ScannedAt
);
