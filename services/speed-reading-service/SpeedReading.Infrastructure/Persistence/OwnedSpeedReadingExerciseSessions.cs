using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using OwnedExerciseSessionStatus = SpeedReading.Domain.Sessions.ExerciseSessionStatus;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Core exercise/session use case backed only by the owned Speed Reading
/// database. Assignment validation and gamification remain separate slices;
/// this implementation deliberately rejects those references until their
/// data is owned by this context as well.
/// </summary>
internal sealed class OwnedSpeedReadingExerciseSessions(
    OwnedSpeedReadingDbContext db) : ISpeedReadingExerciseSessions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> ReadingExerciseTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SpeedReading",
        "RSVP",
        "Comprehension",
        "FreeReading",
        "Chunking",
        "TextFading",
        "Skimming",
        "Scanning"
    };

    public async Task<StartExerciseSessionResponse> StartAsync(
        Guid studentId,
        StartExerciseSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (studentId == Guid.Empty || request.ExerciseId == Guid.Empty)
            throw new ArgumentException("A valid student and exercise are required.");
        if (request.StudentAssignmentId.HasValue)
        {
            throw new InvalidOperationException(
                "Student assignments are not available in the owned core slice yet.");
        }

        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.ExerciseId && item.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Exercise not found.");
        var exerciseType = await db.ExerciseTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == exercise.ExerciseTypeId && item.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Exercise type not found.");

        if (request.ReadingTextId.HasValue)
        {
            var readingTextMatches = await db.ReadingTexts
                .AsNoTracking()
                .AnyAsync(item => item.Id == request.ReadingTextId.Value
                    && item.IsActive
                    && (item.ExerciseId == null || item.ExerciseId == request.ExerciseId), cancellationToken);
            if (!readingTextMatches)
            {
                throw new KeyNotFoundException("Reading text not found or does not belong to the exercise.");
            }
        }

        var hasActiveSession = await db.ExerciseSessions
            .AsNoTracking()
            .AnyAsync(item => item.StudentId == studentId
                && item.ExerciseId == request.ExerciseId
                && (item.Status == OwnedExerciseSessionStatus.Active
                    || item.Status == OwnedExerciseSessionStatus.Paused), cancellationToken);
        if (hasActiveSession)
            throw new InvalidOperationException("An active session already exists for this exercise.");

        var readingTextId = request.ReadingTextId;
        if (!readingTextId.HasValue && ReadingExerciseTypes.Contains(exerciseType.Name))
        {
            readingTextId = await db.ReadingTexts
                .AsNoTracking()
                .Where(item => item.IsActive
                    && item.Content != string.Empty
                    && (item.ExerciseId == null || item.ExerciseId == request.ExerciseId))
                .OrderBy(item => item.Id)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var state = await CreateSessionStateAsync(
            request.ExerciseId,
            exercise,
            exerciseType.Name,
            readingTextId,
            request.CustomData,
            cancellationToken);
        var now = DateTime.UtcNow;
        var session = ExerciseSession.Start(
            studentId,
            exercise.Id,
            readingTextId,
            state.TotalSteps,
            now,
            state.TimeLimitSeconds);
        session.SetState(
            JsonSerializer.Serialize(state, JsonOptions),
            SerializeOptional(request.CustomData));
        session.SetProcessedActions("{}");
        db.ExerciseSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return new StartExerciseSessionResponse(
            session.Id,
            session.ExerciseId,
            exerciseType.Name,
            Application.ExerciseSessions.ExerciseSessionStatus.Active,
            session.StartTime,
            session.TotalSteps,
            ToPublicJson(state),
            RemoveAnswerKeys(ParseJsonOrEmpty(exercise.ConfigurationJson)));
    }

    public async Task<ExerciseActionValidationResponse> ValidateActionAsync(
        Guid studentId,
        Guid sessionId,
        ExerciseActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        if (TryGetCachedAction(session.ProcessedActionsJson, request.ActionId, out var cached))
            return cached!;

        var now = DateTime.UtcNow;
        if (session.IsTimedOut(now))
        {
            session.MarkTimedOut(now);
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("This exercise session has timed out.");
        }

        if (session.Status != OwnedExerciseSessionStatus.Active)
            throw new InvalidOperationException("Actions can only be submitted to an active session.");

        var state = DeserializeState(session.SessionDataJson);
        var response = request.Action?.Trim().ToLowerInvariant() switch
        {
            "start_reading" => StartReading(session, state, now),
            "finish_reading" => FinishReading(session, state, now),
            "answer_question" => AnswerQuestion(session, state, request),
            "advance" => Advance(session, state),
            _ when state.CurrentNumber.HasValue => ClickGrid(session, state, request),
            _ => AdvanceGeneric(session)
        };

        session.SetState(JsonSerializer.Serialize(state, JsonOptions), session.CustomDataJson);
        if (request.ActionId is { } actionId && actionId != Guid.Empty)
            session.SetProcessedActions(RecordCachedAction(session.ProcessedActionsJson, actionId, response));

        await db.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<SpeedReading.Application.ExerciseSessions.ExerciseSessionResult> CompleteAsync(
        Guid studentId,
        Guid sessionId,
        CompleteExerciseSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        var now = DateTime.UtcNow;
        if (session.IsTimedOut(now))
        {
            session.MarkTimedOut(now);
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("This exercise session has timed out.");
        }

        var state = DeserializeState(session.SessionDataJson);
        var existingResult = await db.ExerciseSessionResults
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.SessionId == session.Id, cancellationToken);
        if (existingResult is not null)
            return ToResult(existingResult, session, state);

        var answers = ResolveAnswers(session, state, request.QuestionAnswers);
        if (state.Questions.Count > 0 && answers.Count != state.Questions.Count)
            throw new InvalidOperationException("All questions in the session must be answered.");

        if (request.CustomData is not null)
            session.SetState(session.SessionDataJson, SerializeOptional(request.CustomData));
        session.Complete(now);

        var timeSpent = SpeedReadingExerciseSessionRules.CalculateActiveSeconds(
            session.StartTime,
            now,
            session.TotalPausedSeconds,
            null,
            isPaused: false);
        var accuracy = SpeedReadingExerciseSessionRules.CalculateAccuracy(
            session.CorrectCount,
            session.IncorrectCount);
        var wordsRead = state.WordCount > 0 ? (int?)state.WordCount : null;
        var rawWpm = wordsRead.HasValue && timeSpent > 0
            ? Math.Round((decimal)wordsRead.Value / timeSpent * 60, 2)
            : (decimal?)null;
        var comprehension = state.Questions.Count > 0
            ? Math.Round((decimal)answers.Count(item => item.IsCorrect) / state.Questions.Count * 100, 2)
            : accuracy;
        var score = state.Questions.Count > 0
            ? Math.Round(Math.Clamp((comprehension * 0.6m) + ((rawWpm ?? 0) > 0
                ? Math.Min(rawWpm!.Value / 5, 100) * 0.4m
                : 0), 0, 100), 2)
            : accuracy;
        var weightedKdp = rawWpm.HasValue ? Math.Round(rawWpm.Value * comprehension / 100, 2) : (decimal?)null;
        state.FinalWpm = rawWpm;
        state.ComprehensionScore = comprehension;
        state.WeightedKdp = weightedKdp;
        state.Answers = answers;
        session.SetState(JsonSerializer.Serialize(state, JsonOptions), session.CustomDataJson);

        var result = SpeedReading.Domain.Sessions.ExerciseSessionResult.Create(
            Guid.NewGuid(),
            session.Id,
            session.StudentId,
            session.ExerciseId,
            session.ReadingTextId,
            wordsRead ?? 0,
            timeSpent,
            rawWpm ?? 0,
            comprehension,
            weightedKdp ?? 0,
            score,
            now,
            JsonSerializer.Serialize(answers, JsonOptions));
        db.ExerciseSessionResults.Add(result);
        await db.SaveChangesAsync(cancellationToken);

        return ToResult(result, session, state, score, SpeedReadingExerciseSessionRules.CalculateXp(score, accuracy, timeSpent));
    }

    public async Task PauseAsync(Guid studentId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        session.Pause(DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResumeAsync(Guid studentId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        session.Resume(DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExerciseSessionProgress> GetProgressAsync(
        Guid studentId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        var now = DateTime.UtcNow;
        var timedOut = session.IsTimedOut(now);
        if (timedOut)
        {
            session.MarkTimedOut(now);
            await db.SaveChangesAsync(cancellationToken);
        }

        return new ExerciseSessionProgress(
            session.Id,
            (Application.ExerciseSessions.ExerciseSessionStatus)session.Status,
            session.CurrentStep,
            session.TotalSteps,
            session.TotalSteps == 0 ? 0 : Math.Round((decimal)session.CurrentStep / session.TotalSteps * 100, 2),
            session.CorrectCount,
            session.IncorrectCount,
            SpeedReadingExerciseSessionRules.CalculateActiveSeconds(
                session.StartTime,
                session.EndTime ?? now,
                session.TotalPausedSeconds,
                session.PausedAt,
                session.Status == OwnedExerciseSessionStatus.Paused),
            session.TimeLimitSeconds,
            timedOut);
    }

    public async Task<ExerciseSessionDetails> GetDetailsAsync(
        Guid studentId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        var exercise = await db.Exercises.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == session.ExerciseId, cancellationToken)
            ?? throw new KeyNotFoundException("Exercise not found.");
        var typeName = await db.ExerciseTypes.AsNoTracking()
            .Where(item => item.Id == exercise.ExerciseTypeId)
            .Select(item => item.DisplayName)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        var now = DateTime.UtcNow;
        return new ExerciseSessionDetails(
            session.Id,
            session.ExerciseId,
            exercise.Title,
            typeName,
            (Application.ExerciseSessions.ExerciseSessionStatus)session.Status,
            session.StartTime,
            session.EndTime,
            session.CurrentStep,
            session.TotalSteps,
            session.CorrectCount,
            session.IncorrectCount,
            SpeedReadingExerciseSessionRules.CalculateAccuracy(session.CorrectCount, session.IncorrectCount),
            SpeedReadingExerciseSessionRules.CalculateActiveSeconds(
                session.StartTime,
                session.EndTime ?? now,
                session.TotalPausedSeconds,
                session.PausedAt,
                session.Status == OwnedExerciseSessionStatus.Paused));
    }

    public async Task<IReadOnlyList<ActiveExerciseSession>> GetActiveAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var sessions = await db.ExerciseSessions
            .AsNoTracking()
            .Where(item => item.StudentId == studentId
                && (item.Status == OwnedExerciseSessionStatus.Active
                    || item.Status == OwnedExerciseSessionStatus.Paused))
            .OrderByDescending(item => item.StartTime)
            .ToListAsync(cancellationToken);
        var exerciseIds = sessions.Select(item => item.ExerciseId).Distinct().ToArray();
        var exercises = await db.Exercises.AsNoTracking()
            .Where(item => exerciseIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var typeIds = exercises.Values.Select(item => item.ExerciseTypeId).Distinct().ToArray();
        var types = await db.ExerciseTypes.AsNoTracking()
            .Where(item => typeIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        return sessions.Select(session =>
        {
            exercises.TryGetValue(session.ExerciseId, out var exercise);
            var type = exercise is not null && types.TryGetValue(exercise.ExerciseTypeId, out var foundType)
                ? foundType.DisplayName
                : string.Empty;
            return new ActiveExerciseSession(
                session.Id,
                session.ExerciseId,
                exercise?.Title ?? string.Empty,
                type,
                (Application.ExerciseSessions.ExerciseSessionStatus)session.Status,
                session.StartTime,
                session.CurrentStep,
                session.TotalSteps,
                session.TotalSteps == 0 ? 0 : Math.Round((decimal)session.CurrentStep / session.TotalSteps * 100, 2));
        }).ToList();
    }

    private async Task<ExerciseSession> GetOwnedSessionAsync(
        Guid studentId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        await db.ExerciseSessions.SingleOrDefaultAsync(item => item.Id == sessionId
            && item.StudentId == studentId, cancellationToken)
        ?? throw new KeyNotFoundException("Exercise session not found.");

    private async Task<SessionState> CreateSessionStateAsync(
        Guid exerciseId,
        Exercise exercise,
        string exerciseTypeName,
        Guid? readingTextId,
        Dictionary<string, JsonElement>? customData,
        CancellationToken cancellationToken)
    {
        var config = ParseJsonOrEmpty(exercise.ConfigurationJson);
        var state = new SessionState
        {
            ExerciseId = exerciseId,
            ExerciseTypeName = exerciseTypeName,
            DifficultyLevel = exercise.DifficultyLevel,
            CurrentNumber = exerciseTypeName.Equals("SchulteTable", StringComparison.OrdinalIgnoreCase) ? 1 : null,
            TimeLimitSeconds = ReadPositiveInt(config, "timeLimitSeconds")
        };

        if (readingTextId.HasValue)
        {
            var readingText = await db.ReadingTexts.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == readingTextId.Value && item.IsActive, cancellationToken)
                ?? throw new KeyNotFoundException("Reading text not found.");
            state.ReadingTextId = readingText.Id;
            state.ReadingTextTitle = readingText.Title;
            state.Content = readingText.Content;
            state.WordCount = readingText.WordCount > 0 ? readingText.WordCount : CountWords(readingText.Content);
            state.Words = SplitWords(readingText.Content);
            state.Questions = await db.ReadingQuestions.AsNoTracking()
                .Where(item => item.ReadingTextId == readingText.Id)
                .OrderBy(item => item.OrderIndex)
                .Select(item => new SessionQuestion
                {
                    QuestionId = item.Id,
                    QuestionText = item.QuestionText,
                    OptionA = item.OptionA,
                    OptionB = item.OptionB,
                    OptionC = item.OptionC,
                    OptionD = item.OptionD,
                    CorrectAnswer = item.CorrectAnswer,
                    Explanation = item.Explanation,
                    BloomLevel = item.BloomLevel,
                    DifficultyLevel = item.DifficultyLevel
                })
                .ToListAsync(cancellationToken);
        }

        var gridSize = exerciseTypeName.Equals("SchulteTable", StringComparison.OrdinalIgnoreCase)
            ? ReadPositiveInt(config, "gridSize") ?? Math.Clamp(exercise.DifficultyLevel + 2, 3, 7)
            : (int?)null;
        if (gridSize.HasValue)
        {
            state.GridSize = gridSize.Value;
            state.TotalSteps = gridSize.Value * gridSize.Value;
            var values = Enumerable.Range(1, state.TotalSteps).OrderBy(_ => Guid.NewGuid()).ToArray();
            state.Grid = Enumerable.Range(0, gridSize.Value)
                .Select(row => values.Skip(row * gridSize.Value).Take(gridSize.Value).ToArray())
                .ToArray();
        }
        else
        {
            state.TotalSteps = ReadPositiveInt(config, "totalSteps")
                ?? ReadPositiveInt(config, "itemCount")
                ?? ReadPositiveInt(config, "rounds")
                ?? (state.Questions.Count > 0 ? 1 + state.Questions.Count : state.Words.Length);
            if (state.TotalSteps <= 0) state.TotalSteps = 1;
        }

        if (exerciseTypeName.Equals("RSVP", StringComparison.OrdinalIgnoreCase) && state.Words.Length > 0)
            state.TotalSteps = state.Words.Length;
        state.CustomData = customData;
        return state;
    }

    private static ExerciseActionValidationResponse StartReading(
        ExerciseSession session,
        SessionState state,
        DateTime now)
    {
        state.ReadingStartTime ??= now;
        session.SetCurrentStep(Math.Max(session.CurrentStep, 1));
        return Valid("Okuma başlatıldı. Zamanınız işliyor...", nextStep: session.CurrentStep);
    }

    private static ExerciseActionValidationResponse FinishReading(
        ExerciseSession session,
        SessionState state,
        DateTime now)
    {
        state.ReadingStartTime ??= session.StartTime;
        state.ReadingEndTime = now;
        session.SetCurrentStep(Math.Max(session.CurrentStep, 1));
        var seconds = Math.Max(1, (int)(now - state.ReadingStartTime.Value).TotalSeconds);
        var wpm = state.WordCount == 0 ? 0 : Math.Round((decimal)state.WordCount / seconds * 60, 2);
        return Valid("Okuma tamamlandı! Hızınız: " + wpm + " WPM.", session.CurrentStep, currentWpm: wpm);
    }

    private static ExerciseActionValidationResponse AnswerQuestion(
        ExerciseSession session,
        SessionState state,
        ExerciseActionRequest request)
    {
        if (!request.QuestionId.HasValue || string.IsNullOrWhiteSpace(request.Answer))
            return Invalid("Soru ID ve cevap gereklidir.");
        var question = state.Questions.SingleOrDefault(item => item.QuestionId == request.QuestionId.Value);
        if (question is null)
            return Invalid("Soru bu oturuma ait değil.");
        if (state.Answers.Any(item => item.QuestionId == question.QuestionId))
            return Invalid("Bu soru zaten yanıtlandı.");

        var answer = request.Answer.Trim();
        var isCorrect = string.Equals(answer, question.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
        session.RecordAnswer(
            question.QuestionId,
            answer,
            isCorrect,
            Math.Max(0, (request.ResponseTime ?? 0) / 1000),
            question.BloomLevel);
        state.Answers.Add(new SessionAnswer
        {
            QuestionId = question.QuestionId,
            Answer = answer,
            IsCorrect = isCorrect,
            TimeSpentSeconds = Math.Max(0, (request.ResponseTime ?? 0) / 1000),
            BloomLevel = question.BloomLevel
        });
        return new ExerciseActionValidationResponse(
            isCorrect,
            isCorrect ? "Doğru cevap!" : "Yanlış cevap.",
            null,
            session.CurrentStep,
            null,
            state.Answers.Count == state.Questions.Count,
            isCorrect,
            question.CorrectAnswer,
            question.Explanation,
            null,
            null);
    }

    private static ExerciseActionValidationResponse Advance(ExerciseSession session, SessionState state)
    {
        if (state.Words.Length == 0)
            return AdvanceGeneric(session);
        state.CurrentWordIndex = Math.Min(state.Words.Length, state.CurrentWordIndex + 1);
        session.SetCurrentStep(Math.Min(session.TotalSteps, state.CurrentWordIndex));
        return Valid(
            state.CurrentWordIndex >= state.Words.Length ? "Okuma tamamlandı." : string.Empty,
            session.CurrentStep,
            state.CurrentWordIndex < state.Words.Length ? state.Words[state.CurrentWordIndex] : null,
            state.CurrentWordIndex >= state.Words.Length);
    }

    private static ExerciseActionValidationResponse ClickGrid(
        ExerciseSession session,
        SessionState state,
        ExerciseActionRequest request)
    {
        var expected = state.CurrentNumber!.Value;
        var isCorrect = request.Number == expected;
        session.Advance(isCorrect);
        if (isCorrect) state.CurrentNumber++;
        var completed = state.CurrentNumber > state.TotalSteps;
        return new ExerciseActionValidationResponse(
            isCorrect,
            isCorrect ? "Doğru!" : $"Yanlış! Doğru sıra: {expected}",
            completed ? null : state.CurrentNumber,
            session.CurrentStep,
            null,
            completed,
            isCorrect,
            null,
            null,
            null,
            null);
    }

    private static ExerciseActionValidationResponse AdvanceGeneric(ExerciseSession session)
    {
        session.Advance();
        return Valid(
            session.CurrentStep >= session.TotalSteps ? "Egzersiz tamamlandı." : string.Empty,
            session.CurrentStep,
            isCompleted: session.CurrentStep >= session.TotalSteps,
            isCorrect: true);
    }

    private static List<SessionAnswer> ResolveAnswers(
        ExerciseSession session,
        SessionState state,
        IReadOnlyList<ExerciseQuestionAnswer>? submittedAnswers)
    {
        if (submittedAnswers is null)
            return state.Answers;

        var questionMap = state.Questions.ToDictionary(item => item.QuestionId);
        var ids = submittedAnswers.Select(item => item.QuestionId).ToList();
        if (ids.Count != ids.Distinct().Count())
            throw new InvalidOperationException("A question cannot be submitted more than once.");

        var resolved = submittedAnswers.Select(answer =>
        {
            if (!questionMap.TryGetValue(answer.QuestionId, out var question))
                throw new InvalidOperationException("Submitted question does not belong to this session.");
            var normalizedAnswer = answer.Answer.Trim();
            return new SessionAnswer
            {
                QuestionId = question.QuestionId,
                Answer = normalizedAnswer,
                IsCorrect = string.Equals(normalizedAnswer, question.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase),
                TimeSpentSeconds = Math.Max(answer.TimeSpentSeconds, 0),
                BloomLevel = question.BloomLevel
            };
        }).ToList();

        foreach (var answer in resolved)
        {
            var existing = state.Answers.SingleOrDefault(item => item.QuestionId == answer.QuestionId);
            if (existing is not null)
            {
                if (!string.Equals(existing.Answer, answer.Answer, StringComparison.Ordinal))
                    throw new InvalidOperationException("A submitted answer cannot change after it is recorded.");
                continue;
            }

            session.RecordAnswer(
                answer.QuestionId,
                answer.Answer,
                answer.IsCorrect,
                answer.TimeSpentSeconds,
                answer.BloomLevel);
        }

        return resolved;
    }

    private static SpeedReading.Application.ExerciseSessions.ExerciseSessionResult ToResult(
        SpeedReading.Domain.Sessions.ExerciseSessionResult result,
        ExerciseSession session,
        SessionState state,
        decimal? score = null,
        int? xp = null) =>
        new(
            session.Id,
            result.StudentId,
            result.ExerciseId,
            session.CorrectCount,
            session.IncorrectCount,
            SpeedReadingExerciseSessionRules.CalculateAccuracy(session.CorrectCount, session.IncorrectCount),
            result.TimeSpentSeconds,
            score ?? result.Score,
            result.WordsRead == 0 ? null : result.WordsRead,
            result.RawWpm == 0 ? null : result.RawWpm,
            result.ComprehensionScore,
            result.WeightedKdp,
            xp ?? SpeedReadingExerciseSessionRules.CalculateXp(
                score ?? result.Score,
                result.ComprehensionScore,
                result.TimeSpentSeconds),
            [],
            false,
            null,
            RemoveAnswerKeys(JsonSerializer.SerializeToElement(state, JsonOptions)),
            score.HasValue ? "Egzersiz tamamlandı." : "Bu oturum daha önce tamamlandı.",
            null);

    private static bool TryGetCachedAction(
        string processedActionsJson,
        Guid? actionId,
        out ExerciseActionValidationResponse? response)
    {
        response = null;
        if (actionId is not { } id || id == Guid.Empty || string.IsNullOrWhiteSpace(processedActionsJson))
            return false;
        try
        {
            var actions = JsonSerializer.Deserialize<Dictionary<Guid, ExerciseActionValidationResponse>>(
                processedActionsJson,
                JsonOptions);
            return actions is not null && actions.TryGetValue(id, out response);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string RecordCachedAction(
        string processedActionsJson,
        Guid actionId,
        ExerciseActionValidationResponse response)
    {
        Dictionary<Guid, ExerciseActionValidationResponse> actions;
        try
        {
            actions = JsonSerializer.Deserialize<Dictionary<Guid, ExerciseActionValidationResponse>>(
                processedActionsJson,
                JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            actions = [];
        }

        actions[actionId] = response;
        return JsonSerializer.Serialize(actions, JsonOptions);
    }

    private static SessionState DeserializeState(string json) =>
        string.IsNullOrWhiteSpace(json)
            ? new SessionState()
            : JsonSerializer.Deserialize<SessionState>(json, JsonOptions) ?? new SessionState();

    private static JsonElement ParseJsonOrEmpty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return JsonSerializer.SerializeToElement(new { }, JsonOptions);
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new { }, JsonOptions);
        }
    }

    private static JsonElement ToPublicJson(SessionState state) =>
        RemoveAnswerKeys(JsonSerializer.SerializeToElement(state, JsonOptions));

    private static JsonElement RemoveAnswerKeys(JsonElement element)
    {
        var node = JsonNode.Parse(element.GetRawText());
        RemoveAnswerKeys(node);
        using var document = JsonDocument.Parse(node?.ToJsonString() ?? "{}");
        return document.RootElement.Clone();
    }

    private static void RemoveAnswerKeys(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var property in jsonObject.ToList())
            {
                if (property.Key.Equals("correctAnswer", StringComparison.OrdinalIgnoreCase)
                    || property.Key.Equals("correctOption", StringComparison.OrdinalIgnoreCase))
                    jsonObject.Remove(property.Key);
                else
                    RemoveAnswerKeys(property.Value);
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
                RemoveAnswerKeys(item);
        }
    }

    private static int? ReadPositiveInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.TryGetInt32(out var value)
                && value > 0)
                return value;
        }
        return null;
    }

    private static string[] SplitWords(string content) =>
        content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int CountWords(string content) => SplitWords(content).Length;

    private static string? SerializeOptional(Dictionary<string, JsonElement>? values) =>
        values is null ? null : JsonSerializer.Serialize(values, JsonOptions);

    private static ExerciseActionValidationResponse Valid(
        string message,
        int? nextStep = null,
        string? nextWord = null,
        bool isCompleted = false,
        bool? isCorrect = null,
        decimal? currentWpm = null) =>
        new(true, message, null, nextStep, nextWord, isCompleted, isCorrect, null, null, currentWpm, null);

    private static ExerciseActionValidationResponse Invalid(string message) =>
        new(false, message, null, null, null, false, false, null, null, null, null);

    private sealed class SessionState
    {
        public Guid ExerciseId { get; set; }
        public string ExerciseTypeName { get; set; } = string.Empty;
        public Guid? ReadingTextId { get; set; }
        public string ReadingTextTitle { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int WordCount { get; set; }
        public int DifficultyLevel { get; set; }
        public int TotalSteps { get; set; }
        public int CurrentWordIndex { get; set; }
        public int? CurrentNumber { get; set; }
        public int GridSize { get; set; }
        public int[][]? Grid { get; set; }
        public int? TimeLimitSeconds { get; set; }
        public DateTime? ReadingStartTime { get; set; }
        public DateTime? ReadingEndTime { get; set; }
        public decimal? FinalWpm { get; set; }
        public decimal? ComprehensionScore { get; set; }
        public decimal? WeightedKdp { get; set; }
        public string[] Words { get; set; } = [];
        public List<SessionQuestion> Questions { get; set; } = [];
        public List<SessionAnswer> Answers { get; set; } = [];
        public Dictionary<string, JsonElement>? CustomData { get; set; }
    }

    private sealed class SessionQuestion
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string OptionA { get; set; } = string.Empty;
        public string OptionB { get; set; } = string.Empty;
        public string OptionC { get; set; } = string.Empty;
        public string OptionD { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public string? Explanation { get; set; }
        public int BloomLevel { get; set; }
        public int DifficultyLevel { get; set; }
    }

    private sealed class SessionAnswer
    {
        public Guid QuestionId { get; set; }
        public string Answer { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public int TimeSpentSeconds { get; set; }
        public int BloomLevel { get; set; }
    }
}
