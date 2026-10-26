using EduPlatform.Shared.Kernel.Results;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Services;

public static class ProductSessionContext
{
    public static Result<PlatformProduct?> Resolve(
        User user,
        PlatformProduct? requestedProduct,
        bool isSystemAdministrator)
    {
        if (requestedProduct is { } requested)
        {
            if (!Enum.IsDefined(requested))
            {
                return Result.Failure<PlatformProduct?>(new Error(
                    "Auth.ProductAccessDenied",
                    "Bu platforma erişim yetkiniz bulunmuyor."));
            }

            return isSystemAdministrator || user.ProductAccesses.Any(access =>
                    access.Product == requested && access.IsActive)
                ? Result.Success<PlatformProduct?>(requested)
                : Result.Failure<PlatformProduct?>(new Error(
                    "Auth.ProductAccessDenied",
                    "Bu platforma erişim yetkiniz bulunmuyor."));
        }

        if (isSystemAdministrator)
        {
            return Result.Success<PlatformProduct?>(null);
        }

        var activeProducts = user.ProductAccesses
            .Where(access => access.IsActive)
            .Select(access => access.Product)
            .Distinct()
            .ToArray();

        return activeProducts.Length switch
        {
            1 => Result.Success<PlatformProduct?>(activeProducts[0]),
            0 => Result.Failure<PlatformProduct?>(new Error(
                "Auth.ProductAccessDenied",
                "Bu hesap için etkin platform erişimi bulunmuyor.")),
            _ => Result.Failure<PlatformProduct?>(new Error(
                "Auth.ProductContextRequired",
                "Bu hesap birden fazla platformda kayıtlı. Lütfen ilgili platformun giriş sayfasını kullanın."))
        };
    }
}
