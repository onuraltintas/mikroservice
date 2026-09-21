using Coaching.Application.Queries.ExportCoachingData;

namespace Coaching.Application.Interfaces;

public interface ICoachingDataExportRepository
{
    Task<CoachingDataExportDto> ExportStudentDataAsync(
        Guid studentId,
        DateTimeOffset exportedAt,
        CancellationToken cancellationToken);
}
