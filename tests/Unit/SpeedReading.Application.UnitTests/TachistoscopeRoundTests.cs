using FluentAssertions;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Application.UnitTests;

public sealed class TachistoscopeRoundTests
{
    [Fact]
    public void Selection_prefers_least_used_words_of_the_appropriate_length()
    {
        var state = new TachistoscopeState { TargetLength = 3, AdaptiveEnabled = false, Pool = ["bir", "iki", "ses"] };
        state.Record("bir", "bir", 100);
        state.Record("iki", "iki", 100);
        for (var i = 0; i < 50; i++) state.SelectStimulus().Should().Be("ses");
    }

    [Fact]
    public void Legacy_assessment_is_restarted_without_replacing_its_unique_session()
    {
        var attemptId = Guid.NewGuid();
        var session = ExerciseSession.Start(Guid.NewGuid(), Guid.NewGuid(), null, 1,
            DateTime.UtcNow.AddMinutes(-1), null, assessmentAttemptId: attemptId);
        session.SetState("{\"old\":true}");
        session.Advance(true);
        session.RestartForVerification(3, null, "{\"tachistoscope\":{}}", DateTime.UtcNow);
        session.AssessmentAttemptId.Should().Be(attemptId);
        session.TotalSteps.Should().Be(3);
        session.CurrentStep.Should().Be(0);
        session.CorrectCount.Should().Be(0);
        session.CustomDataJson.Should().Contain("old");
        session.SessionDataJson.Should().Contain("tachistoscope");
    }

    [Theory]
    [InlineData("number", "2468")]
    [InlineData("letter", "ABC")]
    public void Custom_content_is_used_for_all_content_types(string type, string stimulus)
    {
        var state = new TachistoscopeState { ContentType = type, Source = "custom", Pool = [stimulus] };
        state.SelectStimulus().Should().Be(stimulus);
    }

    [Fact]
    public void Adaptation_waits_for_a_complete_three_trial_block_and_changes_only_duration()
    {
        var state = new TachistoscopeState { DisplayDurationMs = 500, TargetLength = 3, Pool = ["bir", "masa", "kalem"] };
        state.Record("bir", "bir", 100);
        state.Record("bir", "bir", 100);
        state.TargetLength.Should().Be(3);
        state.DisplayDurationMs.Should().Be(500);
        state.Record("bir", "bir", 100);
        state.TargetLength.Should().Be(3);
        state.DisplayDurationMs.Should().Be(425);
    }

    [Theory]
    [InlineData(0, 575)]
    [InlineData(1, 575)]
    [InlineData(2, 500)]
    [InlineData(3, 425)]
    public void Completed_blocks_adjust_duration_from_accuracy(int correctCount, int expectedDuration)
    {
        var state = new TachistoscopeState { DisplayDurationMs = 500, InitialDurationMs = 500, TargetLength = 3 };
        for (var i = 0; i < 3; i++) state.Record("123", i < correctCount ? "123" : "wrong", 100);
        state.DisplayDurationMs.Should().Be(expectedDuration);
        state.TargetLength.Should().Be(3);
    }

    [Fact]
    public void Final_partial_block_does_not_change_settings()
    {
        var state = new TachistoscopeState { DisplayDurationMs = 500, TargetLength = 3 };
        for (var i = 0; i < 2; i++) state.Record("123", "123", 100);
        state.DisplayDurationMs.Should().Be(500);
        state.TargetLength.Should().Be(3);
    }

    [Fact]
    public void Disabled_adaptation_keeps_explicit_settings()
    {
        var state = new TachistoscopeState { AdaptiveEnabled = false, DisplayDurationMs = 600, TargetLength = 3 };
        for (var i = 0; i < 5; i++) state.Record("İZ", "iz", 100);
        state.DisplayDurationMs.Should().Be(600);
        state.TargetLength.Should().Be(3);
        state.Trials.Should().OnlyContain(item => item.IsCorrect);
    }

    [Fact]
    public void Selection_prefers_the_target_length_without_repeating_the_last_word()
    {
        var state = new TachistoscopeState { TargetLength = 4, LastStimulus = "masa", Pool = ["bir", "masa", "kapı", "kalem"] };
        state.SelectStimulus().Should().Be("kapı");
    }

    [Theory]
    [InlineData("kâğıt", "kağıt")]
    [InlineData("KÂĞIT", "kağıt")]
    [InlineData("kağıt", "kâğıt")]
    [InlineData("hâlâ", "hala")]
    [InlineData("millî", "milli")]
    [InlineData("Îlan", "ilan")]
    [InlineData("mahkûm", "mahkum")]
    [InlineData("ÛMİT", "umit")]
    [InlineData("kâğıt  kalem", "kağıt kalem")]
    public void Circumflex_letters_match_their_plain_form(string stimulus, string answer)
    {
        var state = new TachistoscopeState { AdaptiveEnabled = false };
        state.Record(stimulus, answer, 100).Should().BeTrue();
    }

    [Theory]
    [InlineData("kağıt", "kagıt")]
    [InlineData("ılık", "ilik")]
    [InlineData("kâğıt", "kâğit")]
    public void Other_letters_still_have_to_match_exactly(string stimulus, string answer)
    {
        var state = new TachistoscopeState { AdaptiveEnabled = false };
        state.Record(stimulus, answer, 100).Should().BeFalse();
    }
}
