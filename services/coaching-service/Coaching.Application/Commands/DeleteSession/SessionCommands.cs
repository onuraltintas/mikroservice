using MediatR;

namespace Coaching.Application.Commands.DeleteSession;

public record CancelSessionCommand(Guid SessionId) : IRequest;
public record DeleteSessionCommand(Guid SessionId) : IRequest;
