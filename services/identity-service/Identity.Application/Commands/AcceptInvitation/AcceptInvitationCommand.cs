using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.AcceptInvitation;

public record AcceptInvitationCommand(Guid InvitationId) : IRequest<Result>;
