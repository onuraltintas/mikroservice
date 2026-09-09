using System.Text.Json;
using FluentAssertions;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.Gamification;
using SpeedReading.Application.StudentReading;
using SpeedReading.Application.Content;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingContractSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Exercise_session_metrics_keep_the_legacy_acronym_casing()
    {
        using var document = JsonDocument.Parse("{}");
        var result = new ExerciseSessionResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            0,
            100,
            60,
            90,
            100,
            300,
            250,
            200,
            10,
            [],
            false,
            null,
            document.RootElement.Clone(),
            "Tamamlandı.",
            null);

        var json = JsonSerializer.Serialize(result, JsonOptions);

        json.Should().Contain("\"rawWPM\":300");
        json.Should().Contain("\"weightedKDP\":200");
    }

    [Fact]
    public void Gamification_metrics_keep_the_legacy_acronym_casing()
    {
        var summary = new GamificationSummary(
            Guid.NewGuid(),
            900,
            9,
            0,
            100,
            "Okuyucu",
            "📖",
            2,
            3,
            null,
            1,
            10,
            30,
            DateTime.UtcNow,
            null);

        var json = JsonSerializer.Serialize(summary, JsonOptions);

        json.Should().Contain("\"totalXP\":900");
        json.Should().Contain("\"currentLevelXP\":0");
        json.Should().Contain("\"nextLevelXP\":100");
    }

    [Fact]
    public void Student_reading_questions_never_serialize_answer_keys()
    {
        var question = new StudentReadingQuestion(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Soru",
            1,
            2,
            1,
            null,
            "A",
            "B",
            "C",
            "D",
            1);

        var json = JsonSerializer.Serialize(question, JsonOptions);

        json.ToLowerInvariant().Should().NotContain("correctanswer");
    }

    [Fact]
    public void Student_exercise_configuration_removes_nested_answer_keys()
    {
        const string configuration = "{\"questions\":[{\"prompt\":\"2+2?\",\"correctAnswer\":\"4\",\"correct_answer\":\"4\",\"correctOption\":\"A\",\"answer_key\":\"A\",\"is_correct\":true}],\"positionTargetIndices\":[1,3],\"position_target_indices\":[1,3],\"nLevel\":2,\"focusNLevel\":2,\"engineType\":\"quiz\"}";

        var sanitized = SpeedReadingContentSecurity.SanitizeExerciseConfiguration(configuration);

        sanitized.Should().Contain("\"engineType\":\"quiz\"");
        sanitized.ToLowerInvariant().Should().NotContain("correctanswer");
        sanitized.ToLowerInvariant().Should().NotContain("correctoption");
        sanitized.ToLowerInvariant().Should().NotContain("correct_answer");
        sanitized.ToLowerInvariant().Should().NotContain("answerkey");
        sanitized.ToLowerInvariant().Should().NotContain("answer_key");
        sanitized.ToLowerInvariant().Should().NotContain("iscorrect");
        sanitized.ToLowerInvariant().Should().NotContain("is_correct");
        sanitized.ToLowerInvariant().Should().NotContain("positiontargetindices");
        sanitized.ToLowerInvariant().Should().NotContain("position_target_indices");
        sanitized.Should().Contain("\"nLevel\":2");
        sanitized.Should().Contain("\"focusNLevel\":2");
    }

    [Fact]
    public void Invalid_student_exercise_configuration_falls_back_to_empty_object()
    {
        SpeedReadingContentSecurity.SanitizeExerciseConfiguration("not-json").Should().Be("{}");
    }

    [Fact]
    public void Assessment_payload_also_hides_explanations()
    {
        using var document = JsonDocument.Parse(
            "{\"questions\":[{\"explanation\":\"İpucu\",\"correctAnswer\":\"A\"}],\"label\":\"assessment\"}");

        var sanitized = SpeedReadingContentSecurity.SanitizeAssessmentJson(document.RootElement).GetRawText();

        sanitized.Should().Contain("\"label\":\"assessment\"");
        sanitized.ToLowerInvariant().Should().NotContain("explanation");
        sanitized.ToLowerInvariant().Should().NotContain("correctanswer");
    }

    [Fact]
    public void Focus_assessment_payload_hides_complete_sequences()
    {
        using var document = JsonDocument.Parse(
            "{\"focusMode\":\"position\",\"focusNLevel\":2,\"positionSequence\":[1,2,1],\"wordSequence\":[\"a\",\"b\"],\"positionTargetIndices\":[2]}");

        var sanitized = SpeedReadingContentSecurity.SanitizeFocusAssessmentJson(document.RootElement).GetRawText();

        sanitized.Should().Contain("\"focusMode\":\"position\"");
        sanitized.Should().Contain("\"focusNLevel\":2");
        sanitized.ToLowerInvariant().Should().NotContain("positionsequence");
        sanitized.ToLowerInvariant().Should().NotContain("wordsequence");
        sanitized.ToLowerInvariant().Should().NotContain("positiontargetindices");
    }

    [Fact]
    public void Timed_out_question_actions_keep_the_timeout_marker_on_the_wire()
    {
        var action = new ExerciseActionRequest
        {
            Action = "answer_question",
            QuestionId = Guid.NewGuid(),
            Answer = string.Empty,
            IsTimeout = true
        };

        var json = JsonSerializer.Serialize(action, JsonOptions);

        json.Should().Contain("\"isTimeout\":true");
    }

    [Fact]
    public void Practice_payload_keeps_explanations_for_feedback()
    {
        using var document = JsonDocument.Parse("{\"explanation\":\"İpucu\"}");

        var sanitized = SpeedReadingContentSecurity.SanitizeStudentJson(document.RootElement).GetRawText();

        using var sanitizedDocument = JsonDocument.Parse(sanitized);
        sanitizedDocument.RootElement.GetProperty("explanation").GetString().Should().Be("İpucu");
    }
}
