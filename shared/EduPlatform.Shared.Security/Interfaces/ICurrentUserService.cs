using System.Security.Claims;

namespace EduPlatform.Shared.Security.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? FullName { get; }
    IEnumerable<string> Roles { get; }
    bool IsAuthenticated { get; }
    ClaimsPrincipal? User { get; }
}
