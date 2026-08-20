using EduPlatform.Shared.Kernel.Results;
using Identity.Application.DTOs.Institutions;
using Identity.Application.Authorization;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Identity.Application.Commands.ManageInstitutions;

public sealed record CreateInstitutionCommand(
    string Name,
    InstitutionType Type,
    string? City,
    string? Email) : IRequest<Result<Guid>>;

public sealed class CreateInstitutionCommandValidator : AbstractValidator<CreateInstitutionCommand>
{
    public CreateInstitutionCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.City).MaximumLength(100).When(command => command.City is not null);
        RuleFor(command => command.Email).EmailAddress().MaximumLength(255)
            .When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.Type).IsInEnum();
    }
}

public sealed record UpdateInstitutionCommand(
    Guid Id,
    string? Name,
    string? Address,
    string? City,
    string? District,
    string? Phone,
    string? Email,
    string? Website,
    LicenseType? LicenseType,
    int? MaxStudents,
    int? MaxTeachers,
    DateTime? SubscriptionEndDate) : IRequest<Result>;

public sealed class UpdateInstitutionCommandValidator : AbstractValidator<UpdateInstitutionCommand>
{
    public UpdateInstitutionCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name).MaximumLength(200).When(command => command.Name is not null);
        RuleFor(command => command.Address).MaximumLength(500).When(command => command.Address is not null);
        RuleFor(command => command.City).MaximumLength(100).When(command => command.City is not null);
        RuleFor(command => command.District).MaximumLength(100).When(command => command.District is not null);
        RuleFor(command => command.Phone).MaximumLength(50).When(command => command.Phone is not null);
        RuleFor(command => command.Email).EmailAddress().MaximumLength(255)
            .When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.Website).MaximumLength(500).When(command => command.Website is not null);
        RuleFor(command => command.LicenseType).IsInEnum().When(command => command.LicenseType.HasValue);
        RuleFor(command => command.MaxStudents).InclusiveBetween(1, 10_000).When(command => command.MaxStudents.HasValue);
        RuleFor(command => command.MaxTeachers).InclusiveBetween(1, 1_000).When(command => command.MaxTeachers.HasValue);
    }
}

public sealed record SetInstitutionActiveCommand(Guid Id, bool IsActive) : IRequest<Result>;

public sealed record AssignInstitutionAdminCommand(
    Guid InstitutionId,
    Guid UserId,
    InstitutionAdminRole Role) : IRequest<Result>;

public sealed record SetInstitutionAdminActiveCommand(
    Guid InstitutionId,
    Guid UserId,
    bool IsActive) : IRequest<Result>;

public sealed class CreateInstitutionCommandHandler : IRequestHandler<CreateInstitutionCommand, Result<Guid>>
{
    private readonly IInstitutionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InstitutionManagementAuthorization _authorization;

    public CreateInstitutionCommandHandler(
        IInstitutionRepository repository,
        IUnitOfWork unitOfWork,
        InstitutionManagementAuthorization authorization)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result<Guid>> Handle(CreateInstitutionCommand request, CancellationToken cancellationToken)
    {
        var access = _authorization.EnsureSystemAdministrator();
        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        var institution = Institution.Create(request.Name.Trim(), request.Type, request.City?.Trim(), request.Email?.Trim());
        await _repository.AddAsync(institution, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(institution.Id);
    }
}

public sealed class UpdateInstitutionCommandHandler : IRequestHandler<UpdateInstitutionCommand, Result>
{
    private readonly IInstitutionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InstitutionManagementAuthorization _authorization;

