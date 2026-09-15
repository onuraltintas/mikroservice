using System.Text.Json;

namespace SpeedReading.Domain.Programs;

public static class ProgramWeeklyPatternRules
{
    public static void Validate(string weeklyPatternJson, bool isAssessment, bool isActive)
    {
        try
        {
            using var document = JsonDocument.Parse(weeklyPatternJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Haftalık plan bir JSON nesnesi olmalıdır.", nameof(weeklyPatternJson));

            var weeks = document.RootElement.EnumerateObject()
                .Where(item => item.Name.StartsWith("week", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (isActive && weeks.Count == 0)
            {
                throw new ArgumentException(
                    "Aktif programda en az bir haftalık egzersiz planı bulunmalıdır.",
                    nameof(weeklyPatternJson));
            }

            foreach (var week in weeks)
                ValidateWeek(week.Value, isAssessment, weeklyPatternJson);

            if (document.RootElement.TryGetProperty("adaptation", out var adaptation))
                ValidateAdaptation(adaptation, weeklyPatternJson);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Haftalık plan geçerli JSON olmalıdır.", nameof(weeklyPatternJson), exception);
        }
    }

    private static void ValidateWeek(JsonElement week, bool isAssessment, string parameterName)
    {
        if (week.ValueKind == JsonValueKind.Array)
        {
            ValidateEntries(week, isAssessment, parameterName);
            return;
        }
        if (week.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Her hafta bir egzersiz listesi veya gün listeleri içermelidir.", parameterName);

        var days = week.EnumerateObject()
            .Where(item => item.Name.StartsWith("day", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (days.Count == 0)
            throw new ArgumentException("Haftalık plan en az bir gün içermelidir.", parameterName);
        foreach (var day in days)
        {
            if (day.Value.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("Her gün bir egzersiz listesi içermelidir.", parameterName);
            ValidateEntries(day.Value, isAssessment, parameterName);
        }
    }

    private static void ValidateEntries(JsonElement entries, bool isAssessment, string parameterName)
    {
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Programdaki her egzersiz kaydı bir nesne olmalıdır.", parameterName);
            if (isAssessment)
            {
                if (!entry.TryGetProperty("exerciseId", out var id)
                    || id.ValueKind != JsonValueKind.String
                    || !Guid.TryParse(id.GetString(), out _))
                {
                    throw new ArgumentException("Seviye tespit planındaki her kayıtta geçerli bir exerciseId olmalıdır.", parameterName);
                }
                continue;
            }

            if (!entry.TryGetProperty("type", out var type)
                || type.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(type.GetString()))
            {
                throw new ArgumentException("Programdaki her egzersizde tür adı bulunmalıdır.", parameterName);
            }
            if (!entry.TryGetProperty("count", out var count)
                || !count.TryGetInt32(out var countValue)
                || countValue is < 1 or > 20)
            {
                throw new ArgumentException("Programdaki egzersiz sayısı 1 ile 20 arasında olmalıdır.", parameterName);
            }
            if (entry.TryGetProperty("difficulty", out var difficulty)
                && (!difficulty.TryGetInt32(out var difficultyValue) || difficultyValue is < 0 or > 10))
            {
                throw new ArgumentException("Programdaki egzersiz zorluğu 0 ile 10 arasında olmalıdır.", parameterName);
            }
        }
    }

    private static void ValidateAdaptation(JsonElement adaptation, string parameterName)
    {
        if (adaptation.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Uyarlama kuralları bir JSON nesnesi olmalıdır.", parameterName);

        var minimumSessions = GetOptionalInt(adaptation, "minimumMeasuredSessions", 2, 10, parameterName);
        var advance = GetOptionalDecimal(adaptation, "advanceComprehensionThreshold", 0, 100, parameterName);
        var maintain = GetOptionalDecimal(adaptation, "maintainComprehensionThreshold", 0, 100, parameterName);
        _ = GetOptionalDecimal(adaptation, "minimumWpmTrendPercent", -100, 100, parameterName);
        _ = GetOptionalDecimal(adaptation, "supportTrendPercent", -100, 0, parameterName);
        if (advance.HasValue && maintain.HasValue && advance.Value < maintain.Value)
            throw new ArgumentException("İlerleme anlama eşiği pekiştirme eşiğinin altında olamaz.", parameterName);
        _ = minimumSessions;
    }

    private static int? GetOptionalInt(JsonElement element, string name, int min, int max, string parameterName)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (!value.TryGetInt32(out var number) || number < min || number > max)
            throw new ArgumentException($"{name} geçerli aralıkta olmalıdır.", parameterName);
        return number;
    }

    private static decimal? GetOptionalDecimal(JsonElement element, string name, decimal min, decimal max, string parameterName)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (!value.TryGetDecimal(out var number) || number < min || number > max)
            throw new ArgumentException($"{name} geçerli aralıkta olmalıdır.", parameterName);
        return number;
    }
}
