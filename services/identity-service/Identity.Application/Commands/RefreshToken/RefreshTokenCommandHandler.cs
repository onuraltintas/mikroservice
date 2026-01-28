using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;
using System.Security.Claims;

namespace Identity.Application.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IUserRepository userRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RefreshTokenResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate Refresh Token Logic (Needs DB access)
        // Since we don't have a direct method to get token, we find user by token OR need a method in repo.
        // Let's assume we fetch the user who HAS this token.
        // For efficiency, we should have GetByRefreshToken in Repository, but for now we might iterate or query.
        // EF Core can query: _context.Users.Include(u => u.RefreshTokens).SingleOrDefault(u => u.RefreshTokens.Any(t => t.Token == request.RefreshToken));
        
        // Let's add GetByRefreshTokenAsync to IUserRepository to be clean.
        // For now, I'll simulate it or access via repo if I update the interface.

        var user = await _userRepository.GetByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        
        if (user == null)
            return Result.Failure<RefreshTokenResponse>(new Error("Auth.InvalidToken", "Geçersiz oturum anahtarı."));

        var existingRefreshToken = user.RefreshTokens.SingleOrDefault(r => r.Token == request.RefreshToken);

        if (existingRefreshToken == null || !existingRefreshToken.IsActive)
             return Result.Failure<RefreshTokenResponse>(new Error("Auth.InvalidToken", "Oturum süresi dolmuş veya geçersiz."));

        // 2. Revoke Old
        existingRefreshToken.Revoke("0.0.0.0", "Refreshed");

        // 3. Generate New
        var newAccessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken(user.Id, "0.0.0.0");
        
        user.AddRefreshToken(newRefreshToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RefreshTokenResponse(newAccessToken, newRefreshToken.Token));
    }
}
