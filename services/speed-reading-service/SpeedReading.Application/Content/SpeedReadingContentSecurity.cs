using System.Text.Json;
using System.Text.Json.Nodes;

namespace SpeedReading.Application.Content;

/// <summary>
/// Removes fields that can reveal answer keys from student-facing exercise configuration.
/// Configuration remains editable by administrators through the protected write endpoints.
/// </summary>
public static class SpeedReadingContentSecurity
{
    private static readonly HashSet<string> AnswerKeyPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "correctAnswer",
        "correct_answer",
        "correctOption",
        "correct_option",
        "correctAnswers",
        "correct_answers",
        "acceptedAnswers",
        "accepted_answers",
        "answerKey",
        "answer_key",
        "answerKeys",
        "answer_keys",
        "isCorrect",
        "is_correct",
        "correctIndex",
        "correct_index",
        "correctIndices",
        "correct_indices",
        "answerIndex",
        "answer_index",
        "answerIndices",
        "answer_indices",
        "targetIndex",
        "target_index",
        "targetIndices",
        "target_indices",
        "positionTargetIndices",
        "position_target_indices",
        "wordTargetIndices",
        "word_target_indices",
        "visualExpansionExpectedStimuli",
        "visual_expansion_expected_stimuli",
        "isMiss",
        "is_miss"
    };

    public static string SanitizeExerciseConfiguration(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return "{}";

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            return SanitizeStudentJson(document.RootElement).GetRawText();
        }
        catch (JsonException)
        {
            return "{}";
        }
    }

    /// <summary>
    /// Produces the stricter public payload used by placement assessments.
    /// Explanations are hidden as well as answer keys because they can reveal
    /// the intended answer before the assessment is scored.
    /// </summary>
    public static string SanitizeAssessmentConfiguration(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return "{}";

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            return SanitizeAssessmentJson(document.RootElement).GetRawText();
        }
        catch (JsonException)
        {
            return "{}";
        }
    }

    public static JsonElement SanitizeStudentJson(JsonElement element)
        => SanitizeJson(element, removeExplanations: false);

    public static JsonElement SanitizeAssessmentJson(JsonElement element)
        => SanitizeJson(element, removeExplanations: true);

    /// <summary>
    /// Produces an assessment payload for focus exercises without exposing
    /// the complete stimulus sequence. The current stimulus is streamed from
    /// the session endpoint one trial at a time.
    /// </summary>
    public static JsonElement SanitizeFocusAssessmentJson(JsonElement element)
        => SanitizeJson(element, removeExplanations: true, removeFocusSequences: true);

    private static JsonElement SanitizeJson(
        JsonElement element,
        bool removeExplanations,
        bool removeFocusSequences = false)
    {
        try
        {
            var node = JsonNode.Parse(element.GetRawText());
            RemoveAnswerKeys(node, removeExplanations, removeFocusSequences);
            using var document = JsonDocument.Parse(node?.ToJsonString() ?? "{}");
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new { });
        }
    }

    private static void RemoveAnswerKeys(
        JsonNode? node,
        bool removeExplanations,
        bool removeFocusSequences = false)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var property in jsonObject.ToList())
            {
                if (AnswerKeyPropertyNames.Contains(property.Key)
                    || (removeExplanations && property.Key.Equals("explanation", StringComparison.OrdinalIgnoreCase))
                    || (removeFocusSequences && (property.Key.Equals("positionSequence", StringComparison.OrdinalIgnoreCase)
                        || property.Key.Equals("wordSequence", StringComparison.OrdinalIgnoreCase))))
                    jsonObject.Remove(property.Key);
                else
                    RemoveAnswerKeys(property.Value, removeExplanations, removeFocusSequences);
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
                RemoveAnswerKeys(item, removeExplanations, removeFocusSequences);
        }
    }
}
