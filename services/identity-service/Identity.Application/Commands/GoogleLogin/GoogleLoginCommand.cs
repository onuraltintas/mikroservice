using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Commands.Login;
using Identity.Application.LegalPages;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.GoogleLogin;

public record GoogleLoginCommand(
    string IdToken,
    string IpAddress,
    PlatformProduct? Product = null,
    IReadOnlyList<LegalPageAcceptance>? LegalAcceptances = null,
    /// <summary>Roadmap O11: campaign tags of the landing page, stored when the login creates an account.</summary>
    Identity.Application.Registration.RegistrationUtm? Utm = null,
    /// <summary>maxkampus.com: what a new account says about itself (kind, phone, workspace name).</summary>
    Identity.Application.Commands.RegisterCourseManagement.CourseManagementSignup? Signup = null) : IRequest<Result<LoginResponse>>;
