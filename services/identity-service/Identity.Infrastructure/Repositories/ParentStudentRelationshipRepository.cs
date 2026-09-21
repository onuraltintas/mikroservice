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
}
