using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
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
