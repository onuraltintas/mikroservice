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
    DateTime? WithdrawnAt,
    string? StudentName = null,
    string? StudentEmail = null,
    string? ConsentDocumentVersion = null,
    string? ConsentDocumentReference = null);

public sealed record SpeedReadingStudyDefinitionSummary(
    Guid Id,
    string StudyCode,
    string Name,
    string ProtocolVersion,
    string CohortCode,
    string ConsentDocumentVersion,
    string ConsentDocumentReference,
    bool IsActive);

public sealed record CreateSpeedReadingStudyDefinitionRequest(
    string StudyCode,
    string Name,
    string ProtocolVersion,
    string CohortCode,
    string ConsentDocumentVersion,
    string ConsentDocumentReference);

public sealed record UpdateSpeedReadingStudyDefinitionRequest(
    string Name,
    string ProtocolVersion,
    string CohortCode,
    string ConsentDocumentVersion,
    string ConsentDocumentReference);

public sealed record SpeedReadingStudyStudentOption(
    Guid StudentId,
    string DisplayName,
    string? Email);

public sealed record CreateSpeedReadingStudyEnrollmentRequest(
    Guid StudentId,
    Guid StudyDefinitionId,
    DateTime ConsentRecordedAt);

public interface ISpeedReadingStudyCatalog
{
    Task<IReadOnlyList<SpeedReadingStudyDefinitionSummary>> GetAllAsync(CancellationToken cancellationToken);
    Task<SpeedReadingStudyDefinitionSummary> CreateAsync(Guid actorId, CreateSpeedReadingStudyDefinitionRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Guid id, Guid actorId, UpdateSpeedReadingStudyDefinitionRequest request, CancellationToken cancellationToken);
    Task<bool> RetireAsync(Guid id, Guid actorId, CancellationToken cancellationToken);
}

public interface ISpeedReadingStudyEnrollments
{
    Task<IReadOnlyList<SpeedReadingStudyEnrollmentSummary>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<SpeedReadingStudyStudentOption>> SearchStudentsAsync(
        Guid viewerUserId,
        string searchTerm,
        CancellationToken cancellationToken);
    Task<SpeedReadingStudyEnrollmentSummary> CreateAsync(Guid actorId, CreateSpeedReadingStudyEnrollmentRequest request, CancellationToken cancellationToken);
    Task<bool> WithdrawAsync(Guid id, Guid actorId, CancellationToken cancellationToken);
}

public sealed class UnavailableSpeedReadingStudyEnrollments : ISpeedReadingStudyEnrollments
{
    public Task<IReadOnlyList<SpeedReadingStudyEnrollmentSummary>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SpeedReadingStudyEnrollmentSummary>>([]);
    public Task<IReadOnlyList<SpeedReadingStudyStudentOption>> SearchStudentsAsync(
        Guid viewerUserId,
        string searchTerm,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SpeedReadingStudyStudentOption>>([]);
    public Task<SpeedReadingStudyEnrollmentSummary> CreateAsync(Guid actorId, CreateSpeedReadingStudyEnrollmentRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Research enrollments require owned Speed Reading data.");
    public Task<bool> WithdrawAsync(Guid id, Guid actorId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Research enrollments require owned Speed Reading data.");
}

public sealed class UnavailableSpeedReadingStudyCatalog : ISpeedReadingStudyCatalog
{
    public Task<IReadOnlyList<SpeedReadingStudyDefinitionSummary>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SpeedReadingStudyDefinitionSummary>>([]);
    public Task<SpeedReadingStudyDefinitionSummary> CreateAsync(Guid actorId, CreateSpeedReadingStudyDefinitionRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Research studies require owned Speed Reading data.");
    public Task<bool> UpdateAsync(Guid id, Guid actorId, UpdateSpeedReadingStudyDefinitionRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Research studies require owned Speed Reading data.");
    public Task<bool> RetireAsync(Guid id, Guid actorId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Research studies require owned Speed Reading data.");
}
