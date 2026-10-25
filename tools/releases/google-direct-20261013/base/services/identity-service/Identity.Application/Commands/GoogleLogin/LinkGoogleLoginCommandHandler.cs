using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Commands.GoogleLogin;

public sealed class LinkGoogleLoginCommandHandler : IRequestHandler<LinkGoogleLoginCommand, Result>
{
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<LinkGoogleLoginCommandHandler> _logger;

    public LinkGoogleLoginCommandHandler(
        IGoogleAuthService googleAuthService,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        ILogger<LinkGoogleLoginCommandHandler> logger)
    {
        _googleAuthService = googleAuthService;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result> Handle(LinkGoogleLoginCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId is not Guid currentUserId)
        {
            return Result.Failure(new Error("Auth.Unauthorized", "Oturum açılmamış."));
        }

        var googleUser = await _googleAuthService.VerifyGoogleTokenAsync(request.IdToken);
        if (googleUser is null || !GoogleAuthenticationRules.IsVerifiedGoogleUser(googleUser))
        {
            _logger.LogWarning("Google account link failed security validation.");
            return Result.Failure(new Error("Auth.InvalidToken", "Invalid Google ID Token."));
        }

        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(new Error("User.NotFound", "Kullanıcı bulunamadı."));
        }

        if (!user.IsActive)
        {
            return Result.Failure(new Error("Auth.UserInactive", "User account is inactive."));
        }

        if (!string.Equals(user.Email, googleUser.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(new Error(
                "Auth.ExternalLoginEmailMismatch",
                "Google hesabının e-posta adresi mevcut hesapla eşleşmiyor."));
        }

        var linkedUser = await _userRepository.GetByLoginAsync(
            GoogleAuthenticationRules.LoginProvider,
            googleUser.GoogleId,
            cancellationToken);
        if (linkedUser is not null && linkedUser.Id != user.Id)
        {
            return Result.Failure(new Error(
                "Auth.ExternalLoginAlreadyLinked",
                "Google hesabı başka bir kullanıcıya bağlıdır."));
        }

        if (linkedUser is null)
        {
            var loginAdded = await _userRepository.TryAddLoginAsync(user, UserLogin.Create(
                user.Id,
                GoogleAuthenticationRules.LoginProvider,
                googleUser.GoogleId,
                GoogleAuthenticationRules.LoginProvider),
                cancellationToken);
            if (!loginAdded)
            {
                return Result.Failure(new Error(
                    "Auth.ExternalLoginAlreadyLinked",
                    "Google hesabı başka bir kullanıcıya bağlıdır."));
            }
        }

        return Result.Success();
    }
}
