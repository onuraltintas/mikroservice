using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Commands.Login;
using MediatR;

namespace Identity.Application.Commands.OpenCourseManagementSession;

public sealed record OpenCourseManagementSessionCommand(
    Guid UserId,
    string IpAddress,
    DateTimeOffset? MfaVerifiedAt) : IRequest<Result<LoginResponse>>;
