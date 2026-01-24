using EduPlatform.Shared.Kernel.Results;

namespace Identity.Application.Interfaces;

public interface IIdentityService
{
    /// <summary>
    /// Keycloak üzerinde yeni bir kullanıcı oluşturur.
    /// </summary>
    /// <returns>Oluşturulan kullanıcının ID'si (Guid)</returns>
    Task<Result<Guid>> RegisterUserAsync(string email, string password, string firstName, string lastName, CancellationToken cancellationToken);

    /// <summary>
    /// Kullanıcıyı Keycloak'tan siler (Rollback senaryoları için).
    /// </summary>
    Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Kullanıcıyı pasife çeker (Login olamaz).
    /// </summary>
    Task<Result> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Kullanıcıyı aktifleştirir.
    /// </summary>
    Task<Result> ActivateUserAsync(Guid userId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Kullanıcıya rol atar
    /// </summary>
    Task<Result> AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken);

    /// <summary>
    /// Geçici şifre ile kullanıcı oluşturur. İlk girişte şifre değişikliği zorunludur.
    /// </summary>
    /// <returns>UserId ve Geçici Şifre</returns>
    Task<Result<(Guid UserId, string TemporaryPassword)>> RegisterUserWithTemporaryPasswordAsync(
        string email, 
        string firstName, 
        string lastName, 
        CancellationToken cancellationToken);
}
