using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.JoinCourseManagement;

public sealed class JoinCourseManagementCommandHandler(
    IUserRepository users, IRoleRepository roles, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    : IRequestHandler<JoinCourseManagementCommand, Result>
{
    public async Task<Result> Handle(JoinCourseManagementCommand request, CancellationToken cancellationToken)
    {
        var roleName = request.Role switch
        {
            "Teacher" => "CourseTeacher",
            "InstitutionOwner" => "CourseInstitutionOwner",
            "Student" => "CourseStudent",
            _ => null
        };
        if (roleName is null) return Result.Failure(new Error("CourseJoin.InvalidRole", "Geçersiz kurs katılım türü."));
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            return Result.Failure(new Error("Auth.Unauthorized", "Giriş yapılmalıdır."));
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || !user.EmailConfirmed)
            return Result.Failure(new Error("CourseJoin.AccountNotEligible", "Aktif ve e-postası doğrulanmış bir hesap gereklidir."));
        if (user.HasProductAccess(PlatformProduct.CourseManagement) && user.Roles.Any(item =>
            item.Product == PlatformProduct.CourseManagement && item.Role is not null && !item.Role.IsDeleted && item.Role.Name == roleName))
            return Result.Success();
        var role = await roles.GetByNameAsync(roleName, cancellationToken);
        if (role is null)
        {
            // Course participation roles carry no shared administration permissions.
            role = Role.Create(roleName, "Kurs yönetimi ürün katılımı", isSystemRole: true);
            await roles.AddAsync(role, cancellationToken);
        }
        if (role.IsDeleted || role.Name != roleName || role.Permissions.Count != 0)
            return Result.Failure(new Error("CourseJoin.RoleNotAvailable", "Kurs katılım rolü kullanıma uygun değil."));
        user.AddRole(new Identity.Domain.Entities.UserRole(userId, role.Id, PlatformProduct.CourseManagement));
        if (user.GrantProductAccess(PlatformProduct.CourseManagement, UserProductAccessSource.SelfRegistration, userId, DateTimeOffset.UtcNow))
            users.TrackProductAccessIfNew(user, PlatformProduct.CourseManagement);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
