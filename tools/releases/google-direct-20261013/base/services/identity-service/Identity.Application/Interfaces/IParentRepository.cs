using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Interfaces;

public interface IParentRepository
{
    Task AddAsync(ParentProfile parent, CancellationToken cancellationToken);
    Task<ParentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<ParentProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentProfile>> GetActiveChildrenByUserIdAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IParentStudentRelationshipRepository
{
    Task AddAsync(ParentStudentRelationship relationship, CancellationToken cancellationToken);
    Task<ParentStudentRelationship?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> HasActiveAsync(Guid parentUserId, Guid studentUserId, CancellationToken cancellationToken);
    Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeCoachingAgreementRepresentativeAsync(
        Guid representativeUserId,
        Guid studentUserId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<VerifiedParentStudentRelationship>> GetVerifiedChildrenAsync(
        Guid parentUserId,
        CancellationToken cancellationToken);
    Task<ParentStudentRelationshipPage> SearchAsync(
        ParentStudentRelationshipStatus? status,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed record CoachingAgreementRepresentativeAuthorization(Guid RelationshipId, string PartyRole);
public sealed record VerifiedParentStudentRelationship(
    Guid RelationshipId,
    ParentRelationship Relationship,
    StudentProfile Student);

public sealed record ParentStudentRelationshipListItem(
    Guid Id, Guid ParentUserId, string ParentName, string ParentEmail,
    Guid StudentUserId, string StudentName, string StudentEmail,
    ParentRelationship Relationship, ParentStudentRelationshipStatus Status,
    DateTime RequestedAt, DateTime? VerifiedAt, DateTime? RevokedAt,
    string? RevocationReason);

public sealed record ParentStudentRelationshipPage(
    IReadOnlyList<ParentStudentRelationshipListItem> Items,
    int TotalCount);
