using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;

namespace Identity.Application.Commands.RegisterTeacher;

public class RegisterTeacherCommandHandler : IRequestHandler<RegisterTeacherCommand, Result<Guid>>
{
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterTeacherCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        ITeacherRepository teacherRepository,
        IUnitOfWork unitOfWork)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _teacherRepository = teacherRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(RegisterTeacherCommand request, CancellationToken cancellationToken)
    {
        var identityResult = await _identityService.RegisterUserAsync(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            cancellationToken);

        if (identityResult.IsFailure)
        {
            return Result.Failure<Guid>(identityResult.Error);
        }

        var userId = identityResult.Value;

        var user = User.Create(userId, request.Email);
        if (request.Phone != null) user.SetPhoneNumber(request.Phone);
        
        user.AddRole(new UserRole(userId, Identity.Domain.Enums.UserRole.Teacher));

        // Independent Teacher
        var teacher = TeacherProfile.Create(userId, request.FirstName, request.LastName, null, true);

        try
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _teacherRepository.AddAsync(teacher, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(teacher.Id);
        }
        catch (Exception ex)
        {
            await _identityService.DeleteUserAsync(userId, cancellationToken);
            return Result.Failure<Guid>(new Error("Registration.Failed", $"Database error: {ex.Message}"));
        }
    }
}
