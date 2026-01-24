using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;

namespace Identity.Application.Commands.RegisterStudent;

public class RegisterStudentCommandHandler : IRequestHandler<RegisterStudentCommand, Result<Guid>>
{
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterStudentCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        IUnitOfWork unitOfWork)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(RegisterStudentCommand request, CancellationToken cancellationToken)
    {
        var identityResult = await _identityService.RegisterUserAsync(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            cancellationToken);

        if (identityResult.IsFailure)
        {
            if (identityResult.Error.Code == "User.Duplicate")
            {
                // Check local DB if user exists but inactive (Soft Deleted)
                var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        
                if (existingUser != null && !existingUser.IsActive)
                {
                    // Reactivate Case
                    existingUser.Activate();
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
            
                    // Reactivate in Keycloak (This will enable user AND set emailVerified=true)
                    await _identityService.ActivateUserAsync(existingUser.Id, cancellationToken);
            
                    return Result.Success(existingUser.Id);
                }
            }

            return Result.Failure<Guid>(identityResult.Error);
        }

        var userId = identityResult.Value;

        var user = User.Create(userId, request.Email);
        if (request.Phone != null) user.SetPhoneNumber(request.Phone);
        
        user.AddRole(new UserRole(userId, Identity.Domain.Enums.UserRole.Student));

        var student = StudentProfile.Create(userId, request.FirstName, request.LastName, null, null);

        try
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _studentRepository.AddAsync(student, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(student.Id);
        }
        catch (Exception ex)
        {
            await _identityService.DeleteUserAsync(userId, cancellationToken);
            return Result.Failure<Guid>(new Error("Registration.Failed", $"Database error: {ex.Message}"));
        }
    }
}
