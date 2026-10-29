namespace SpeedReading.Application.Privacy;

/// <summary>
/// KVKK access right (review H2b, finding 9): everything Speed Reading keeps about one user, section by section.
/// Each row is the record's own fields; internal working data and secrets (provider tokens and raw responses,
/// push keys, token hashes, session snapshots) are left out.
/// </summary>
public sealed record SpeedReadingPersonalDataExportDto(
    Guid UserId,
    string SchemaVersion,
    DateTime GeneratedAt,
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> Sections);

public interface ISpeedReadingPersonalDataExport
{
    Task<SpeedReadingPersonalDataExportDto> ExportAsync(Guid userId, CancellationToken cancellationToken);
}
