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

    public async Task<IReadOnlyList<VerifiedParentStudentRelationship>> GetVerifiedChildrenAsync(
        Guid parentUserId,
        CancellationToken cancellationToken)
    {
        if (parentUserId == Guid.Empty)
            return [];

        var rows = await context.ParentStudentRelationships
            .AsNoTracking()
            .Where(item => item.ParentUserId == parentUserId)
            .Where(item => item.Status == ParentStudentRelationshipStatus.Verified)
            .Where(item => item.Relationship == ParentRelationship.Mother
                || item.Relationship == ParentRelationship.Father
                || item.Relationship == ParentRelationship.Guardian)
            .Where(item => item.ParentUser.IsActive && item.StudentUser.IsActive)
            .Where(item => item.ParentUser.Roles.Any(userRole => userRole.Role.Name == "Parent"))
            .Where(item => item.StudentUser.Roles.Any(userRole => userRole.Role.Name == "Student"))
            .Join(
                context.StudentProfiles.AsNoTracking()
                    .Include(student => student.Institution)
                    .Where(student => student.IsActive)
                    .Where(student => !student.InstitutionId.HasValue
                        || (student.Institution != null && student.Institution.IsActive)),
                relationship => relationship.StudentUserId,
                student => student.UserId,
                (relationship, student) => new
                {
                    RelationshipId = relationship.Id,
                    relationship.Relationship,
                    Student = student
                })
            .OrderBy(item => item.Student.FirstName)
            .ThenBy(item => item.Student.LastName)
            .ToListAsync(cancellationToken);

        return rows.Select(item => new VerifiedParentStudentRelationship(
            item.RelationshipId,
            item.Relationship,
            item.Student)).ToList();
    }
}
