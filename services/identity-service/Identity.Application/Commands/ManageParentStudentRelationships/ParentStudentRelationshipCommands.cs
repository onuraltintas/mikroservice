using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MediatR;
using IdentityUserRole = Identity.Domain.Enums.UserRole;

namespace Identity.Application.Commands.ManageParentStudentRelationships;

public sealed record RequestParentStudentRelationshipCommand(
    Guid ParentUserId,
    Guid StudentUserId,
    ParentRelationship Relationship) : IRequest<Result<Guid>>;

public sealed record VerifyParentStudentRelationshipCommand(Guid RelationshipId) : IRequest<Result>;

public sealed record RevokeParentStudentRelationshipCommand(
    Guid RelationshipId,
    string Reason) : IRequest<Result>;

public sealed class RequestParentStudentRelationshipCommandHandler(
    IParentStudentRelationshipRepository relationshipRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<RequestParentStudentRelationshipCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        RequestParentStudentRelationshipCommand request,
        CancellationToken cancellationToken)
    {
        var actorResult = ParentStudentRelationshipManagement.GetSystemAdministrator(currentUser);
        if (actorResult.IsFailure)
            return Result.Failure<Guid>(actorResult.Error);

        if (request.ParentUserId == Guid.Empty || request.StudentUserId == Guid.Empty)
            return Result.Failure<Guid>(ParentStudentRelationshipErrors.InvalidSubjects);

        var parent = await userRepository.GetByIdAsync(request.ParentUserId, cancellationToken);
        var student = await userRepository.GetByIdAsync(request.StudentUserId, cancellationToken);
        if (!ParentStudentRelationshipManagement.IsActiveUserInRole(parent, IdentityUserRole.Parent)
            || !ParentStudentRelationshipManagement.IsActiveUserInRole(student, IdentityUserRole.Student))
        {
            return Result.Failure<Guid>(ParentStudentRelationshipErrors.InvalidSubjects);
        }

        if (await relationshipRepository.HasActiveAsync(
                request.ParentUserId,
                request.StudentUserId,
                cancellationToken))
        {
            return Result.Failure<Guid>(ParentStudentRelationshipErrors.AlreadyActive);
        }

        ParentStudentRelationship relationship;
        try
        {
            relationship = ParentStudentRelationship.Request(
                request.ParentUserId,
                request.StudentUserId,
                request.Relationship,
                actorResult.Value,
                timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (ArgumentException)
        {
            return Result.Failure<Guid>(ParentStudentRelationshipErrors.InvalidRequest);
        }

        await relationshipRepository.AddAsync(relationship, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(relationship.Id);
    }
}

public sealed class VerifyParentStudentRelationshipCommandHandler(
    IParentStudentRelationshipRepository relationshipRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<VerifyParentStudentRelationshipCommand, Result>
{
    public async Task<Result> Handle(
        VerifyParentStudentRelationshipCommand request,
        CancellationToken cancellationToken)
    {
        var actorResult = ParentStudentRelationshipManagement.GetSystemAdministrator(currentUser);
        if (actorResult.IsFailure)
            return Result.Failure(actorResult.Error);

        var relationship = await relationshipRepository.GetByIdAsync(request.RelationshipId, cancellationToken);
        if (relationship is null)
            return Result.Failure(ParentStudentRelationshipErrors.NotFound);
        if (relationship.Status != ParentStudentRelationshipStatus.Pending)
            return Result.Failure(ParentStudentRelationshipErrors.NotPending);

        relationship.Verify(
            ParentStudentVerificationMethod.ManualReview,
            actorResult.Value,
            timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class RevokeParentStudentRelationshipCommandHandler(
    IParentStudentRelationshipRepository relationshipRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<RevokeParentStudentRelationshipCommand, Result>
{
    public async Task<Result> Handle(
        RevokeParentStudentRelationshipCommand request,
        CancellationToken cancellationToken)
    {
        var actorResult = ParentStudentRelationshipManagement.GetSystemAdministrator(currentUser);
        if (actorResult.IsFailure)
            return Result.Failure(actorResult.Error);

        var relationship = await relationshipRepository.GetByIdAsync(request.RelationshipId, cancellationToken);
        if (relationship is null)
            return Result.Failure(ParentStudentRelationshipErrors.NotFound);
        if (relationship.Status == ParentStudentRelationshipStatus.Revoked)
            return Result.Failure(ParentStudentRelationshipErrors.AlreadyRevoked);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            return Result.Failure(ParentStudentRelationshipErrors.InvalidRevocationReason);

        relationship.Revoke(
            actorResult.Value,
            timeProvider.GetUtcNow().UtcDateTime,
            request.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal static class ParentStudentRelationshipManagement
{
    internal static Result<Guid> GetSystemAdministrator(ICurrentUserService currentUser)
    {
        if (!currentUser.IsAuthenticated
            || currentUser.UserId is not Guid actorId
            || actorId == Guid.Empty
            || !currentUser.Roles.Contains(IdentityUserRole.SystemAdmin.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure<Guid>(ParentStudentRelationshipErrors.Forbidden);
        }

        return Result.Success(actorId);
    }

    internal static bool IsActiveUserInRole(User? user, IdentityUserRole role) =>
        user is { IsActive: true }
        && user.Roles.Any(userRole =>
            userRole.Role.Name.Equals(role.ToString(), StringComparison.OrdinalIgnoreCase));
}

internal static class ParentStudentRelationshipErrors
{
    internal static readonly Error Forbidden = new(
        "ParentStudentRelationship.Forbidden",
        "Bu işlem yalnız yetkili sistem yöneticileri tarafından gerçekleştirilebilir.");
    internal static readonly Error InvalidSubjects = new(
        "ParentStudentRelationship.InvalidSubjects",
        "Aktif bir ebeveyn ve aktif bir öğrenci hesabı seçilmelidir.");
    internal static readonly Error AlreadyActive = new(
        "ParentStudentRelationship.AlreadyActive",
        "Bu ebeveyn ve öğrenci arasında zaten aktif bir ilişki bulunmaktadır.");
    internal static readonly Error InvalidRequest = new(
        "ParentStudentRelationship.InvalidRequest",
        "İlişki talebi geçerli değildir.");
    internal static readonly Error NotFound = new(
        "ParentStudentRelationship.NotFound",
        "Ebeveyn-öğrenci ilişkisi bulunamadı.");
    internal static readonly Error NotPending = new(
        "ParentStudentRelationship.NotPending",
        "Yalnız bekleyen bir ilişki doğrulanabilir.");
    internal static readonly Error AlreadyRevoked = new(
        "ParentStudentRelationship.AlreadyRevoked",
        "İlişki zaten iptal edilmiş.");
    internal static readonly Error InvalidRevocationReason = new(
        "ParentStudentRelationship.InvalidRevocationReason",
        "İptal gerekçesi zorunludur ve 500 karakteri aşamaz.");
}
