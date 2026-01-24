using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.RejectInvitation;

public record RejectInvitationCommand(Guid InvitationId) : IRequest<Result>;
