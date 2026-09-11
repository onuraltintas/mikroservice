using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assessment;
using SpeedReading.Domain.Assessment;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingStudyCatalog(OwnedSpeedReadingDbContext db) : ISpeedReadingStudyCatalog
{
    public async Task<IReadOnlyList<SpeedReadingStudyDefinitionSummary>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.AssessmentStudyDefinitions.AsNoTracking()
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.StudyCode).ThenBy(item => item.CohortCode)
            .Select(item => new SpeedReadingStudyDefinitionSummary(item.Id, item.StudyCode, item.Name,
                item.ProtocolVersion, item.CohortCode, item.ConsentDocumentVersion, item.ConsentDocumentReference, item.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<SpeedReadingStudyDefinitionSummary> CreateAsync(Guid actorId, CreateSpeedReadingStudyDefinitionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = request.StudyCode?.Trim() ?? string.Empty;
        var cohort = request.CohortCode?.Trim() ?? string.Empty;
        if (await db.AssessmentStudyDefinitions.AnyAsync(item => item.StudyCode == code && item.CohortCode == cohort, cancellationToken))
            throw new ArgumentException("A study definition already exists for this study code and cohort.", nameof(request));
        var row = AssessmentStudyDefinition.Create(Guid.NewGuid(), code, request.Name, request.ProtocolVersion, cohort,
            request.ConsentDocumentVersion, request.ConsentDocumentReference, actorId, DateTime.UtcNow);
        db.AssessmentStudyDefinitions.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(row);
    }

    public async Task<bool> UpdateAsync(Guid id, Guid actorId, UpdateSpeedReadingStudyDefinitionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var row = await db.AssessmentStudyDefinitions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null) return false;
        var cohort = request.CohortCode?.Trim() ?? string.Empty;
        if (await db.AssessmentStudyDefinitions.AnyAsync(item =>
                item.Id != id && item.StudyCode == row.StudyCode && item.CohortCode == cohort,
                cancellationToken))
            throw new ArgumentException("A study definition already exists for this study code and cohort.", nameof(request));
        row.Update(request.Name, request.ProtocolVersion, cohort, request.ConsentDocumentVersion, request.ConsentDocumentReference, actorId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RetireAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var row = await db.AssessmentStudyDefinitions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null) return false;
        row.Retire(actorId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static SpeedReadingStudyDefinitionSummary ToSummary(AssessmentStudyDefinition item) =>
        new(item.Id, item.StudyCode, item.Name, item.ProtocolVersion, item.CohortCode,
            item.ConsentDocumentVersion, item.ConsentDocumentReference, item.IsActive);
}
