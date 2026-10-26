using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Authorization;
using Identity.Application.Authorization;
using Identity.Application.Commands.Login;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using Identity.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Commands.OpenCourseManagementSession;

public sealed class OpenCourseManagementSessionCommandHandler(
    IUserRepository users,
    IAuthenticationSessionIssuer sessionIssuer,
    IConfigurationService configurationService,
    ILogger<OpenCourseManagementSessionCommandHandler> logger)
    : IRequestHandler<OpenCourseManagementSessionCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(
        OpenCourseManagementSessionCommand request,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure<LoginResponse>(
                new Error("Auth.Unauthorized", "Giriş yapılmalıdır."));
        }

        var productRoles = user.GetRolesForProductScope(PlatformProduct.CourseManagement)
            .Where(userRole => userRole.Role is not null && !userRole.Role.IsDeleted)
            .ToArray();
        var isCourseStudent = productRoles.Any(userRole =>
            string.Equals(userRole.Role!.Name, "CourseStudent", StringComparison.OrdinalIgnoreCase));
        var isSystemAdministrator = productRoles.Any(userRole =>
            string.Equals(userRole.Role!.Name, "SystemAdmin", StringComparison.OrdinalIgnoreCase));
        var hasStaffAccess = StaffProductAccessPolicy.GetStaffRoles(user, PlatformProduct.CourseManagement).Count > 0;

        if ((!user.HasProductAccess(PlatformProduct.CourseManagement) && !isSystemAdministrator)
            || (!isCourseStudent && !hasStaffAccess && !isSystemAdministrator))
        {
            return Result.Failure<LoginResponse>(new Error(
                "Auth.ProductAccessDenied",
                "Bu platform için hesabınıza erişim yetkisi tanımlı değil."));
        }

        // Same rule as sign-in: any account with an enrolled authenticator.
        if (user.MfaEnabled
            && request.MfaVerifiedAt is null
            && await IsPrivilegedMfaRequiredAsync(cancellationToken))
        {
            return Result.Failure<LoginResponse>(new Error(
                "Auth.MfaRequired",
                "Bu platforma geçmek için yeniden giriş yapıp iki adımlı doğrulamayı tamamlayın."));
        }

        return await sessionIssuer.IssueAsync(
            user,
            rememberMe: false,
            request.IpAddress,
            request.MfaVerifiedAt,
            cancellationToken,
            PlatformProduct.CourseManagement);
    }

    private async Task<bool> IsPrivilegedMfaRequiredAsync(CancellationToken cancellationToken)
    {
        try
        {
            var mode = await configurationService.GetConfigurationValueAsync(
                MfaOperationCategories.ConfigurationKey(MfaOperationCategories.System),
                cancellationToken);
            return !string.Equals(mode, MfaPolicyModes.Disabled, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Course Management session MFA policy could not be read; keeping MFA required.");
            return true;
        }
    }
}
