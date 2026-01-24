using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;

using MassTransit;

namespace Identity.Application.Commands.CreateStudent;

public class CreateStudentCommandHandler : IRequestHandler<CreateStudentCommand, Result<CreateStudentResult>>
{
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IInstitutionRepository _institutionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublishEndpoint _publishEndpoint;

    public CreateStudentCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        IInstitutionRepository institutionRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPublishEndpoint publishEndpoint)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _institutionRepository = institutionRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Result<CreateStudentResult>> Handle(CreateStudentCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate: Current user must be logged in
        if (_currentUserService.UserId == null)
        {
            return Result.Failure<CreateStudentResult>(new Error("Auth.Unauthorized", "User is not authenticated"));
        }

        var adminUserId = _currentUserService.UserId.Value;

        // 2. Find Institution for this admin
        var institutionId = await _institutionRepository.GetInstitutionIdByAdminIdAsync(adminUserId, cancellationToken);
        if (institutionId == null)
        {
             return Result.Failure<CreateStudentResult>(new Error("CreateStudent.Forbidden", "You are not an admin of any institution."));
        }

        // 3. Create user with temporary password (user must change on first login)
        var identityResult = await _identityService.RegisterUserWithTemporaryPasswordAsync(
            request.Email,
            request.FirstName,
            request.LastName,
            cancellationToken);

        if (identityResult.IsFailure) return Result.Failure<CreateStudentResult>(identityResult.Error);
        
        var (studentUserId, temporaryPassword) = identityResult.Value;

        var user = User.Create(studentUserId, request.Email, request.FirstName, request.LastName);
        user.AddRole(new UserRole(studentUserId, Identity.Domain.Enums.UserRole.Student));

        // Create Student Profile attached to institution
        var student = StudentProfile.Create(
            studentUserId, 
            request.FirstName, 
            request.LastName, 
            institutionId.Value
        );
        
        student.UpdateEducationInfo(gradeLevel: request.GradeLevel);

        try
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _studentRepository.AddAsync(student, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            // Publish UserCreated Event
            var eventMessage = new EduPlatform.Shared.Contracts.Events.Identity.UserCreatedEvent(
                UserId: studentUserId,
                Email: request.Email,
                FirstName: request.FirstName,
                LastName: request.LastName,
                Role: "Student",
                TemporaryPassword: temporaryPassword,
                CreatedAt: DateTime.UtcNow
            );

            await _publishEndpoint.Publish(eventMessage, cancellationToken);

            // Return both StudentId and TemporaryPassword so institution can share it
            return Result.Success(new CreateStudentResult(student.Id, temporaryPassword));
        }
        catch (Exception ex)
        {
            await _identityService.DeleteUserAsync(studentUserId, cancellationToken);
            return Result.Failure<CreateStudentResult>(new Error("CreateStudent.Failed", ex.Message));
        }
    }
}
