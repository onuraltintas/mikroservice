using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.JoinCourseManagement;

public sealed record JoinCourseManagementCommand(string Role) : IRequest<Result>;
