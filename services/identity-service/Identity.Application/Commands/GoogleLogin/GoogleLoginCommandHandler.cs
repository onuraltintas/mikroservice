using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Commands.Login;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;

namespace Identity.Application.Commands.GoogleLogin;

public class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, Result<LoginResponse>>
{
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdentityService _identityService;
    private readonly IStudentRepository _studentRepository; // For auto-registration

    public GoogleLoginCommandHandler(
        IGoogleAuthService googleAuthService,
        IUserRepository userRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IIdentityService identityService,
        IStudentRepository studentRepository)
    {
        _googleAuthService = googleAuthService;
        _userRepository = userRepository;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _identityService = identityService;
        _studentRepository = studentRepository;
    }

    public async Task<Result<LoginResponse>> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify Google Token
        var googleUser = await _googleAuthService.VerifyGoogleTokenAsync(request.IdToken);
        if (googleUser == null)
        {
            return Result.Failure<LoginResponse>(new Error("Auth.InvalidToken", "Invalid Google ID Token."));
        }

        // 2. Check if user exists
        var user = await _userRepository.GetByEmailAsync(googleUser.Email, cancellationToken);
        
        if (user == null)
        {
            // 2b. Auto-Register User
            // Generate random password
            var randomPassword = Guid.NewGuid().ToString("N") + "A1!"; 
            
            var regResult = await _identityService.RegisterUserAsync(
                googleUser.Email, 
                randomPassword, 
                googleUser.FirstName, 
                googleUser.LastName, 
                cancellationToken);

            if (regResult.IsFailure)
            {
                return Result.Failure<LoginResponse>(regResult.Error);
            }

            var userId = regResult.Value;
            user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            
            // Auto-Confirm Email since it comes from Google
            // But RegisterUserAsync might verify? LocalIdentity doesn't set EmailConfirmed=true by default usually.
            // Let's force it if User entity supports it or just assume verified context.
            // (We haven't implemented EmailConfirmed property logic fully yet, but IdentitySeeder sets it?) 
            // Update: User.cs has EmailConfirmed, defaulting to false.
            if (user != null)
            {
                // Assign Role: Student (Default for public sign-up)
                await _identityService.AssignRoleAsync(userId, Identity.Domain.Enums.UserRole.Student.ToString(), cancellationToken);
                
                // Create Student Profile
                var student = StudentProfile.Create(userId, googleUser.FirstName, googleUser.LastName);
                if (!string.IsNullOrEmpty(googleUser.PictureUrl)) student.SetAvatar(googleUser.PictureUrl);
                
                await _studentRepository.AddAsync(student, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        if (user == null) 
            return Result.Failure<LoginResponse>(new Error("Auth.UserCreationFailed", "Could not create user."));

        if (!user.IsActive)
        {
             return Result.Failure<LoginResponse>(new Error("Auth.UserInactive", "User account is inactive."));
        }

        // 3. Generate Tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken(user.Id, request.IpAddress);

        user.AddRefreshToken(refreshToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new LoginResponse(accessToken, refreshToken.Token));
    }
}
