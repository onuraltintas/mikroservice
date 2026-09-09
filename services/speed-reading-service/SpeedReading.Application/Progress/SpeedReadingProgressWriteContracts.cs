using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SpeedReading.Application.Content;

namespace SpeedReading.Application.Progress;

public sealed record CreateExerciseResultRequest(
    Guid ExerciseId,
    Guid? ReadingTextId,
    int WordsRead,
    int TimeSpentSeconds,
    decimal RawWpm,
    decimal ComprehensionScore,
    decimal WeightedKdp,
    string? QuestionAnswersJson,
    string? ReadingMovementsJson,
    DateTime? CompletedAt = null,
    bool? IsMeasured = null,
    Guid? SessionId = null);

public interface ISpeedReadingProgressWriter
{
    Task<ExerciseResultSummary> CreateExerciseResultAsync(
        Guid studentId,
        CreateExerciseResultRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

public static class SpeedReadingProgressWriteRules
{
    public static string ValidateIdempotencyKey(string? key)
    {
        var normalized = key?.Trim() ?? string.Empty;
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                normalized,
                "^[A-Za-z0-9._~-]{16,128}$"))
        {
            throw new ArgumentException(
                "Idempotency-Key must contain 16-128 letters, numbers, dots, underscores, hyphens or tildes.",
                nameof(key));
        }

        return normalized;
    }

    public static Guid RequireAuthoritativeSession(Guid? sessionId)
    {
        if (sessionId is not { } value || value == Guid.Empty)
        {
            throw new EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException(
                "ExerciseResult.ServerAuthoritative",
                "Exercise results must reference a completed server-owned exercise session.");
        }

        return value;
    }
}

/// <summary>
/// Produces a stable hash for an idempotent command without persisting request
/// bodies or sensitive content. Length-prefixing prevents delimiter ambiguity.
/// </summary>
public static class SpeedReadingRequestHasher
{
    public static string Create(Guid studentId, CreateExerciseResultRequest request) =>
        Create(studentId.ToString("D"), Create(request));

    public static string Create(CreateExerciseResultRequest request) =>
        Create(
            Format(request.ExerciseId),
            Format(request.ReadingTextId),
            request.WordsRead.ToString(CultureInfo.InvariantCulture),
            request.TimeSpentSeconds.ToString(CultureInfo.InvariantCulture),
            request.RawWpm.ToString(CultureInfo.InvariantCulture),
            request.ComprehensionScore.ToString(CultureInfo.InvariantCulture),
            request.WeightedKdp.ToString(CultureInfo.InvariantCulture),
            request.QuestionAnswersJson,
            request.ReadingMovementsJson,
            Format(request.CompletedAt),
            request.IsMeasured?.ToString() ?? string.Empty,
            Format(request.SessionId));

    public static string Create(params string?[] values)
    {
        var canonical = string.Join('|', values.Select(value =>
        {
            var normalized = value ?? string.Empty;
            return $"{normalized.Length}:{normalized}";
        }));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string Format(Guid? value) => value?.ToString("D") ?? string.Empty;

    private static string Format(DateTime? value) =>
        value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
}
