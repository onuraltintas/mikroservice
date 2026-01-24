using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;

using MassTransit;

namespace Identity.Application.Commands.CreateTeacher;

public class CreateTeacherCommandHandler : IRequestHandler<CreateTeacherCommand, Result<CreateTeacherResult>>
{
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IInstitutionRepository _institutionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublishEndpoint _publishEndpoint;

    public CreateTeacherCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        ITeacherRepository teacherRepository,
        IInstitutionRepository institutionRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPublishEndpoint publishEndpoint)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _teacherRepository = teacherRepository;
        _institutionRepository = institutionRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Result<CreateTeacherResult>> Handle(CreateTeacherCommand request, CancellationToken cancellationToken)
    {
        // ... (validation)
        // 1. Validate: Current user must be logged in
        if (_currentUserService.UserId == null)
        {
            return Result.Failure<CreateTeacherResult>(new Error("Auth.Unauthorized", "User is not authenticated"));
        }

        var adminUserId = _currentUserService.UserId.Value;

        // 2. Find Institution for this admin
        var institutionId = await _institutionRepository.GetInstitutionIdByAdminIdAsync(adminUserId, cancellationToken);
        if (institutionId == null)
        {
             return Result.Failure<CreateTeacherResult>(new Error("CreateTeacher.Forbidden", "You are not an admin of any institution."));
        }

        // 3. Create user with temporary password (user must change on first login)
        var identityResult = await _identityService.RegisterUserWithTemporaryPasswordAsync(
            request.Email,
            request.FirstName,
            request.LastName,
            cancellationToken);

        if (identityResult.IsFailure) return Result.Failure<CreateTeacherResult>(identityResult.Error);
        
        var (teacherUserId, temporaryPassword) = identityResult.Value;

        var user = User.Create(teacherUserId, request.Email, request.FirstName, request.LastName);
        user.AddRole(new UserRole(teacherUserId, Identity.Domain.Enums.UserRole.Teacher));

        // Create Profile (Not Independent)
        var teacher = TeacherProfile.Create(teacherUserId, request.FirstName, request.LastName, null, isIndependent: false);
        
        if (request.Title != null) teacher.UpdatePersonalInfo(title: request.Title);
        if (request.Subjects.Length > 0) teacher.SetSubjects(request.Subjects);

        // Assign to Institution
        teacher.AssignToInstitution(institutionId.Value);

        try
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _teacherRepository.AddAsync(teacher, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            // Publish UserCreated Event
            var eventMessage = new EduPlatform.Shared.Contracts.Events.Identity.UserCreatedEvent(
                UserId: teacherUserId,
                Email: request.Email,
                FirstName: request.FirstName,
                LastName: request.LastName,
                Role: "Teacher",
                TemporaryPassword: temporaryPassword,
                CreatedAt: DateTime.UtcNow
            );

            await _publishEndpoint.Publish(eventMessage, cancellationToken);

            // Return both TeacherId and TemporaryPassword so institution can share it
            return Result.Success(new CreateTeacherResult(teacher.Id, temporaryPassword));
        }
        catch (Exception ex)
        {
            await _identityService.DeleteUserAsync(teacherUserId, cancellationToken);
            return Result.Failure<CreateTeacherResult>(new Error("CreateTeacher.Failed", ex.Message));
        }
    }
}
