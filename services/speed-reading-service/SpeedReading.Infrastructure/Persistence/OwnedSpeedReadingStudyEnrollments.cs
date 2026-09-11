using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assessment;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Content;
using SpeedReading.Domain.Assessment;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingStudyEnrollments(
    OwnedSpeedReadingDbContext db,
    ISpeedReadingUserDirectory userDirectory,
    ISpeedReadingProgressAccess progressAccess)
    : ISpeedReadingStudyEnrollments
{
    public async Task<IReadOnlyList<SpeedReadingStudyEnrollmentSummary>> GetAllAsync(CancellationToken cancellationToken)
    {
        var rows = await db.AssessmentStudyEnrollments
            .AsNoTracking()
            .OrderByDescending(item => item.EnrolledAt)
            .ToListAsync(cancellationToken);
        var directory = await userDirectory.GetUsersAsync(rows.Select(item => item.StudentId).ToArray(), cancellationToken);
        var usersById = directory.Users.ToDictionary(item => item.UserId);
        return rows.Select(item =>
        {
            usersById.TryGetValue(item.StudentId, out var user);
            return new SpeedReadingStudyEnrollmentSummary(
                item.Id, item.StudentId, item.StudyCode, item.ProtocolVersion, item.CohortCode,
                item.ConsentRecordedAt, item.EnrolledAt, item.IsActive, item.WithdrawnAt,
                user is null ? null : FormatName(user.FirstName, user.LastName), user?.Email,
                item.ConsentDocumentVersion, item.ConsentDocumentReference);
        }).ToList();
    }

    public async Task<IReadOnlyList<SpeedReadingStudyStudentOption>> SearchStudentsAsync(
        Guid viewerUserId,
        string searchTerm,
        CancellationToken cancellationToken)
    {
        var term = searchTerm?.Trim() ?? string.Empty;
        if (viewerUserId == Guid.Empty || term.Length < 2)
            return [];

        var studentIds = await progressAccess.SearchStudentUserIdsAsync(viewerUserId, term, cancellationToken);
        var directory = await userDirectory.GetUsersAsync(studentIds, cancellationToken);
        return directory.Users
            .Where(item => item.IsActive)
            .OrderBy(item => item.FirstName)
            .ThenBy(item => item.LastName)
            .Select(item => new SpeedReadingStudyStudentOption(
                item.UserId,
                FormatName(item.FirstName, item.LastName),
                item.Email))
            .ToList();
    }

    public async Task<SpeedReadingStudyEnrollmentSummary> CreateAsync(
        Guid actorId,
        CreateSpeedReadingStudyEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var study = await db.AssessmentStudyDefinitions.SingleOrDefaultAsync(item =>
            item.Id == request.StudyDefinitionId && item.IsActive, cancellationToken)
            ?? throw new ArgumentException("The selected study definition is not active.", nameof(request));
        if (await db.AssessmentStudyEnrollments.AnyAsync(item =>
                item.StudentId == request.StudentId
                && item.StudyCode == study.StudyCode
                && item.IsActive,
                cancellationToken))
            throw new ArgumentException("The student already has an active enrollment in this study.", nameof(request));
        var row = AssessmentStudyEnrollment.Create(
            Guid.NewGuid(), request.StudentId, study.Id, study.StudyCode, study.ProtocolVersion,
            study.CohortCode, study.ConsentDocumentVersion, study.ConsentDocumentReference,
            request.ConsentRecordedAt, actorId, DateTime.UtcNow);
        db.AssessmentStudyEnrollments.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return new SpeedReadingStudyEnrollmentSummary(
            row.Id, row.StudentId, row.StudyCode, row.ProtocolVersion, row.CohortCode,
            row.ConsentRecordedAt, row.EnrolledAt, row.IsActive, row.WithdrawnAt,
            ConsentDocumentVersion: row.ConsentDocumentVersion,
            ConsentDocumentReference: row.ConsentDocumentReference);
    }

    public async Task<bool> WithdrawAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var row = await db.AssessmentStudyEnrollments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null) return false;
        row.Withdraw(actorId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return true;
}

    private static string FormatName(string firstName, string lastName) =>
        string.Join(' ', new[] { firstName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
}
