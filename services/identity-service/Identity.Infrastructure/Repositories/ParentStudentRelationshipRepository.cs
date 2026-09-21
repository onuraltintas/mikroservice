using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public sealed class ParentStudentRelationshipRepository(IdentityDbContext context)
    : IParentStudentRelationshipRepository
{
    public Task AddAsync(
        ParentStudentRelationship relationship,
        CancellationToken cancellationToken) =>
        context.ParentStudentRelationships.AddAsync(relationship, cancellationToken).AsTask();

    public Task<ParentStudentRelationship?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        context.ParentStudentRelationships.SingleOrDefaultAsync(
            relationship => relationship.Id == id,
            cancellationToken);

    public Task<bool> HasActiveAsync(
        Guid parentUserId,
        Guid studentUserId,
        CancellationToken cancellationToken) =>
        context.ParentStudentRelationships.AnyAsync(
            relationship => relationship.ParentUserId == parentUserId
                && relationship.StudentUserId == studentUserId
                && relationship.Status != ParentStudentRelationshipStatus.Revoked,
            cancellationToken);

    public async Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeCoachingAgreementRepresentativeAsync(
        Guid representativeUserId,
        Guid studentUserId,
        CancellationToken cancellationToken)
    {
        var relationship = await context.ParentStudentRelationships
            .AsNoTracking()
            .Where(item => item.ParentUserId == representativeUserId)
            .Where(item => item.StudentUserId == studentUserId)
            .Where(item => item.Status == ParentStudentRelationshipStatus.Verified)
            .Where(item => item.ParentUser.IsActive && item.StudentUser.IsActive)
            .Where(item => item.ParentUser.Roles.Any(userRole => userRole.Role.Name == "Parent"))
            .Where(item => item.StudentUser.Roles.Any(userRole => userRole.Role.Name == "Student"))
            .Select(item => new { item.Id, item.Relationship })
            .SingleOrDefaultAsync(cancellationToken);

        return relationship?.Relationship switch
        {
            ParentRelationship.Mother or ParentRelationship.Father =>
                new CoachingAgreementRepresentativeAuthorization(relationship.Id, "Parent"),
            ParentRelationship.Guardian =>
                new CoachingAgreementRepresentativeAuthorization(relationship.Id, "LegalGuardian"),
            _ => null
        };
    }
}
