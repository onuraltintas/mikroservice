using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

public sealed class UserAccessManagementService : IUserAccessManagementService
{
    private readonly IdentityDbContext _context;

    public UserAccessManagementService(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Result> SetProductAccessAsync(
        Guid userId,
        PlatformProduct product,
        bool isActive,
        Guid grantedByUserId,
        string? revokedByIp,
        CancellationToken cancellationToken) => await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        if (product is not (PlatformProduct.Coaching or PlatformProduct.SpeedReading))
            return Result.Failure(new Error("Auth.InvalidProduct", "Koçluk veya Hızlı Okuma servisi seçilmelidir."));

        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken) : null;
        if (transaction is not null)
        {
            // Serialize first grants and opposing updates; retry attempts must reload state.
            _context.ChangeTracker.Clear();
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM identity.users WHERE \"Id\" = {userId} FOR UPDATE", cancellationToken);
        }

        var user = await _context.Users
            .Include(candidate => candidate.ProductAccesses)
            .Include(candidate => candidate.Roles).ThenInclude(assignment => assignment.Role)
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
            return Result.Failure(new Error("User.NotFound", "Kullanıcı bulunamadı."));
        if (user.Roles.Any(assignment => assignment.Product is null && assignment.Role.Name == "SystemAdmin"))
            return Result.Failure(new Error("User.GlobalAdministrator", "SystemAdmin genel yetkisi servis erişimiyle kısıtlanamaz."));

        // Keep an explicit inactive row even when revoking a previously absent access.
        // Role assignment must not silently reopen an access closed by an administrator.
        var access = user.ProductAccesses.SingleOrDefault(candidate => candidate.Product == product);
        if (isActive || access is null)
        {
            if (user.GrantProductAccess(product, UserProductAccessSource.Admin, grantedByUserId, DateTimeOffset.UtcNow))
            {
                access = user.ProductAccesses.Single(candidate => candidate.Product == product);
                _context.Entry(access).State = EntityState.Added;
            }
        }

        if (!isActive)
        {
            user.RevokeProductAccess(product, DateTimeOffset.UtcNow);
            var now = DateTime.UtcNow;
            var sessions = await _context.RefreshTokens
                .Where(token => token.UserId == userId && token.Product == product
                    && token.RevokedAt == null && token.ExpiresAt > now)
                .ToListAsync(cancellationToken);
            foreach (var session in sessions)
                session.Revoke(revokedByIp, "Administrator revoked product access");
        }

        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    });

    public async Task<Result<IReadOnlyList<UserSessionDto>>> GetActiveSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await _context.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<UserSessionDto>>(
                new Error("User.NotFound", "Kullanıcı bulunamadı."));
        }

        var now = DateTime.UtcNow;
        var sessions = await _context.RefreshTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId
                && token.RevokedAt == null
                && token.ExpiresAt > now)
            .OrderByDescending(token => token.CreatedAt)
            .Select(token => new UserSessionDto(
                token.Id,
                token.CreatedAt,
                token.ExpiresAt,
                token.CreatedByIp,
                token.IsPersistent,
                token.MfaVerifiedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<UserSessionDto>>(sessions);
    }

    public async Task<Result> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        string? revokedByIp,
        CancellationToken cancellationToken)
    {
        var session = await _context.RefreshTokens
            .FirstOrDefaultAsync(
                token => token.Id == sessionId && token.UserId == userId,
                cancellationToken);
        if (session is null)
        {
            return Result.Failure(new Error("Session.NotFound", "Aktif oturum bulunamadı."));
        }

        if (!session.IsActive)
        {
            return Result.Failure(new Error("Session.NotActive", "Oturum zaten kapatılmış veya süresi dolmuş."));
        }

        session.Revoke(revokedByIp, "Administrator terminated the session");
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RevokeAllSessionsAsync(
        Guid userId,
        string? revokedByIp,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!await _context.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return Result.Failure(new Error("User.NotFound", "Kullanıcı bulunamadı."));
        }

        var now = DateTime.UtcNow;
        var sessions = await _context.RefreshTokens
            .Where(token => token.UserId == userId
                && token.RevokedAt == null
                && token.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(revokedByIp, reason);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ResetMfaAsync(
        Guid userId,
        string? revokedByIp,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(new Error("User.NotFound", "Kullanıcı bulunamadı."));
        }

        user.ResetMfa();

        var now = DateTime.UtcNow;
        var sessions = await _context.RefreshTokens
            .Where(token => token.UserId == userId
                && token.RevokedAt == null
                && token.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(revokedByIp, "MFA reset by administrator");
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
