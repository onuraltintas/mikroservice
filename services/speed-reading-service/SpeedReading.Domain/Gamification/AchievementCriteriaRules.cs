using System.Text.Json;

namespace SpeedReading.Domain.Gamification;

public static class AchievementCriteriaRules
{
    public static void Validate(string criteriaType, string criteriaValue, bool isRepeatable)
    {
        if (isRepeatable)
        {
            throw new ArgumentException(
                "Tekrarlanabilir rozetler henüz tekil rozet kayıt modeliyle desteklenmiyor.",
                nameof(isRepeatable));
        }

        try
        {
            using var document = JsonDocument.Parse(criteriaValue);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Başarı kriteri bir JSON nesnesi olmalıdır.", nameof(criteriaValue));
            var criteria = document.RootElement;
            switch (criteriaType.Trim().ToLowerInvariant())
            {
                case "streak": RequireInt(criteria, "days", 1, int.MaxValue); break;
                case "level_reached": RequireInt(criteria, "level", 1, 10_000); break;
                case "activity_count":
                case "reading_count":
                case "rsvp_count":
                case "exercise_count":
                case "vocabulary_learned":
                case "vocabulary_streak":
                case "vocabulary_categories": RequireInt(criteria, "count", 1, int.MaxValue); break;
                case "total_xp": RequireLong(criteria, "xp", 1, long.MaxValue); break;
                case "reading_minutes": RequireInt(criteria, "minutes", 1, int.MaxValue); break;
                case "wpm_reached":
                case "rsvp_wpm": RequireInt(criteria, "wpm", 1, 1_500); break;
                case "comprehension_score":
                case "rsvp_comprehension": RequireDecimal(criteria, "score", 0, 100); break;
                case "vocabulary_box": RequireInt(criteria, "box", 1, 5); break;
                case "exercise_type_first": RequireText(criteria, "type"); break;
                case "exercise_variety": RequireInt(criteria, "types", 1, int.MaxValue); break;
                default:
                    throw new ArgumentException("Başarı kriter türü desteklenmiyor.", nameof(criteriaType));
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Başarı kriteri geçerli JSON olmalıdır.", nameof(criteriaValue), exception);
        }
    }

    private static void RequireInt(JsonElement criteria, string name, int minimum, int maximum)
    {
        if (!criteria.TryGetProperty(name, out var value)
            || !value.TryGetInt32(out var number)
            || number < minimum || number > maximum)
        {
            throw new ArgumentException($"{name} geçerli aralıkta bir tam sayı olmalıdır.");
        }
    }

    private static void RequireLong(JsonElement criteria, string name, long minimum, long maximum)
    {
        if (!criteria.TryGetProperty(name, out var value)
            || !value.TryGetInt64(out var number)
            || number < minimum || number > maximum)
        {
            throw new ArgumentException($"{name} geçerli aralıkta bir tam sayı olmalıdır.");
        }
    }

    private static void RequireDecimal(JsonElement criteria, string name, decimal minimum, decimal maximum)
    {
        if (!criteria.TryGetProperty(name, out var value)
            || !value.TryGetDecimal(out var number)
            || number < minimum || number > maximum)
        {
            throw new ArgumentException($"{name} geçerli aralıkta bir sayı olmalıdır.");
        }
    }

    private static void RequireText(JsonElement criteria, string name)
    {
        if (!criteria.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString())
            || value.GetString()!.Trim().Length > 100)
        {
            throw new ArgumentException($"{name} geçerli bir metin olmalıdır.");
        }
    }
}
