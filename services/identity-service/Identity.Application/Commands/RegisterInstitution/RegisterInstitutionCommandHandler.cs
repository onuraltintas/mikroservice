using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.RegisterInstitution;

public class RegisterInstitutionCommandHandler : IRequestHandler<RegisterInstitutionCommand, Result<Guid>>
{
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly IInstitutionRepository _institutionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterInstitutionCommandHandler(
        IIdentityService identityService,
        IUserRepository userRepository,
        IInstitutionRepository institutionRepository,
        IUnitOfWork unitOfWork)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _institutionRepository = institutionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(RegisterInstitutionCommand request, CancellationToken cancellationToken)
    {
        // 1. Create User in Keycloak
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

        // 2. Create Domain Entities
        var user = User.Create(userId, request.Email, request.FirstName, request.LastName);
        if (request.Phone != null) user.SetPhoneNumber(request.Phone);
        
        user.AddRole(new Identity.Domain.Entities.UserRole(userId, Identity.Domain.Enums.UserRole.InstitutionAdmin));
        user.AddRole(new Identity.Domain.Entities.UserRole(userId, Identity.Domain.Enums.UserRole.InstitutionOwner));

        var institution = Institution.Create(
            request.InstitutionName,
            request.InstitutionType,
            request.City,
            request.Email);

        var admin = InstitutionAdmin.Create(
            userId,
            institution.Id,
            InstitutionAdminRole.Owner);

        // 3. Save to Database (Transactional)
        try
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _institutionRepository.AddAsync(institution, cancellationToken);
            await _institutionRepository.AddAdminAsync(admin, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(institution.Id);
        }
        catch (Exception ex)
        {
            // 5. Compensating Transaction: Delete User from Keycloak
            await _identityService.DeleteUserAsync(userId, cancellationToken);
            
            return Result.Failure<Guid>(new Error("Registration.Failed", $"Database error: {ex.Message}"));
        }
    }
}
