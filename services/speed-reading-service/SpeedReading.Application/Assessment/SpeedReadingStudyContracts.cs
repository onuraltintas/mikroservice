namespace SpeedReading.Application.Assessment;

public sealed record SpeedReadingStudyEnrollmentSummary(
    Guid Id,
    Guid StudentId,
    string StudyCode,
    string ProtocolVersion,
    string CohortCode,
    DateTime ConsentRecordedAt,
    DateTime EnrolledAt,
    bool IsActive,
    DateTime? WithdrawnAt);

public sealed record CreateSpeedReadingStudyEnrollmentRequest(
    Guid StudentId,
    string StudyCode,
    string ProtocolVersion,
    string CohortCode,
    DateTime ConsentRecordedAt);

public interface ISpeedReadingStudyEnrollments
{
    Task<IReadOnlyList<SpeedReadingStudyEnrollmentSummary>> GetAllAsync(CancellationToken cancellationToken);
    Task<SpeedReadingStudyEnrollmentSummary> CreateAsync(Guid actorId, CreateSpeedReadingStudyEnrollmentRequest request, CancellationToken cancellationToken);
    Task<bool> WithdrawAsync(Guid id, Guid actorId, CancellationToken cancellationToken);
}

public sealed class UnavailableSpeedReadingStudyEnrollments : ISpeedReadingStudyEnrollments
{
    public Task<IReadOnlyList<SpeedReadingStudyEnrollmentSummary>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SpeedReadingStudyEnrollmentSummary>>([]);
    public Task<SpeedReadingStudyEnrollmentSummary> CreateAsync(Guid actorId, CreateSpeedReadingStudyEnrollmentRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Research enrollments require owned Speed Reading data.");
    public Task<bool> WithdrawAsync(Guid id, Guid actorId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Research enrollments require owned Speed Reading data.");
}
