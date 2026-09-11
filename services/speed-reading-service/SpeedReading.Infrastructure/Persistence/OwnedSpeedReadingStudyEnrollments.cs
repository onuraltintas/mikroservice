using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assessment;
using SpeedReading.Domain.Assessment;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingStudyEnrollments(OwnedSpeedReadingDbContext db)
    : ISpeedReadingStudyEnrollments
{
    public async Task<IReadOnlyList<SpeedReadingStudyEnrollmentSummary>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.AssessmentStudyEnrollments
            .AsNoTracking()
            .OrderByDescending(item => item.EnrolledAt)
            .Select(item => new SpeedReadingStudyEnrollmentSummary(
                item.Id, item.StudentId, item.StudyCode, item.ProtocolVersion, item.CohortCode,
                item.ConsentRecordedAt, item.EnrolledAt, item.IsActive, item.WithdrawnAt))
            .ToListAsync(cancellationToken);

    public async Task<SpeedReadingStudyEnrollmentSummary> CreateAsync(
        Guid actorId,
        CreateSpeedReadingStudyEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await db.AssessmentStudyEnrollments.AnyAsync(item =>
                item.StudentId == request.StudentId
                && item.StudyCode == request.StudyCode.Trim()
                && item.IsActive,
                cancellationToken))
            throw new ArgumentException("The student already has an active enrollment in this study.", nameof(request));
        var row = AssessmentStudyEnrollment.Create(
            Guid.NewGuid(), request.StudentId, request.StudyCode, request.ProtocolVersion,
            request.CohortCode, request.ConsentRecordedAt, actorId, DateTime.UtcNow);
        db.AssessmentStudyEnrollments.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return new SpeedReadingStudyEnrollmentSummary(
            row.Id, row.StudentId, row.StudyCode, row.ProtocolVersion, row.CohortCode,
            row.ConsentRecordedAt, row.EnrolledAt, row.IsActive, row.WithdrawnAt);
    }

    public async Task<bool> WithdrawAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var row = await db.AssessmentStudyEnrollments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null) return false;
        row.Withdraw(actorId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
