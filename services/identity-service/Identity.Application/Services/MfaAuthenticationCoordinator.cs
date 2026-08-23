using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Commands.Login;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;

namespace Identity.Application.Services;

public interface IAuthenticationSessionIssuer
{
    Task<Result<LoginResponse>> IssueAsync(
        User user,
        bool rememberMe,
        string ipAddress,
        DateTimeOffset? mfaVerifiedAt,
        CancellationToken cancellationToken);
}

public sealed record MfaCompletionResponse(
    LoginResponse Session,
    IReadOnlyList<string>? RecoveryCodes = null);

public sealed class MfaAuthenticationCoordinator
{
    private static readonly Error InvalidChallenge = new(
        "Auth.InvalidMfaChallenge",
        "MFA doğrulama isteği geçersiz veya süresi dolmuş.");
    private static readonly Error InvalidCode = new(
        "Auth.InvalidMfaCode",
        "MFA doğrulama kodu geçersiz.");

    private readonly IUserRepository _users;
    private readonly IMultiFactorService _multiFactor;
    private readonly IAuthenticationSessionIssuer _sessionIssuer;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public MfaAuthenticationCoordinator(
        IUserRepository users,
        IMultiFactorService multiFactor,
        IAuthenticationSessionIssuer sessionIssuer,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _users = users;
        _multiFactor = multiFactor;
        _sessionIssuer = sessionIssuer;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<MfaSetupResponse>> StartSetupAsync(
        string challengeToken,
        CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync(challengeToken, cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<MfaSetupResponse>(context.Error);
        }

        if (context.Value.User.MfaEnabled)
        {
            return Result.Failure<MfaSetupResponse>(new Error("Auth.MfaAlreadyEnabled", "MFA zaten etkin."));
        }

        return Result.Success(_multiFactor.CreateSetup(context.Value.User.Id, context.Value.User.Email));
    }

    public async Task<Result<MfaSetupResponse>> StartAuthenticatedSetupAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure<MfaSetupResponse>(InvalidChallenge);
        }

        if (user.MfaEnabled)
        {
            return Result.Failure<MfaSetupResponse>(new Error("Auth.MfaAlreadyEnabled", "MFA zaten etkin."));
        }

        var challengeToken = _multiFactor.CreateChallenge(user.Id, rememberMe: true);
        var setup = _multiFactor.CreateSetup(user.Id, user.Email);
        return Result.Success(setup with { ChallengeToken = challengeToken });
    }

    public async Task<Result<MfaCompletionResponse>> EnableAsync(
        string challengeToken,
        string setupToken,
        string code,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync(challengeToken, cancellationToken);
        var setup = _multiFactor.ReadSetupToken(setupToken);
        if (context.IsFailure || setup.IsFailure || setup.Value.UserId != context.Value.User.Id)
        {
            return Result.Failure<MfaCompletionResponse>(InvalidChallenge);
        }

        var user = context.Value.User;
        if (user.MfaEnabled)
        {
            return Result.Failure<MfaCompletionResponse>(new Error("Auth.MfaAlreadyEnabled", "MFA zaten etkin."));
        }

        var protectedSecret = _multiFactor.ProtectSecret(setup.Value.Secret);
        var timeStep = _multiFactor.FindMatchingTimeStep(protectedSecret, code);
        if (!timeStep.HasValue)
        {
            return Result.Failure<MfaCompletionResponse>(InvalidCode);
        }

        var recoveryCodes = _multiFactor.GenerateRecoveryCodes();
        user.EnableMfa(
            protectedSecret,
            recoveryCodes.Select(_multiFactor.HashRecoveryCode),
            _timeProvider.GetUtcNow());
        if (!user.TryAcceptMfaTimeStep(timeStep.Value))
        {
            return Result.Failure<MfaCompletionResponse>(InvalidCode);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var session = await _sessionIssuer.IssueAsync(
            user,
            context.Value.Challenge.RememberMe,
            ipAddress,
            _timeProvider.GetUtcNow(),
            cancellationToken);
        return session.IsSuccess
            ? Result.Success(new MfaCompletionResponse(session.Value, recoveryCodes))
            : Result.Failure<MfaCompletionResponse>(session.Error);
    }

    public async Task<Result<MfaCompletionResponse>> VerifyAsync(
        string challengeToken,
        string? code,
        string? recoveryCode,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync(challengeToken, cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<MfaCompletionResponse>(context.Error);
        }

        var user = context.Value.User;
        var now = _timeProvider.GetUtcNow();
        if (!user.MfaEnabled || string.IsNullOrWhiteSpace(user.MfaSecretProtected))
        {
            return Result.Failure<MfaCompletionResponse>(InvalidChallenge);
        }

        if (user.IsMfaVerificationLocked(now))
        {
            return Result.Failure<MfaCompletionResponse>(new Error("Auth.MfaLocked", "Çok fazla hatalı deneme yapıldı. Lütfen daha sonra tekrar deneyin."));
        }

        var verified = false;
        if (!string.IsNullOrWhiteSpace(code))
        {
            var timeStep = _multiFactor.FindMatchingTimeStep(user.MfaSecretProtected, code);
            verified = timeStep.HasValue && user.TryAcceptMfaTimeStep(timeStep.Value);
        }
        else if (!string.IsNullOrWhiteSpace(recoveryCode))
        {
            verified = user.ConsumeMfaRecoveryCode(_multiFactor.HashRecoveryCode(recoveryCode));
        }

        if (!verified)
        {
            user.RecordFailedMfaAttempt(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<MfaCompletionResponse>(InvalidCode);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var session = await _sessionIssuer.IssueAsync(
            user,
            context.Value.Challenge.RememberMe,
            ipAddress,
            now,
            cancellationToken);
        return session.IsSuccess
            ? Result.Success(new MfaCompletionResponse(session.Value))
            : Result.Failure<MfaCompletionResponse>(session.Error);
    }

    private async Task<Result<MfaContext>> LoadContextAsync(
        string challengeToken,
        CancellationToken cancellationToken)
    {
        var challenge = _multiFactor.ReadChallenge(challengeToken);
        if (challenge.IsFailure)
        {
            return Result.Failure<MfaContext>(challenge.Error);
        }

        var user = await _users.GetByIdAsync(challenge.Value.UserId, cancellationToken);
        return user is not null && user.IsActive
            ? Result.Success(new MfaContext(user, challenge.Value))
            : Result.Failure<MfaContext>(InvalidChallenge);
    }

    private sealed record MfaContext(User User, MfaChallengePayload Challenge);
}
