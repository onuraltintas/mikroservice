using System.Text.Json;

namespace SpeedReading.Domain.Sessions;

public sealed record TargetHuntSettings(
    string Target,
    IReadOnlyList<string> Distractors,
    int Rows,
    int Columns,
    int TargetCount,
    int TimeLimitSeconds);

/// <summary>
/// "Hedef Avı" (decision 29.10.2026, replaces N-back in programs): rows of letters or short words in which the student
/// taps every occurrence of one target among look-alike distractors before time runs out. Scoring matches error
/// analysis: found-target percentage minus five points per wrong tap, at most thirty.
/// </summary>
public static class TargetHuntRules
{
    public const string EngineType = "target_hunt";
    public const int MinGridSide = 3;
    public const int MaxGridSide = 12;
    public const int MaxTokenLength = 12;
    public const int MaxDistractors = 10;
    public const int MinTimeLimitSeconds = 20;
    public const int MaxTimeLimitSeconds = 600;

    public static TargetHuntSettings Read(JsonElement engineConfig)
    {
        if (engineConfig.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Hedef avı ayarları eksik.");
        var target = Token(engineConfig, "target", "Hedef");
        if (!engineConfig.TryGetProperty("distractors", out var list) || list.ValueKind != JsonValueKind.Array
            || list.GetArrayLength() is < 1 or > MaxDistractors)
            throw new ArgumentException($"Hedef avı için 1–{MaxDistractors} çeldirici gerekir.");
        var distractors = new List<string>();
        foreach (var item in list.EnumerateArray())
        {
            var value = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (!IsToken(value) || value == target || distractors.Contains(value!))
                throw new ArgumentException("Hedef avı çeldiricileri boşluksuz, birbirinden ve hedeften farklı olmalıdır.");
            distractors.Add(value!);
        }
        var rows = Integer(engineConfig, "rows", MinGridSide, MaxGridSide, "Satır sayısı");
        var columns = Integer(engineConfig, "columns", MinGridSide, MaxGridSide, "Sütun sayısı");
        var targetCount = Integer(engineConfig, "targetCount", 1, rows * columns / 2, "Hedef sayısı (en fazla öğelerin yarısı)");
        var timeLimit = Integer(engineConfig, "timeLimit", MinTimeLimitSeconds, MaxTimeLimitSeconds, "Süre (sn)");
        return new TargetHuntSettings(target, distractors, rows, columns, targetCount, timeLimit);
    }

    public static string[] CreateLayout(TargetHuntSettings settings, Random random)
    {
        var count = settings.Rows * settings.Columns;
        var targets = Enumerable.Range(0, count).OrderBy(_ => random.Next()).Take(settings.TargetCount).ToHashSet();
        return Enumerable.Range(0, count)
            .Select(index => targets.Contains(index)
                ? settings.Target
                : settings.Distractors[random.Next(settings.Distractors.Count)])
            .ToArray();
    }

    public static decimal Accuracy(int hits, int falseTaps, int targets) => targets > 0
        ? Math.Round(100m * hits / (targets + falseTaps), 2) : 0;

    public static decimal Score(int hits, int falseTaps, int targets) => targets > 0
        ? Math.Max(0, Math.Round(100m * hits / targets - Math.Min(falseTaps * 5, 30), 2)) : 0;

    private static string Token(JsonElement config, string name, string label)
    {
        var value = config.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() : null;
        if (!IsToken(value))
            throw new ArgumentException($"{label} 1–{MaxTokenLength} karakter olmalı ve boşluk içermemelidir.");
        return value!;
    }

    private static bool IsToken(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= MaxTokenLength && !value.Any(char.IsWhiteSpace);

    private static int Integer(JsonElement config, string name, int min, int max, string label)
    {
        if (!config.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Number
            || !element.TryGetInt32(out var value) || value < min || value > max)
            throw new ArgumentException($"{label} {min}–{max} arasında olmalıdır.");
        return value;
    }
}
