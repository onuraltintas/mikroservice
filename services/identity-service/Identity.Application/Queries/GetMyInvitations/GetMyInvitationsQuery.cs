using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Queries.GetMyInvitations;

public class InvitationDto
{
    public Guid Id { get; set; }
    public string InviterEmail { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public record GetMyInvitationsQuery() : IRequest<Result<List<InvitationDto>>>;
