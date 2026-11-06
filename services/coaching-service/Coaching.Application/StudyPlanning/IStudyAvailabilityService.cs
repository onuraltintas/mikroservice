using System.ComponentModel.DataAnnotations;

namespace Coaching.Application.StudyPlanning;

public sealed record StudyWindowInput(DayOfWeek Day, int StartMinute, int EndMinute);
public sealed record StudyAvailabilityUpdate(int? ExpectedVersion,
    /* E3 #34: ignored, every study time is in Europe/Istanbul; older clients still send it. */ string? TimeZoneId,
    [Required] IReadOnlyList<StudyWindowInput> Windows);
public sealed record StudyAvailabilityView(int Version, string TimeZoneId, IReadOnlyList<StudyWindowInput> Windows);

public interface IStudyAvailabilityService
{
    Task<StudyAvailabilityView?> GetAsync(CancellationToken cancellationToken = default);
    Task<StudyAvailabilityView> ReplaceAsync(StudyAvailabilityUpdate request, CancellationToken cancellationToken = default);
}
