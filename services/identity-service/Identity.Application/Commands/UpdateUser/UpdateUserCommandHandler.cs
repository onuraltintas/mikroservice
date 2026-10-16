using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using MediatR;

namespace Identity.Application.Commands.UpdateUser;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInstitutionRepository _institutionRepository;

    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IStudentRepository studentRepository,
        ITeacherRepository teacherRepository,
        IInstitutionRepository institutionRepository)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _institutionRepository = institutionRepository;
    }

    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        
        if (user == null)
        {
            return Result.Failure(new Error("User.NotFound", "User not found"));
        }

        if (request.UpdateRoleProfile && request.Product is null or Identity.Domain.Enums.PlatformProduct.Coaching)
            return Result.Failure(new Error("Profiles.OwnedByCoaching", "Eğitim ve kurum ilişkilerini Koçluk panelinden güncelleyin."));

        // 1. Update Database
        user.UpdateName(request.FirstName, request.LastName);
        if (request.PhoneNumber != null)
        {
            user.SetPhoneNumber(request.PhoneNumber);
        }

        // 2. Save Changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
