using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.CancelSentInvitation;

public sealed record CancelSentInvitationCommand(Guid InvitationId) : IRequest<Result>;
