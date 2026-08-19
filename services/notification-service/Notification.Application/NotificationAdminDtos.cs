namespace Notification.Application.DTOs;

public sealed record SupportRequestDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Subject,
    string Message,
    string? IdempotencyKey,
    bool IsProcessed,
    string? AdminNote,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record EmailTemplateDto(
    Guid Id,
    string TemplateName,
    string Category,
    string Subject,
    string Body,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record PagedSupportRequestsDto(
    IReadOnlyList<SupportRequestDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);