    public UpdateInstitutionCommandHandler(
        IInstitutionRepository repository,
        IUnitOfWork unitOfWork,
        InstitutionManagementAuthorization authorization)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result> Handle(UpdateInstitutionCommand request, CancellationToken cancellationToken)
    {
        var access = await _authorization.EnsureInstitutionAccessAsync(request.Id, cancellationToken);
        if (access.IsFailure)
        {
            return access;
        }

        if (!_authorization.IsSystemAdministrator
            && (request.LicenseType.HasValue
                || request.MaxStudents.HasValue
                || request.MaxTeachers.HasValue
                || request.SubscriptionEndDate.HasValue))
        {
            return Result.Failure(Error.Forbidden("Lisans ve kapasite ayarlarını yalnızca sistem yöneticisi değiştirebilir."));
        }

        var institution = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (institution is null)
        {
            return Result.Failure(new Error("Institution.NotFound", "Kurum bulunamadı."));
        }

        institution.UpdateInfo(request.Name?.Trim(), request.Address?.Trim(), request.City?.Trim(),
            request.District?.Trim(), request.Phone?.Trim(), request.Email?.Trim(), request.Website?.Trim());

        if (request.LicenseType.HasValue || request.MaxStudents.HasValue || request.MaxTeachers.HasValue || request.SubscriptionEndDate.HasValue)
        {
            institution.UpgradeLicense(
                request.LicenseType ?? institution.LicenseType,
                request.MaxStudents ?? institution.MaxStudents,
                request.MaxTeachers ?? institution.MaxTeachers,
                request.SubscriptionEndDate ?? institution.SubscriptionEndDate ?? DateTime.UtcNow.AddDays(14));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class SetInstitutionActiveCommandHandler : IRequestHandler<SetInstitutionActiveCommand, Result>
{
    private readonly IInstitutionRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InstitutionManagementAuthorization _authorization;

    public SetInstitutionActiveCommandHandler(
        IInstitutionRepository repository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        InstitutionManagementAuthorization authorization)
    {
        _repository = repository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result> Handle(SetInstitutionActiveCommand request, CancellationToken cancellationToken)
    {
        var access = _authorization.EnsureSystemAdministrator();
        if (access.IsFailure)
        {
            return access;
        }

        var institution = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (institution is null)
        {
            return Result.Failure(new Error("Institution.NotFound", "Kurum bulunamadı."));
        }

        if (request.IsActive) institution.Activate();
        else
        {
            institution.Deactivate();
            await _userRepository.RevokeActiveRefreshTokensForInstitutionAsync(
                request.Id,
                "security-sensitive institution deactivation",
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class AssignInstitutionAdminCommandHandler : IRequestHandler<AssignInstitutionAdminCommand, Result>
{
    private readonly IInstitutionRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InstitutionManagementAuthorization _authorization;

    public AssignInstitutionAdminCommandHandler(
        IInstitutionRepository repository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        InstitutionManagementAuthorization authorization)
    {
        _repository = repository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result> Handle(AssignInstitutionAdminCommand request, CancellationToken cancellationToken)
    {
        var access = await _authorization.EnsureInstitutionAccessAsync(request.InstitutionId, cancellationToken);
        if (access.IsFailure)
        {
            return access;
        }

        var institution = await _repository.GetByIdAsync(request.InstitutionId, cancellationToken);
        if (institution is null || !institution.IsActive)
        {
            return Result.Failure(new Error("Institution.NotFound", "Aktif kurum bulunamadı."));
        }

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure(new Error("User.NotFound", "Aktif kullanıcı bulunamadı."));
        }

        if (!_authorization.IsSystemAdministrator)
        {
            var userAccess = await _authorization.EnsureUserInCurrentInstitutionAsync(
                request.UserId,
                cancellationToken);
            if (userAccess.IsFailure)
            {
                return userAccess;
            }
        }

        var canManageInstitution = user.Roles.Any(role =>
            string.Equals(role.Role?.Name, Identity.Domain.Enums.UserRole.InstitutionAdmin.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(role.Role?.Name, Identity.Domain.Enums.UserRole.InstitutionOwner.ToString(), StringComparison.OrdinalIgnoreCase));
        if (!canManageInstitution)
        {
            return Result.Failure(new Error(
                "User.InvalidRole",
                "Kurum yöneticisi atanacak kullanıcı InstitutionAdmin veya InstitutionOwner rolüne sahip olmalıdır."));
        }

        var existingAdmin = await _repository.GetAdminAsync(
            request.InstitutionId,
            request.UserId,
            cancellationToken);
        if (existingAdmin?.IsActive == true)
        {
            return Result.Failure(new Error("InstitutionAdmin.Exists", "Kullanıcı zaten bu kurumun yöneticisi."));
        }

        if (existingAdmin is not null)
        {
            existingAdmin.ChangeRole(request.Role);
            existingAdmin.Activate();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        await _repository.AddAdminAsync(
            InstitutionAdmin.Create(request.UserId, request.InstitutionId, request.Role),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class SetInstitutionAdminActiveCommandHandler
    : IRequestHandler<SetInstitutionAdminActiveCommand, Result>
{
    private readonly IInstitutionRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InstitutionManagementAuthorization _authorization;

    public SetInstitutionAdminActiveCommandHandler(
        IInstitutionRepository repository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        InstitutionManagementAuthorization authorization)
    {
        _repository = repository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result> Handle(
        SetInstitutionAdminActiveCommand request,
        CancellationToken cancellationToken)
    {
        var access = await _authorization.EnsureInstitutionAccessAsync(request.InstitutionId, cancellationToken);
        if (access.IsFailure)
        {
            return access;
        }

        var admin = await _repository.GetAdminAsync(
            request.InstitutionId,
            request.UserId,
            cancellationToken);
        if (admin is null)
        {
            return Result.Failure(new Error("InstitutionAdmin.NotFound", "Kurum yöneticisi bulunamadı."));
        }

        if (request.IsActive) admin.Activate();
        else
        {
            admin.Deactivate();
            await _userRepository.RevokeActiveRefreshTokensAsync(
                request.UserId,
                "security-sensitive institution membership change",
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
