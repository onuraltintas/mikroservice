using System.Reflection;
using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.StudentReading;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.AdaptiveText;
using SpeedReading.Application.Content;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Gamification;
using SpeedReading.Domain.Profiles;
using SpeedReading.Domain.Sessions;
using SpeedReading.Domain.Vocabulary;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class StudentReadingPersistenceTests
{
    [Fact]
    public async Task Vocabulary_quiz_uses_server_owned_direction_and_word_count()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var vocabularyItemId = Guid.NewGuid();
        var secondVocabularyItemId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(
            typeId, "Kelime", "Kelime testi", "vocabulary_builder"));
        context.Exercises.Add(Exercise.Create(
            "Kelime testi",
            "vocabulary_builder",
            """{"engineType":"vocabulary_builder","vocabulary":{"category":"Genel","count":2,"difficultyLevel":1},"mode":"quiz","quizType":"word_to_definition","totalSteps":1}""",
            1,
            studentId,
            typeId,
            id: exerciseId));
        context.VocabularyItems.AddRange(
            VocabularyItem.Create(
                vocabularyItemId, "merak", "Bir şeyi anlama ve öğrenme isteği",
                null, null, null, "Genel", 1, null, studentId, DateTime.UtcNow),
            VocabularyItem.Create(
                secondVocabularyItemId, "özen", "Dikkatli ve titiz çalışma",
                null, null, null, "Genel", 1, null, studentId, DateTime.UtcNow));
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var started = await service.StartAsync(
            studentId,
            new StartExerciseSessionRequest { ExerciseId = exerciseId },
            CancellationToken.None);
        started.TotalSteps.Should().Be(2);
        started.InitialData.GetProperty("vocabularyQuizType").GetString()
            .Should().Be("word_to_definition");
        var response = await service.ValidateActionAsync(
            studentId,
            started.SessionId,
            new ExerciseActionRequest
            {
                Action = "vocabulary_review",
                Answer = "B",
                CustomData = new Dictionary<string, JsonElement>
                {
                    ["vocabularyItemId"] = JsonSerializer.SerializeToElement(vocabularyItemId),
                    ["reviewKind"] = JsonSerializer.SerializeToElement("quiz"),
                    ["questionType"] = JsonSerializer.SerializeToElement("word"),
                    ["selectedAnswer"] = JsonSerializer.SerializeToElement("Yanlış tanım"),
                    ["isCorrect"] = JsonSerializer.SerializeToElement(true)
                }
            },
            CancellationToken.None);

        response.IsCorrect.Should().BeFalse();
        var session = await context.ExerciseSessions.SingleAsync(item => item.Id == started.SessionId);
        session.CorrectCount.Should().Be(0);
        session.IncorrectCount.Should().Be(1);

        var incomplete = () => service.CompleteAsync(
            studentId,
            started.SessionId,
            new CompleteExerciseSessionRequest(),
            CancellationToken.None);
        await incomplete.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*vocabulary rounds*");

        var forgedDirection = await service.ValidateActionAsync(
            studentId,
            started.SessionId,
            new ExerciseActionRequest
            {
                Action = "vocabulary_review",
                CustomData = new Dictionary<string, JsonElement>
                {
                    ["vocabularyItemId"] = JsonSerializer.SerializeToElement(secondVocabularyItemId),
                    ["reviewKind"] = JsonSerializer.SerializeToElement("quiz"),
                    ["questionType"] = JsonSerializer.SerializeToElement("definition"),
                    ["selectedAnswer"] = JsonSerializer.SerializeToElement("özen")
                }
            },
            CancellationToken.None);
        forgedDirection.IsValid.Should().BeFalse();
        forgedDirection.IsCorrect.Should().BeFalse();
        forgedDirection.Message.Should().Contain("yönü");

        var sessionAfterForgery = await context.ExerciseSessions.SingleAsync(item => item.Id == started.SessionId);
        sessionAfterForgery.CorrectCount.Should().Be(0);
        sessionAfterForgery.IncorrectCount.Should().Be(1);
        using (var sessionState = JsonDocument.Parse(sessionAfterForgery.SessionDataJson))
        {
            sessionState.RootElement.GetProperty("answers").GetArrayLength().Should().Be(1);
            sessionState.RootElement.GetProperty("answers")[0].GetProperty("questionId").GetGuid()
                .Should().Be(vocabularyItemId);
        }

        var stillIncomplete = () => service.CompleteAsync(
            studentId,
            started.SessionId,
            new CompleteExerciseSessionRequest(),
            CancellationToken.None);
        await stillIncomplete.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*vocabulary rounds*");
    }

    [Fact]
    public async Task Vocabulary_learning_completion_does_not_increment_verified_gamification()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var vocabularyItemId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(
            typeId, "Kelime", "Kelime öğrenme", "vocabulary_builder"));
        context.Exercises.Add(Exercise.Create(
            "Kelime öğrenme",
            "vocabulary_builder",
            """{"engineType":"vocabulary_builder","vocabulary":{"category":"Genel","count":1,"difficultyLevel":1},"mode":"learning"}""",
            1,
            studentId,
            typeId,
            id: exerciseId));
        context.VocabularyItems.Add(VocabularyItem.Create(
            vocabularyItemId, "merak", "Bir şeyi anlama ve öğrenme isteği",
            null, null, null, "Genel", 1, null, studentId, DateTime.UtcNow));
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var started = await service.StartAsync(
            studentId,
            new StartExerciseSessionRequest { ExerciseId = exerciseId },
            CancellationToken.None);
        var review = await service.ValidateActionAsync(
            studentId,
            started.SessionId,
            new ExerciseActionRequest
            {
                Action = "vocabulary_review",
                CustomData = new Dictionary<string, JsonElement>
                {
                    ["vocabularyItemId"] = JsonSerializer.SerializeToElement(vocabularyItemId),
                    ["reviewKind"] = JsonSerializer.SerializeToElement("known")
                }
            },
            CancellationToken.None);
        review.IsValid.Should().BeTrue();

        await service.CompleteAsync(
            studentId,
            started.SessionId,
            new CompleteExerciseSessionRequest(),
            CancellationToken.None);

        var stats = await context.UserGamifications.SingleOrDefaultAsync(item => item.UserId == studentId);
        stats.Should().BeNull();
    }

    [Fact]
    public async Task Unsupported_action_does_not_advance_a_generic_exercise_session()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(
            typeId, "Göz hareketi", "Hareketli hedef", "motion_path"));
        context.Exercises.Add(Exercise.Create(
            "Hareketli hedef", "motion_path", "{\"totalSteps\":5}", 1,
            studentId, typeId, id: exerciseId));
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var started = await service.StartAsync(
            studentId,
            new StartExerciseSessionRequest { ExerciseId = exerciseId },
            CancellationToken.None);

        var response = await service.ValidateActionAsync(
            studentId,
            started.SessionId,
            new ExerciseActionRequest { Action = "invented_client_success" },
            CancellationToken.None);

        response.IsValid.Should().BeFalse();
        var session = await context.ExerciseSessions.SingleAsync(item => item.Id == started.SessionId);
        session.CurrentStep.Should().Be(0);
    }

    [Fact]
    public async Task Grid_session_uses_the_validated_nested_grid_configuration()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(
            typeId, "Schulte", "Grid", "grid_interaction"));
        context.Exercises.Add(Exercise.Create(
            "İç ayarlı grid",
            "grid_interaction",
            """{"engineType":"grid_interaction","engineConfig":{"engineType":"grid_interaction","gridSize":7,"sequenceType":"numeric"}}""",
            1,
            studentId,
            typeId,
            id: exerciseId));
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var started = await service.StartAsync(
            studentId,
            new StartExerciseSessionRequest { ExerciseId = exerciseId },
            CancellationToken.None);

        started.TotalSteps.Should().Be(49);
        started.InitialData.GetProperty("gridSize").GetInt32().Should().Be(7);
        started.InitialData.GetProperty("grid").GetArrayLength().Should().Be(7);
    }

    [Fact]
    public async Task Focus_session_uses_nested_then_root_configuration_fallbacks()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(typeId, "Odak", "N-back", "focus"));
        context.Exercises.Add(Exercise.Create(
            "Odak",
            "focus",
            """{"engineType":"focus","nLevel":5,"gridSize":7,"engineConfig":{"engineType":"focus","mode":"position","speedMs":500,"positionSequence":[1,2,1,2,1,2]}}""",
            1,
            studentId,
            typeId,
            id: exerciseId));
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var started = await service.StartAsync(
            studentId,
            new StartExerciseSessionRequest { ExerciseId = exerciseId },
            CancellationToken.None);

        started.InitialData.GetProperty("focusNLevel").GetInt32().Should().Be(5);
        started.InitialData.GetProperty("focusSpeedMs").GetInt32().Should().Be(500);
        started.InitialData.GetProperty("gridSize").GetInt32().Should().Be(7);
        started.TotalSteps.Should().Be(6);
    }

    [Fact]
    public async Task Observation_only_motion_path_completion_does_not_increment_verified_gamification()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(
            typeId, "Göz hareketi", "Hareketli hedef", "motion_path"));
        context.Exercises.Add(Exercise.Create(
            "Hareketli hedef", "motion_path", "{\"totalSteps\":5}", 1,
            studentId, typeId, id: exerciseId));
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var started = await service.StartAsync(
            studentId,
            new StartExerciseSessionRequest { ExerciseId = exerciseId },
            CancellationToken.None);

        var result = await service.CompleteAsync(
            studentId,
            started.SessionId,
            new CompleteExerciseSessionRequest(),
            CancellationToken.None);

        result.MeasurementStatus.Should().Be(nameof(SpeedReadingMeasurementStatus.NotMeasured));
        var stats = await context.UserGamifications.SingleOrDefaultAsync(item => item.UserId == studentId);
        stats.Should().BeNull();
    }

    [Fact]
    public async Task Reading_text_details_hide_content_for_a_different_age_group()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var userAgeGroupId = Guid.NewGuid();
        var otherAgeGroupId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        context.UserProfiles.Add(SpeedReadingUserProfile.Import(
            Guid.NewGuid(), userId, 1, 150, 70, 15, userAgeGroupId, null, true,
            DateTime.UtcNow, userId.ToString(), null, null));
        context.ReadingTexts.Add(ReadingText.Create(
            textId, "Başka yaş grubu", "Bu içerik farklı bir yaş grubuna aittir.",
            difficultyLevel: 1, targetAgeGroupId: otherAgeGroupId));
        await context.SaveChangesAsync();

        var catalogType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingCatalog",
            throwOnError: true)!;
        var catalog = (ILegacySpeedReadingCatalog)Activator.CreateInstance(
            catalogType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [context],
            culture: null)!;
        var result = await catalog.GetReadingTextAsync(
            textId,
            includeQuestions: true,
            includeInactive: false,
            includeAnswers: false,
            viewerUserId: userId,
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Catalog_reads_hide_age_restricted_content_when_profile_is_missing()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var globalTextId = Guid.NewGuid();
        context.ReadingTexts.AddRange(
            ReadingText.Create(
                textId, "Yaşa özel kısa metin", "Bu içerik profil olmadan görünmemelidir.",
                difficultyLevel: 1, targetAgeGroupId: Guid.NewGuid()),
            ReadingText.Create(
                globalTextId, "Genel kısa metin", "Bu genel içerik profil olmadan görülebilir.",
                difficultyLevel: 1));
        await context.SaveChangesAsync();
        var catalogType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingCatalog",
            throwOnError: true)!;
        var catalog = (ILegacySpeedReadingCatalog)Activator.CreateInstance(
            catalogType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [context],
            culture: null)!;

        var details = await catalog.GetReadingTextAsync(
            textId, true, false, false, userId, CancellationToken.None);
        var list = await catalog.GetReadingTextsAsync(
            null, null, null, null, false, null, true, userId, CancellationToken.None);
        var shortTexts = await catalog.GetShortReadingTextsAsync(
            10, userId, CancellationToken.None);

        details.Should().BeNull();
        list.Select(item => item.Id).Should().Equal(globalTextId);
        shortTexts.Select(item => item.Id).Should().Equal(globalTextId);
    }

    [Fact]
    public async Task Automatic_text_selection_prefers_exercise_and_difficulty_match_with_scorable_questions()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var unsuitableTextId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var suitableTextId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        context.ExerciseTypes.Add(ExerciseType.Create(
            typeId, "Okuma", "Okuma", "Comprehension"));
        context.Exercises.Add(Exercise.Create(
            "Seviyeye uygun okuma", "reading", "{}", 3, studentId, typeId, id: exerciseId));
        context.ReadingTexts.AddRange(
            ReadingText.Create(
                unsuitableTextId, "Uygun olmayan", "Bu metin yanlış seviyededir.",
                difficultyLevel: 1),
            ReadingText.Create(
                suitableTextId, "Uygun metin", "Bu metin egzersize ve seviyeye uygundur.",
                exerciseId: exerciseId, difficultyLevel: 3));
        context.ReadingQuestions.AddRange(
            ReadingQuestion.Create(
                Guid.NewGuid(), unsuitableTextId, "Bu metin hangi seviyededir?", " a ",
                orderIndex: 0, type: 1, bloomLevel: 1,
                optionA: "Bir", optionB: "İki", optionC: "Üç", optionD: "Dört"),
            ReadingQuestion.Create(
                Guid.NewGuid(), suitableTextId, "Hangi metin uygundur?", "A",
                orderIndex: 0, type: 1, bloomLevel: 1,
                optionA: "Seviyeye uygun", optionB: "Uygun değil", optionC: "Hiçbiri", optionD: "Bilinmiyor"));
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var started = await service.StartAsync(
            studentId,
            new StartExerciseSessionRequest { ExerciseId = exerciseId },
            CancellationToken.None);

        var session = await context.ExerciseSessions.SingleAsync(item => item.Id == started.SessionId);
        session.ReadingTextId.Should().Be(suitableTextId);
    }

    [Fact]
    public async Task Universal_reading_completion_projects_answer_details_to_reading_history()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var exerciseTypeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var firstQuestionId = Guid.NewGuid();
        var secondQuestionId = Guid.NewGuid();
        var type = ExerciseType.Create(
            exerciseTypeId,
            "Comprehension",
            "Anlama",
            "reading_comprehension");
        var exercise = Exercise.Create(
            "Okuma anlama",
            "comprehension",
            "{}",
            difficultyLevel: 1,
            creatorId: studentId,
            exerciseTypeId,
            id: exerciseId);
        var text = ReadingText.Create(
            textId,
            "Birleşik akış metni",
            "Bu metin normal öğrenci akışının cevap ayrıntılarını doğrular.",
            category: "Test",
            difficultyLevel: 1,
            exerciseId: exerciseId);
        var firstQuestion = ReadingQuestion.Create(
            firstQuestionId,
            textId,
            "Birinci soru",
            "A",
            orderIndex: 0,
            type: 1,
            bloomLevel: 2,
            optionA: "Doğru",
            optionB: "Yanlış",
            optionC: "Diğer",
            optionD: "Son");
        var secondQuestion = ReadingQuestion.Create(
            secondQuestionId,
            textId,
            "İkinci soru",
            "B",
            orderIndex: 1,
            type: 2,
            bloomLevel: 4,
            optionA: "Yanlış",
            optionB: "Doğru",
            optionC: "Diğer",
            optionD: "Son");
        context.ExerciseTypes.Add(type);
        context.Exercises.Add(exercise);
        context.ReadingTexts.Add(text);
        context.ReadingQuestions.AddRange(firstQuestion, secondQuestion);
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var started = await service.StartAsync(
            studentId,
            new StartExerciseSessionRequest
            {
                ExerciseId = exerciseId,
                ReadingTextId = textId
            },
            CancellationToken.None);

        var invalidAction = await service.ValidateActionAsync(
            studentId,
            started.SessionId,
            new ExerciseActionRequest
            {
                Action = "answer_question",
                QuestionId = firstQuestionId,
                Answer = "E"
            },
            CancellationToken.None);
        invalidAction.IsValid.Should().BeFalse();
        context.ExerciseSessionAnswers.Should().BeEmpty();

        await service.ValidateActionAsync(
            studentId,
            started.SessionId,
            new ExerciseActionRequest { Action = "start_reading" },
            CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(3.1));
        context.ChangeTracker.Clear();
        await service.ValidateActionAsync(
            studentId,
            started.SessionId,
            new ExerciseActionRequest { Action = "finish_reading" },
            CancellationToken.None);
        context.ChangeTracker.Clear();
        await service.ValidateActionAsync(
            studentId,
            started.SessionId,
            new ExerciseActionRequest
            {
                Action = "answer_question",
                QuestionId = firstQuestionId,
                Answer = "A"
            },
            CancellationToken.None);
        context.ChangeTracker.Clear();
        await service.ValidateActionAsync(
            studentId,
            started.SessionId,
            new ExerciseActionRequest
            {
                Action = "answer_question",
                QuestionId = secondQuestionId,
                Answer = "A"
            },
            CancellationToken.None);
        context.ChangeTracker.Clear();

        await service.CompleteAsync(
            studentId,
            started.SessionId,
            new CompleteExerciseSessionRequest(),
            CancellationToken.None);

        context.ReadingSessions.Should().ContainSingle()
            .Which.Id.Should().Be(started.SessionId);
        context.ReadingSessionAnswers
            .OrderBy(item => item.OrderIndex)
            .Select(item => new { item.QuestionType, item.BloomLevel, item.SelectedAnswer, item.IsCorrect })
            .Should().BeEquivalentTo(
            [
                new { QuestionType = 1, BloomLevel = 2, SelectedAnswer = "A", IsCorrect = true },
                new { QuestionType = 2, BloomLevel = 4, SelectedAnswer = "A", IsCorrect = false }
            ], options => options.WithStrictOrdering());

        var readingService = CreateService(context);
        var details = await readingService.GetSessionDetailsAsync(
            studentId,
            started.SessionId,
            CancellationToken.None);
        details.Should().NotBeNull();
        details!.IsMeasured.Should().BeTrue();
        details.Answers.Select(item => new { item.QuestionType, item.BloomLevel, item.SelectedAnswer, item.IsCorrect })
            .Should().BeEquivalentTo(
            [
                new { QuestionType = 1, BloomLevel = 2, SelectedAnswer = "A", IsCorrect = true },
                new { QuestionType = 2, BloomLevel = 4, SelectedAnswer = "A", IsCorrect = false }
            ], options => options.WithStrictOrdering());

        var replay = await service.CompleteAsync(
            studentId,
            started.SessionId,
            new CompleteExerciseSessionRequest(),
            CancellationToken.None);
        replay.SessionId.Should().Be(started.SessionId);
        context.ExerciseSessionResults.Should().ContainSingle();

        // A session without a validated reading interval is not a measured
        // reading result and must not add a zero-WPM row to reading analytics.
        context.ChangeTracker.Clear();
        var unmeasured = await service.StartAsync(
            studentId,
            new StartExerciseSessionRequest
            {
                ExerciseId = exerciseId,
                ReadingTextId = textId
            },
            CancellationToken.None);
        await service.ValidateActionAsync(
            studentId,
            unmeasured.SessionId,
            new ExerciseActionRequest
            {
                Action = "answer_question",
                QuestionId = firstQuestionId,
                Answer = "A"
            },
            CancellationToken.None);
        context.ChangeTracker.Clear();
        await service.ValidateActionAsync(
            studentId,
            unmeasured.SessionId,
            new ExerciseActionRequest
            {
                Action = "answer_question",
                QuestionId = secondQuestionId,
                Answer = "B"
            },
            CancellationToken.None);
        context.ChangeTracker.Clear();
        await service.CompleteAsync(
            studentId,
            unmeasured.SessionId,
            new CompleteExerciseSessionRequest(),
            CancellationToken.None);

        context.ReadingSessions.Should().HaveCount(2);
        context.ReadingSessions.Count(item => item.IsMeasured).Should().Be(1);
        context.ReadingSessions.Single(item => !item.IsMeasured).CalculatedWpm.Should().Be(0);
        context.ExerciseSessionResults.Should().HaveCount(2);
    }

    [Fact]
    public async Task Universal_completion_rejects_non_option_question_answers()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var exerciseTypeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var firstQuestionId = Guid.NewGuid();
        var secondQuestionId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(
            exerciseTypeId, "Comprehension", "Anlama", "reading_comprehension"));
        context.Exercises.Add(Exercise.Create(
            "Okuma anlama", "comprehension", "{}", difficultyLevel: 1,
            creatorId: studentId, exerciseTypeId, id: exerciseId));
        context.ReadingTexts.Add(ReadingText.Create(
            textId, "Cevap doğrulama metni", "Bu metin seçenek doğrulamasını sınar.",
            category: "Test", difficultyLevel: 1, exerciseId: exerciseId));
        context.ReadingQuestions.AddRange(
            ReadingQuestion.Create(
                firstQuestionId, textId, "Birinci soru", "A", orderIndex: 0,
                type: 1, bloomLevel: 2, optionA: "Bir", optionB: "İki", optionC: "Üç", optionD: "Dört"),
            ReadingQuestion.Create(
                secondQuestionId, textId, "İkinci soru", "B", orderIndex: 1,
                type: 2, bloomLevel: 3, optionA: "Bir", optionB: "İki", optionC: "Üç", optionD: "Dört"));
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var started = await service.StartAsync(
            studentId,
            new StartExerciseSessionRequest { ExerciseId = exerciseId, ReadingTextId = textId },
            CancellationToken.None);

        var act = () => service.CompleteAsync(
            studentId,
            started.SessionId,
            new CompleteExerciseSessionRequest
            {
                QuestionAnswers =
                [
                    new ExerciseQuestionAnswer { QuestionId = firstQuestionId, Answer = "E" },
                    new ExerciseQuestionAnswer { QuestionId = secondQuestionId, Answer = "B" }
                ]
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*A, B, C veya D*");
        context.ExerciseSessionResults.Should().BeEmpty();
        context.ReadingSessions.Should().BeEmpty();
        context.ReadingSessionAnswers.Should().BeEmpty();
    }

    [Fact]
    public async Task Universal_completion_merges_partial_question_answers_with_actions()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var exerciseTypeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var firstQuestionId = Guid.NewGuid();
        var secondQuestionId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(
            exerciseTypeId, "Comprehension", "Anlama", "reading_comprehension"));
        context.Exercises.Add(Exercise.Create(
            "Kısmi cevap akışı", "comprehension", "{}", difficultyLevel: 1,
            creatorId: studentId, exerciseTypeId, id: exerciseId));
        context.ReadingTexts.Add(ReadingText.Create(
            textId, "Kısmi cevap metni", "Bu metin kısmi cevap birleştirmesini sınar.",
            category: "Test", difficultyLevel: 1, exerciseId: exerciseId));
        context.ReadingQuestions.AddRange(
            ReadingQuestion.Create(
                firstQuestionId, textId, "Birinci soru", "A", orderIndex: 0,
                type: 1, bloomLevel: 2, optionA: "Bir", optionB: "İki", optionC: "Üç", optionD: "Dört"),
            ReadingQuestion.Create(
                secondQuestionId, textId, "İkinci soru", "B", orderIndex: 1,
                type: 2, bloomLevel: 3, optionA: "Bir", optionB: "İki", optionC: "Üç", optionD: "Dört"));
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var started = await service.StartAsync(
            studentId,
            new StartExerciseSessionRequest { ExerciseId = exerciseId, ReadingTextId = textId },
            CancellationToken.None);
        await service.ValidateActionAsync(
            studentId,
            started.SessionId,
            new ExerciseActionRequest
            {
                Action = "answer_question",
                QuestionId = firstQuestionId,
                Answer = "A"
            },
            CancellationToken.None);
        context.ChangeTracker.Clear();

        var completion = await service.CompleteAsync(
            studentId,
            started.SessionId,
            new CompleteExerciseSessionRequest
            {
                QuestionAnswers =
                [new ExerciseQuestionAnswer { QuestionId = secondQuestionId, Answer = "B" }]
            },
            CancellationToken.None);

        completion.CorrectCount.Should().Be(2);
        completion.IncorrectCount.Should().Be(0);
        context.ReadingSessionAnswers.Should().HaveCount(2);
        context.ReadingSessionAnswers.Select(item => item.QuestionId)
            .Should().Contain([firstQuestionId, secondQuestionId]);
    }

    [Fact]
    public async Task Universal_action_with_the_same_id_is_idempotent_when_requests_race()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var firstContext = CreateContext(databaseName);
        await using var secondContext = CreateContext(databaseName);
        var studentId = Guid.NewGuid();
        var exerciseTypeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        firstContext.ExerciseTypes.Add(ExerciseType.Create(
            exerciseTypeId, "Comprehension", "Anlama", "reading_comprehension"));
        firstContext.Exercises.Add(Exercise.Create(
            "Eşzamanlı cevap", "comprehension", "{}", difficultyLevel: 1,
            creatorId: studentId, exerciseTypeId, id: exerciseId));
        firstContext.ReadingTexts.Add(ReadingText.Create(
            textId, "Eşzamanlı metin", "Aynı action kimliğinin tek kez işlenmesini sınar.",
            category: "Test", difficultyLevel: 1, exerciseId: exerciseId));
        firstContext.ReadingQuestions.Add(ReadingQuestion.Create(
            questionId, textId, "Soru", "A", orderIndex: 0,
            type: 1, bloomLevel: 2, optionA: "Doğru", optionB: "Yanlış", optionC: "Diğer", optionD: "Son"));
        await firstContext.SaveChangesAsync();

        var firstService = CreateExerciseSessionService(firstContext);
        var started = await firstService.StartAsync(
            studentId,
            new StartExerciseSessionRequest { ExerciseId = exerciseId, ReadingTextId = textId },
            CancellationToken.None);
        var secondService = CreateExerciseSessionService(secondContext);
        var request = new ExerciseActionRequest
        {
            Action = "answer_question",
            QuestionId = questionId,
            Answer = "A",
            ActionId = Guid.NewGuid()
        };

        var responses = await Task.WhenAll(
            firstService.ValidateActionAsync(studentId, started.SessionId, request, CancellationToken.None),
            secondService.ValidateActionAsync(studentId, started.SessionId, request, CancellationToken.None));

        responses.Should().OnlyContain(item => item.IsValid);
        await using var verificationContext = CreateContext(databaseName);
        verificationContext.ExerciseSessionAnswers
            .Count(item => item.SessionId == started.SessionId && item.QuestionId == questionId)
            .Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_completions_preserve_both_gamification_updates()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var setupContext = CreateContext(databaseName);
        var studentId = Guid.NewGuid();
        var firstTypeId = Guid.NewGuid();
        var secondTypeId = Guid.NewGuid();
        var firstExerciseId = Guid.NewGuid();
        var secondExerciseId = Guid.NewGuid();
        var firstTextId = Guid.NewGuid();
        var secondTextId = Guid.NewGuid();
        var firstQuestionId = Guid.NewGuid();
        var secondQuestionId = Guid.NewGuid();
        setupContext.ExerciseTypes.AddRange(
            ExerciseType.Create(firstTypeId, "Anlama 1", "Anlama", "reading_comprehension"),
            ExerciseType.Create(secondTypeId, "Anlama 2", "Anlama", "reading_comprehension"));
        setupContext.Exercises.AddRange(
            Exercise.Create("Birinci egzersiz", "reading_comprehension", "{}", 1, studentId, firstTypeId, id: firstExerciseId),
            Exercise.Create("İkinci egzersiz", "reading_comprehension", "{}", 1, studentId, secondTypeId, id: secondExerciseId));
        setupContext.ReadingTexts.AddRange(
            ReadingText.Create(firstTextId, "Birinci metin", "Birinci ölçülmüş eşzamanlı oturum metni.",
                category: "Test", difficultyLevel: 1, exerciseId: firstExerciseId),
            ReadingText.Create(secondTextId, "İkinci metin", "İkinci ölçülmüş eşzamanlı oturum metni.",
                category: "Test", difficultyLevel: 1, exerciseId: secondExerciseId));
        setupContext.ReadingQuestions.AddRange(
            ReadingQuestion.Create(firstQuestionId, firstTextId, "Birinci soru", "A", 0, 1, 1,
                optionA: "Doğru", optionB: "Yanlış", optionC: "Diğer", optionD: "Son"),
            ReadingQuestion.Create(secondQuestionId, secondTextId, "İkinci soru", "A", 0, 1, 1,
                optionA: "Doğru", optionB: "Yanlış", optionC: "Diğer", optionD: "Son"));
        setupContext.UserGamifications.Add(UserGamification.CreateDefault(
            Guid.NewGuid(), studentId, DateTime.UtcNow, studentId.ToString()));
        await setupContext.SaveChangesAsync();

        await using var firstContext = CreateContext(databaseName);
        await using var secondContext = CreateContext(databaseName);
        var firstService = CreateExerciseSessionService(firstContext);
        var secondService = CreateExerciseSessionService(secondContext);
        var firstStarted = await firstService.StartAsync(
            studentId,
            new StartExerciseSessionRequest { ExerciseId = firstExerciseId, ReadingTextId = firstTextId },
            CancellationToken.None);
        var secondStarted = await secondService.StartAsync(
            studentId,
            new StartExerciseSessionRequest { ExerciseId = secondExerciseId, ReadingTextId = secondTextId },
            CancellationToken.None);
        await firstService.ValidateActionAsync(
            studentId,
            firstStarted.SessionId,
            new ExerciseActionRequest { Action = "answer_question", QuestionId = firstQuestionId, Answer = "A" },
            CancellationToken.None);
        await secondService.ValidateActionAsync(
            studentId,
            secondStarted.SessionId,
            new ExerciseActionRequest { Action = "answer_question", QuestionId = secondQuestionId, Answer = "A" },
            CancellationToken.None);

        await Task.WhenAll(
            firstService.CompleteAsync(studentId, firstStarted.SessionId, new CompleteExerciseSessionRequest(), CancellationToken.None),
            secondService.CompleteAsync(studentId, secondStarted.SessionId, new CompleteExerciseSessionRequest(), CancellationToken.None));

        await using var verificationContext = CreateContext(databaseName);
        var stats = await verificationContext.UserGamifications.SingleAsync(item => item.UserId == studentId);
        stats.TotalActivitiesCompleted.Should().Be(2);
        stats.TotalExercisesCompleted.Should().Be(2);
    }

    [Fact]
    public async Task Assessment_start_rejects_a_malformed_content_snapshot_instead_of_using_live_catalog()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var exerciseTypeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(
            exerciseTypeId, "Okuma", "Okuma", "reading_comprehension"));
        context.Exercises.Add(Exercise.Create(
            "Canlı katalog egzersizi", "reading", "{}", difficultyLevel: 1,
            creatorId: studentId, exerciseTypeId, id: exerciseId));
        context.AssessmentAttempts.Add(AssessmentAttempt.Start(
            attemptId,
            studentId,
            AssessmentAttemptPhase.Baseline,
            "baseline-v1",
            "tr",
            null,
            1,
            DateTime.UtcNow,
            studentId.ToString()));
        context.AssessmentAttemptExercises.Add(AssessmentAttemptExercise.Pin(
            Guid.NewGuid(),
            attemptId,
            exerciseId,
            null,
            "comprehension",
            1,
            "{malformed",
            DateTime.UtcNow,
            studentId.ToString()));
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var act = () => service.StartAsync(
            studentId,
            new StartExerciseSessionRequest
            {
                ExerciseId = exerciseId,
                AssessmentAttemptId = attemptId
            },
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("SpeedReading.Assessment.ContentSnapshot.Invalid");
        exception.Which.Message.Should().Contain("snapshot");
        context.ExerciseSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Assessment_start_rejects_an_empty_content_snapshot_instead_of_using_live_catalog()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var exerciseTypeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(
            exerciseTypeId, "Okuma", "Okuma", "reading_comprehension"));
        context.Exercises.Add(Exercise.Create(
            "Canlı katalog egzersizi", "reading", "{}", difficultyLevel: 1,
            creatorId: studentId, exerciseTypeId, id: exerciseId));
        context.AssessmentAttempts.Add(AssessmentAttempt.Start(
            attemptId,
            studentId,
            AssessmentAttemptPhase.Baseline,
            "baseline-v1",
            "tr",
            null,
            1,
            DateTime.UtcNow,
            studentId.ToString()));
        context.AssessmentAttemptExercises.Add(AssessmentAttemptExercise.Pin(
            Guid.NewGuid(),
            attemptId,
            exerciseId,
            null,
            "comprehension",
            1,
            "{}",
            DateTime.UtcNow,
            studentId.ToString()));
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var act = () => service.StartAsync(
            studentId,
            new StartExerciseSessionRequest
            {
                ExerciseId = exerciseId,
                AssessmentAttemptId = attemptId
            },
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("SpeedReading.Assessment.ContentSnapshot.Invalid");
        exception.Which.Message.Should().Contain("snapshot");
        context.ExerciseSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Assessment_start_rejects_a_snapshot_with_missing_answer_key_without_a_server_error()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var exerciseTypeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(
            exerciseTypeId, "Okuma", "Okuma", "reading_comprehension"));
        context.Exercises.Add(Exercise.Create(
            "Snapshot egzersizi", "reading", "{}", difficultyLevel: 1,
            creatorId: studentId, exerciseTypeId, id: exerciseId));
        context.AssessmentAttempts.Add(AssessmentAttempt.Start(
            attemptId,
            studentId,
            AssessmentAttemptPhase.Baseline,
            "baseline-v1",
            "tr",
            null,
            1,
            DateTime.UtcNow,
            studentId.ToString()));
        context.AssessmentAttemptExercises.Add(AssessmentAttemptExercise.Pin(
            Guid.NewGuid(),
            attemptId,
            exerciseId,
            textId,
            "comprehension",
            1,
            $$"""
            {
              "version": 1,
              "exercise": {
                "id": "{{exerciseId}}",
                "title": "Snapshot egzersizi",
                "typeName": "Okuma",
                "configurationJson": "{}"
              },
              "readingText": {
                "id": "{{textId}}",
                "title": "Snapshot metni",
                "content": "Kısa bir metin",
                "wordCount": 3
              },
              "questions": [
                {
                  "readingTextId": "{{textId}}",
                  "id": "{{questionId}}",
                  "questionText": "Soru",
                  "optionA": "A",
                  "optionB": "B",
                  "optionC": "C",
                  "optionD": "D",
                  "bloomLevel": 1,
                  "difficultyLevel": 1,
                  "orderIndex": 0,
                  "questionType": 1
                }
              ]
            }
            """,
            DateTime.UtcNow,
            studentId.ToString()));
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var act = () => service.StartAsync(
            studentId,
            new StartExerciseSessionRequest
            {
                ExerciseId = exerciseId,
                AssessmentAttemptId = attemptId
            },
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Code.Should().Be("SpeedReading.Assessment.ContentSnapshot.Invalid");
        exception.Which.Message.Should().Contain("snapshot");
        context.ExerciseSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Universal_reading_history_uses_the_canonical_engine_type_when_name_is_custom()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var exerciseTypeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(
            exerciseTypeId,
            "Okuma akışı özel adı",
            "Okuma ve anlama",
            "reading_comprehension"));
        context.Exercises.Add(Exercise.Create(
            "Özel adla okuma",
            "custom-reading",
            "{}",
            difficultyLevel: 1,
            creatorId: studentId,
            exerciseTypeId,
            id: exerciseId));
        context.ReadingTexts.Add(ReadingText.Create(
            textId,
            "Özel ad metni",
            "Kanonik motor türü geçmiş bağlantısını doğrular.",
            category: "Test",
            difficultyLevel: 1,
            exerciseId: exerciseId));
        context.ReadingQuestions.Add(ReadingQuestion.Create(
            questionId,
            textId,
            "Metin neyi doğrular?",
            "A",
            orderIndex: 0,
            type: 1,
            bloomLevel: 1,
            optionA: "Kanonik motoru",
            optionB: "Başka bir motoru",
            optionC: "Hiçbir motoru",
            optionD: "Sadece rengi"));
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var started = await service.StartAsync(
            studentId,
            new StartExerciseSessionRequest
            {
                ExerciseId = exerciseId,
                ReadingTextId = textId
            },
            CancellationToken.None);
        await service.ValidateActionAsync(
            studentId,
            started.SessionId,
            new ExerciseActionRequest
            {
                Action = "answer_question",
                QuestionId = questionId,
                Answer = "A"
            },
            CancellationToken.None);

        await service.CompleteAsync(
            studentId,
            started.SessionId,
            new CompleteExerciseSessionRequest(),
            CancellationToken.None);

        context.ReadingSessions.Should().ContainSingle();
        context.ReadingSessionAnswers.Should().ContainSingle();
    }

    [Fact]
    public async Task Non_reading_exercise_with_a_reading_text_does_not_create_reading_history()
    {
        await using var context = CreateContext();
        var studentId = Guid.NewGuid();
        var exerciseTypeId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        context.ExerciseTypes.Add(ExerciseType.Create(
            exerciseTypeId,
            "Visualization",
            "Görselleştirme",
            "visualization"));
        context.Exercises.Add(Exercise.Create(
            "Görsel egzersiz",
            "Görsel egzersiz",
            "{}",
            difficultyLevel: 1,
            creatorId: studentId,
            exerciseTypeId,
            id: exerciseId));
        context.ReadingTexts.Add(ReadingText.Create(
            textId,
            "Bağlı metin",
            "Bu metin görsel egzersizin ilişkilendirilmiş içeriğidir.",
            category: "Test",
            difficultyLevel: 1,
            exerciseId: exerciseId));
        await context.SaveChangesAsync();

        var service = CreateExerciseSessionService(context);
        var started = await service.StartAsync(
            studentId,
            new StartExerciseSessionRequest
            {
                ExerciseId = exerciseId,
                ReadingTextId = textId
            },
            CancellationToken.None);

        await service.CompleteAsync(
            studentId,
            started.SessionId,
            new CompleteExerciseSessionRequest(),
            CancellationToken.None);

        context.ExerciseSessionResults.Should().ContainSingle();
        context.ReadingSessions.Should().BeEmpty();
        context.ReadingSessionAnswers.Should().BeEmpty();
    }

    [Fact]
    public async Task Complete_persists_answer_details_from_the_start_snapshot_and_is_idempotent()
    {
        await using var context = CreateContext();
        var textId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var questionOneId = Guid.NewGuid();
        var questionTwoId = Guid.NewGuid();
        var text = ReadingText.Create(
            textId,
            "Snapshot metni",
            "Bu metin soru cevap kayıtlarının korunmasını doğrular.",
            category: "Test",
            difficultyLevel: 2);
        var questionOne = ReadingQuestion.Create(
            questionOneId,
            textId,
            "Birinci soru",
            "A",
            orderIndex: 0,
            type: 1,
            bloomLevel: 2,
            optionA: "Birinci doğru",
            optionB: "Birinci yanlış",
            optionC: "Birinci seçenek",
            optionD: "Birinci diğer");
        var questionTwo = ReadingQuestion.Create(
            questionTwoId,
            textId,
            "İkinci soru",
            "B",
            orderIndex: 1,
            type: 2,
            bloomLevel: 4,
            optionA: "İkinci yanlış",
            optionB: "İkinci doğru",
            optionC: "İkinci seçenek",
            optionD: "İkinci diğer");
        context.ReadingTexts.Add(text);
        context.ReadingQuestions.AddRange(questionOne, questionTwo);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var started = await service.StartAsync(userId, textId, CancellationToken.None);
        started.Should().NotBeNull();
        started!.Questions.Should().HaveCount(2);

        questionOne.Update(
            "Birinci soru güncellendi",
            type: 1,
            bloomLevel: 2,
            difficultyLevel: 0,
            explanation: null,
            optionA: "Birinci doğru",
            optionB: "Birinci yanlış",
            optionC: "Birinci seçenek",
            optionD: "Birinci diğer",
            correctAnswer: "B",
            orderIndex: 0,
            actorId: userId,
            updatedAt: DateTime.UtcNow);
        await context.SaveChangesAsync();

        var request = new CompleteStudentReadingRequest(
            started.SessionId,
            TimeSpentSeconds: 20,
            ComprehensionScore: 50,
            Answers:
            [
                new StudentReadingAnswer(questionOneId, "A"),
                new StudentReadingAnswer(questionTwoId, "B")
            ]);

        var completion = await service.CompleteAsync(userId, textId, request, CancellationToken.None);

        completion.Should().NotBeNull();
        completion!.CorrectAnswers.Should().Be(2);
        completion.TotalQuestions.Should().Be(2);
        context.ReadingSessions.Single().IsMeasured.Should().BeFalse();
        context.ReadingSessionAnswers.Should().HaveCount(2);
        context.ReadingSessionAnswers
            .OrderBy(item => item.OrderIndex)
            .Select(item => new { item.QuestionType, item.BloomLevel, item.SelectedAnswer, item.IsCorrect })
            .Should().BeEquivalentTo(
            [
                new { QuestionType = 1, BloomLevel = 2, SelectedAnswer = "A", IsCorrect = true },
                new { QuestionType = 2, BloomLevel = 4, SelectedAnswer = "B", IsCorrect = true }
            ], options => options.WithStrictOrdering());

        var replay = await service.CompleteAsync(
            userId,
            textId,
            request with
            {
                Answers =
                [
                    new StudentReadingAnswer(questionOneId, "B"),
                    new StudentReadingAnswer(questionTwoId, "A")
                ]
            },
            CancellationToken.None);

        replay.Should().BeEquivalentTo(completion);
        context.ReadingSessions.Should().HaveCount(1);
        context.ReadingSessionAnswers.Should().HaveCount(2);
    }

    [Fact]
    public async Task Complete_rejects_answer_values_that_are_not_option_keys()
    {
        await using var context = CreateContext();
        var textId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        context.ReadingTexts.Add(ReadingText.Create(textId, "Test", "Kısa bir test metni."));
        context.ReadingQuestions.Add(ReadingQuestion.Create(
            questionId,
            textId,
            "Soru",
            "A",
            orderIndex: 0,
            type: 1,
            bloomLevel: 1,
            optionA: "Doğru",
            optionB: "Yanlış",
            optionC: "Diğer",
            optionD: "Son"));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var started = (await service.StartAsync(userId, textId, CancellationToken.None))!;

        var act = () => service.CompleteAsync(
            userId,
            textId,
            new CompleteStudentReadingRequest(
                started.SessionId,
                10,
                0,
                [new StudentReadingAnswer(questionId, "Doğru")]),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        context.ReadingSessions.Should().BeEmpty();
        context.ReadingSessionAnswers.Should().BeEmpty();
    }

    [Fact]
    public async Task Complete_rejects_an_attempt_without_a_question_snapshot()
    {
        await using var context = CreateContext();
        var textId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        context.ReadingTexts.Add(ReadingText.Create(textId, "Test", "Kısa bir test metni."));
        context.ReadingQuestions.Add(ReadingQuestion.Create(
            questionId,
            textId,
            "Soru",
            "A",
            orderIndex: 0,
            type: 1,
            bloomLevel: 1,
            optionA: "Doğru",
            optionB: "Yanlış",
            optionC: "Diğer",
            optionD: "Son"));
        var attempt = StudentReadingAttempt.Start(
            Guid.NewGuid(),
            userId,
            textId,
            DateTime.UtcNow.AddMinutes(-1));
        context.StudentReadingAttempts.Add(attempt);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var act = () => service.CompleteAsync(
            userId,
            textId,
            new CompleteStudentReadingRequest(
                attempt.Id,
                10,
                0,
                [new StudentReadingAnswer(questionId, "A")]),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*içerik kopyası*");
        context.ReadingSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Complete_rejects_a_null_question_snapshot_with_a_business_rule_error()
    {
        await using var context = CreateContext();
        var textId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        context.ReadingTexts.Add(ReadingText.Create(textId, "Test", "Kısa bir test metni."));
        context.ReadingQuestions.Add(ReadingQuestion.Create(
            questionId,
            textId,
            "Soru",
            "A",
            orderIndex: 0,
            type: 1,
            bloomLevel: 1,
            optionA: "Doğru",
            optionB: "Yanlış",
            optionC: "Diğer",
            optionD: "Son"));
        var attempt = StudentReadingAttempt.Start(
            Guid.NewGuid(),
            userId,
            textId,
            DateTime.UtcNow.AddMinutes(-1));
        attempt.SetQuestionSnapshot("null", DateTime.UtcNow);
        context.StudentReadingAttempts.Add(attempt);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var act = () => service.CompleteAsync(
            userId,
            textId,
            new CompleteStudentReadingRequest(
                attempt.Id,
                10,
                0,
                [new StudentReadingAnswer(questionId, "A")]),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*içerik kopyası*");
        context.ReadingSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Complete_rejects_question_snapshot_with_invalid_metadata()
    {
        await using var context = CreateContext();
        var textId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        context.ReadingTexts.Add(ReadingText.Create(textId, "Test", "Kısa bir test metni."));
        var attempt = StudentReadingAttempt.Start(
            Guid.NewGuid(),
            userId,
            textId,
            DateTime.UtcNow.AddMinutes(-1));
        attempt.SetQuestionSnapshot(
            $"[{{\"Id\":\"{questionId}\",\"Type\":0,\"BloomLevel\":1,\"OrderIndex\":0,\"CorrectAnswer\":\"A\"}}]",
            DateTime.UtcNow);
        context.StudentReadingAttempts.Add(attempt);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var act = () => service.CompleteAsync(
            userId,
            textId,
            new CompleteStudentReadingRequest(
                attempt.Id,
                10,
                0,
                [new StudentReadingAnswer(questionId, "A")]),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*içerik kopyası*");
        context.ReadingSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Complete_uses_start_metadata_when_the_reading_text_changes_after_start()
    {
        await using var context = CreateContext();
        var textId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var originalContent = string.Join(' ', Enumerable.Repeat("kelime", 100));
        context.ReadingTexts.Add(ReadingText.Create(
            textId,
            "Başlangıç metni",
            originalContent,
            language: "tr"));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var started = (await service.StartAsync(userId, textId, CancellationToken.None))!;
        var attempt = await context.StudentReadingAttempts.SingleAsync(item => item.Id == started.SessionId);
        context.Entry(attempt).Property(nameof(StudentReadingAttempt.StartedAt)).CurrentValue = DateTime.UtcNow.AddSeconds(-5);

        var text = await context.ReadingTexts.SingleAsync(item => item.Id == textId);
        text.Update(
            "Güncellenen metin",
            "tek",
            "tr",
            exerciseId: null,
            wordCount: 1,
            category: null,
            difficultyLevel: 0,
            targetAgeGroupId: null,
            isActive: false,
            tags: null,
            recommendedMinLevel: 0,
            recommendedMaxLevel: 0,
            actorId: userId,
            updatedAt: DateTime.UtcNow);
        await context.SaveChangesAsync();

        var completion = await service.CompleteAsync(
            userId,
            textId,
            new CompleteStudentReadingRequest(started.SessionId, 0, 0, []),
            CancellationToken.None);

        completion.Should().NotBeNull();
        completion!.IsMeasured.Should().BeTrue();
        context.ReadingSessions.Single().CalculatedWpm.Should().BeInRange(1_000, 1_500);
    }

    [Fact]
    public async Task Statistics_do_not_treat_readings_without_questions_as_zero_comprehension()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        context.ReadingTexts.Add(ReadingText.Create(
            textId,
            "İstatistik metni",
            "İstatistik hesaplaması için kullanılan metin."));
        var now = DateTime.UtcNow;
        context.ReadingSessions.AddRange(
            ReadingSession.Import(
                Guid.NewGuid(), userId, textId, 10, 240, 0, 0, 0, 0,
                now.AddMinutes(-2), now.AddMinutes(-2), userId.ToString(), null, null),
            ReadingSession.Import(
                Guid.NewGuid(), userId, textId, 10, 300, 2, 2, 100, 300,
                now, now, userId.ToString(), null, null));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var statistics = await service.GetStatisticsAsync(userId, CancellationToken.None);

        statistics.TotalSessions.Should().Be(2);
        statistics.AverageWPM.Should().Be(270);
        statistics.AverageComprehension.Should().Be(100);

        var comprehensionProgression = await service.GetComprehensionProgressionAsync(
            userId,
            CancellationToken.None);
        comprehensionProgression.Should().ContainSingle();
        comprehensionProgression[0].Rate.Should().Be(100);
    }

    [Fact]
    public async Task Adaptive_text_profile_ignores_readings_without_questions_for_comprehension()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        context.UserProfiles.Add(SpeedReadingUserProfile.CreateDefault(
            Guid.NewGuid(), userId, DateTime.UtcNow, userId.ToString()));
        context.ReadingTexts.Add(ReadingText.Create(
            textId,
            "Adaptif profil metni",
            "Adaptif profil hesabı için kullanılan örnek metin.",
            category: "Bilim",
            difficultyLevel: 1));
        var now = DateTime.UtcNow;
        context.ReadingSessions.AddRange(
            ReadingSession.Import(
                Guid.NewGuid(), userId, textId, 5, 100, 0, 0, 0, 0,
                now.AddMinutes(-2), now.AddMinutes(-2), userId.ToString(), null, null,
                // A legacy row can carry a positive WPM without a validated
                // measurement marker; it must not affect measured speed.
                isMeasured: false),
            ReadingSession.Import(
                Guid.NewGuid(), userId, textId, 30, 200, 4, 5, 80, 160,
                now, now, userId.ToString(), null, null,
                isMeasured: true));
        await context.SaveChangesAsync();

        var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAdaptiveText")!;
        var service = (ISpeedReadingAdaptiveText)Activator.CreateInstance(
            serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [context],
            culture: null)!;

        var summary = await service.UpdateProfileAsync(
            userId,
            new UpdateAdaptiveTextProfileRequest(textId, 50, 60, 100),
            CancellationToken.None);

        summary.AverageComprehensionScore.Should().Be(80);
        summary.AverageReadingSpeed.Should().Be(200);
    }

    [Fact]
    public async Task Adaptive_profile_includes_question_bearing_reading_sessions_without_wpm()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        context.UserProfiles.Add(SpeedReadingUserProfile.CreateDefault(
            Guid.NewGuid(), userId, DateTime.UtcNow, userId.ToString()));
        context.ReadingTexts.Add(ReadingText.Create(
            textId,
            "Anlama profili metni",
            "WPM ölçümü alınamayan ancak sorusu cevaplanan metin.",
            category: "Bilim",
            difficultyLevel: 1));
        context.ReadingQuestions.Add(ReadingQuestion.Create(
            questionId,
            textId,
            "Metin hangi veriyi korur?",
            "A",
            orderIndex: 0,
            type: 2,
            bloomLevel: 4,
            optionA: "Anlama verisini",
            optionB: "Rastgele veriyi",
            optionC: "Hiçbir veriyi",
            optionD: "Yalnızca süreyi"));
        var completedAt = DateTime.UtcNow;
        var readingSessionId = Guid.NewGuid();
        context.ReadingSessions.Add(ReadingSession.Import(
            readingSessionId,
            userId,
            textId,
            2,
            0,
            1,
            1,
            100,
            0,
            completedAt,
            completedAt,
            userId.ToString(),
            null,
            null,
            isMeasured: false));
        context.ReadingSessionAnswers.Add(ReadingSessionAnswer.Import(
            Guid.NewGuid(),
            readingSessionId,
            questionId,
            questionType: 2,
            bloomLevel: 4,
            orderIndex: 0,
            selectedAnswer: "A",
            isCorrect: true,
            completedAt,
            userId.ToString()));
        await context.SaveChangesAsync();

        var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAdaptiveLearning")!;
        var service = (SpeedReading.Application.AdaptiveLearning.ISpeedReadingAdaptiveLearning)Activator.CreateInstance(
            serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [context],
            culture: null)!;

        var profile = await service.GetProfileAsync(userId, CancellationToken.None);

        profile.TotalReadingSessions.Should().Be(1);
        profile.AverageComprehension.Should().Be(100);
        profile.BloomPerformance.Should().ContainKey(4).WhoseValue.Should().Be(100);
    }

    [Fact]
    public async Task Adaptive_profile_counts_a_universal_reading_bridge_once()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow;
        context.ReadingTexts.Add(ReadingText.Create(
            textId,
            "Tekil profil metni",
            "Universal okuma köprüsünün iki kez sayılmaması için örnek metin.",
            category: "Bilim",
            difficultyLevel: 1));
        context.ExerciseSessions.Add(ExerciseSession.Import(
            sessionId,
            userId,
            exerciseId,
            textId,
            null,
            SpeedReading.Domain.Sessions.ExerciseSessionStatus.Completed,
            completedAt.AddMinutes(-1),
            completedAt,
            0,
            null,
            null,
            1,
            1,
            1,
            0,
            "{}",
            null,
            "{}",
            completedAt.AddMinutes(-1),
            userId.ToString(),
            completedAt,
            userId.ToString()));
        context.ExerciseSessionResults.Add(SpeedReading.Domain.Sessions.ExerciseSessionResult.Create(
            Guid.NewGuid(),
            sessionId,
            userId,
            exerciseId,
            textId,
            wordsRead: 100,
            timeSpentSeconds: 60,
            rawWpm: 100,
            comprehensionScore: 80,
            weightedKdp: 80,
            score: 80,
            completedAt,
            questionAnswersJson: "[]",
            isMeasured: true));
        context.ReadingSessions.Add(ReadingSession.Import(
            sessionId,
            userId,
            textId,
            readingTimeSeconds: 60,
            calculatedWpm: 100,
            correctAnswers: 0,
            totalQuestions: 0,
            comprehensionRate: 0,
            efficiencyScore: 0,
            completedAt,
            createdAt: completedAt,
            createdBy: userId.ToString(),
            updatedAt: null,
            updatedBy: null,
            isMeasured: true));
        await context.SaveChangesAsync();

        var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAdaptiveLearning")!;
        var service = (SpeedReading.Application.AdaptiveLearning.ISpeedReadingAdaptiveLearning)Activator.CreateInstance(
            serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [context],
            culture: null)!;

        var profile = await service.GetProfileAsync(userId, CancellationToken.None);

        profile.TotalReadingSessions.Should().Be(1);
        profile.TotalExerciseSessions.Should().Be(0);
        profile.TotalMinutesSpent.Should().Be(1);
    }

    private static OwnedSpeedReadingDbContext CreateContext(string? databaseName = null) =>
        new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options);

    private static ISpeedReadingStudentReading CreateService(OwnedSpeedReadingDbContext context)
    {
        var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingStudentReading")!;
        return (ISpeedReadingStudentReading)Activator.CreateInstance(
            serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [context],
            culture: null)!;
    }

    private static ISpeedReadingExerciseSessions CreateExerciseSessionService(OwnedSpeedReadingDbContext context)
    {
        var serviceType = typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        return (ISpeedReadingExerciseSessions)Activator.CreateInstance(
            serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [context],
            culture: null)!;
    }
}
