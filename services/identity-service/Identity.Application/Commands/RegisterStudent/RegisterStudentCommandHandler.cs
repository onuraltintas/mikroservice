using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;
using MassTransit;
using EduPlatform.Shared.Contracts.Events.Identity;

namespace Identity.Application.Commands.RegisterStudent;

public class RegisterStudentCommandHandler : IRequestHandler<RegisterStudentCommand, Result<Guid>>
{
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;

    public RegisterStudentCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
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
            
                    // Reactivate in System (This will enable user AND set emailVerified=true)
                    await _identityService.ActivateUserAsync(existingUser.Id, cancellationToken);
            
                    return Result.Success(existingUser.Id);
                }
            }

            return Result.Failure<Guid>(identityResult.Error);
        }

        var userId = identityResult.Value;

        // Assign Role
        var roleResult = await _identityService.AssignRoleAsync(userId, Identity.Domain.Enums.UserRole.Student.ToString(), cancellationToken);
        if (roleResult.IsFailure)
        {
             await _identityService.DeleteUserAsync(userId, cancellationToken);
             return Result.Failure<Guid>(roleResult.Error);
        }

        // Create Student Profile
        var student = StudentProfile.Create(userId, request.FirstName, request.LastName, null, null);

        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user != null) 
            {
                if (request.Phone != null) user.SetPhoneNumber(request.Phone);
                user.GenerateEmailVerificationToken();
            }

            // await _userRepository.AddAsync(user, cancellationToken); // REMOVED: User already exists
            await _studentRepository.AddAsync(student, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Publish Event for Notification Service (Verification)
            await _publishEndpoint.Publish(new UserRegisteredEvent(
                userId,
                request.Email,
                request.FirstName,
                request.LastName,
                user?.EmailVerificationToken ?? ""
            ), cancellationToken);

            return Result.Success(student.Id);
        }
        catch (Exception ex)
        {
            await _identityService.DeleteUserAsync(userId, cancellationToken);
            return Result.Failure<Guid>(new Error("Registration.Failed", $"Database error: {ex.Message}"));
        }
    }
}
