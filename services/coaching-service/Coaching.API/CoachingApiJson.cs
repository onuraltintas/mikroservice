using System.Text.Json;
using System.Text.Json.Serialization;
using Coaching.Domain.Profiles;

namespace Coaching.API;

/// <summary>The API's JSON enum handling: names only (no numbers), case-insensitive.</summary>
public static class CoachingApiJson
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new EducationStatusConverter());
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    }

    /// <summary>
    /// E3 review L1: "Graduate" was renamed HighSchoolGraduate (same stored number). A page still open from before the release sends
    /// "Graduate"; it is read as HighSchoolGraduate. Written values are always the current names.
    /// </summary>
    private sealed class EducationStatusConverter : JsonConverter<CoachingEducationStatus>
    {
        public override CoachingEducationStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException("Eğitim durumu bir ad olarak gönderilmeli.");
            var text = reader.GetString();
            if (string.Equals(text, "Graduate", StringComparison.OrdinalIgnoreCase)) return CoachingEducationStatus.HighSchoolGraduate;
            return !string.IsNullOrWhiteSpace(text) && !char.IsDigit(text.TrimStart()[0]) && !text.Contains(',')
                && Enum.TryParse<CoachingEducationStatus>(text, ignoreCase: true, out var value) && Enum.IsDefined(value)
                ? value : throw new JsonException("Geçersiz eğitim durumu.");
        }

        public override void Write(Utf8JsonWriter writer, CoachingEducationStatus value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }
}
