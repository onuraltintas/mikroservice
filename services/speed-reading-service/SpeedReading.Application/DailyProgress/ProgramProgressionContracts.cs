namespace SpeedReading.Application.DailyProgress;

public sealed record ProgramProgressionSettings(bool AllowEarlyAdvance, DateTime? UpdatedAt);
public sealed record StudentProgressionPreference(string Mode, bool AllowEarlyAdvance, bool DefaultAllowEarlyAdvance);
public sealed record ProgramDayAdvanceState(Guid? ProgramProgressId, int CompletedProgramDay, int NextProgramDay,
    bool AllowEarlyAdvance, bool CanAdvance);
public sealed record AdvanceProgramDayRequest(Guid ProgramProgressId, int CompletedProgramDay);
public sealed record UpdateProgramProgressionSettingsRequest(bool? AllowEarlyAdvance);
public sealed record UpdateStudentProgressionPreferenceRequest(string Mode);

public interface ISpeedReadingProgramProgression
{
    Task<ProgramProgressionSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<ProgramProgressionSettings> UpdateSettingsAsync(bool allow, Guid actorId, CancellationToken cancellationToken = default);
    Task<StudentProgressionPreference> GetPreferenceAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<StudentProgressionPreference> UpdatePreferenceAsync(Guid userId, string mode, Guid actorId, CancellationToken cancellationToken = default);
    Task<ProgramDayAdvanceState> GetAdvanceStateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ProgramDayAdvanceState> AdvanceAsync(Guid userId, AdvanceProgramDayRequest request, CancellationToken cancellationToken = default);
}
