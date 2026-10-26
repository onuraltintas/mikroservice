namespace CourseManagement.Domain;

public sealed class LegacyStudentImportMapping
{
    private LegacyStudentImportMapping() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string SourceSystem { get; private set; } = string.Empty;
    public Guid SourceRecordId { get; private set; }
    public Guid StudentRecordId { get; private set; }
    public string SelectedFields { get; private set; } = string.Empty;
    public DateTimeOffset ImportedAt { get; private set; }

    public static LegacyStudentImportMapping Create(Guid id, Guid workspaceId, string sourceSystem,
        Guid sourceRecordId, Guid studentRecordId, string selectedFields, DateTimeOffset importedAt)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || sourceRecordId == Guid.Empty || studentRecordId == Guid.Empty)
            throw new ArgumentException("Import mapping identifiers are required.");
        if (sourceSystem is not ("coaching" or "speed-reading"))
            throw new ArgumentException("The source system is not supported.", nameof(sourceSystem));
        ArgumentNullException.ThrowIfNull(selectedFields);
        if (selectedFields.Length is < 1 or > 80)
            throw new ArgumentException("Selected fields are required.", nameof(selectedFields));

        return new LegacyStudentImportMapping
        {
            Id = id,
            WorkspaceId = workspaceId,
            SourceSystem = sourceSystem,
            SourceRecordId = sourceRecordId,
            StudentRecordId = studentRecordId,
            SelectedFields = selectedFields,
            ImportedAt = importedAt
        };
    }
}
