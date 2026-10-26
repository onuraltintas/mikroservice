using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Queries.GetMySentInvitations;

public sealed record SentInvitationDto(
    Guid InvitationId,
    string Email,
    string Role,
    DateTime CreatedAt,
    DateTime ExpiresAt);

public sealed record GetMySentInvitationsQuery() : IRequest<Result<List<SentInvitationDto>>>;
