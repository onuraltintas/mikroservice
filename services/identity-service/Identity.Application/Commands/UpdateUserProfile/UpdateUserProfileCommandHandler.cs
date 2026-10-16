using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.UpdateUserProfile;

public class UpdateUserProfileCommandHandler : IRequestHandler<UpdateUserProfileCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateUserProfileCommandHandler(
        IUserRepository userRepository,
        ITeacherRepository teacherRepository,
        IStudentRepository studentRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId == null)
        {
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated"));
        }

        var userId = _currentUserService.UserId.Value;
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        
        if (user == null)
        {
            return Result.Failure(new Error("User.NotFound", "User not found"));
        }

        if (request.Product is null or PlatformProduct.Coaching &&
            (request.Bio is not null || request.AvatarUrl is not null || request.Title is not null || request.Subjects is not null
             || request.GradeLevel.HasValue || request.BirthDate.HasValue || request.LearningStyle.HasValue || request.ShareProgressWithTeachers.HasValue))
            return Result.Failure(new Error("Profiles.OwnedByCoaching", "Eğitim profilini Koçluk profil ekranından güncelleyin."));

        // 1. Update Core User Info
        if (!string.IsNullOrWhiteSpace(request.FirstName) || !string.IsNullOrWhiteSpace(request.LastName))
        {
            user.UpdateName(
                request.FirstName ?? user.FirstName, 
                request.LastName ?? user.LastName
            );
        }

        if (request.PhoneNumber != null)
        {
            user.SetPhoneNumber(request.PhoneNumber);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
