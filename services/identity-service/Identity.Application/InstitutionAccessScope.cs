using EduPlatform.Shared.Kernel.Results;
using Identity.Domain.Enums;

namespace Identity.Application.Authorization;

public sealed record InstitutionAccessScope(Guid? InstitutionId)
{
    public bool IsGlobal => InstitutionId is null;
}

public static class InstitutionAccessScopeResolver
{
    public static Result<InstitutionAccessScope> Resolve(
        Guid? userId,
        IEnumerable<string> roles,
        Guid? institutionId)
    {
        if (userId is null)
        {
            return Result.Failure<InstitutionAccessScope>(
                Error.Unauthorized("Oturum açılmış kullanıcı bulunamadı."));
        }

        if (roles.Any(role => string.Equals(
                role,
                UserRole.SystemAdmin.ToString(),
                StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Success(new InstitutionAccessScope(null));
        }

        return institutionId.HasValue
            ? Result.Success(new InstitutionAccessScope(institutionId.Value))
            : Result.Failure<InstitutionAccessScope>(
                Error.Forbidden("Kullanıcının erişebileceği aktif bir kurum bulunamadı."));
    }
}
