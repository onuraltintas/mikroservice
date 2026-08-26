using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingExerciseSessions(SpeedReadingDbContext db) : ISpeedReadingExerciseSessions
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
        {
            throw new ArgumentException("A valid student and exercise are required.");
        }

        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.ExerciseId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Exercise not found.");
        var exerciseType = await db.ExerciseTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == exercise.ExerciseTypeId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Exercise type not found.");

        if (request.ReadingTextId.HasValue)
        {
            var readingTextMatches = await db.ReadingTexts
                .AsNoTracking()
                .AnyAsync(item => item.Id == request.ReadingTextId.Value
                    && !item.IsDeleted
                    && (item.ExerciseId == null || item.ExerciseId == request.ExerciseId), cancellationToken);
            if (!readingTextMatches)
            {
                throw new KeyNotFoundException("Reading text not found or does not belong to the exercise.");
            }
        }

        if (request.StudentAssignmentId.HasValue)
        {
            var assignmentMatches = await (
                from studentAssignment in db.StudentAssignments.AsNoTracking()
                join assignment in db.Assignments.AsNoTracking()
                    on studentAssignment.AssignmentId equals assignment.Id
                where studentAssignment.Id == request.StudentAssignmentId.Value
                    && studentAssignment.StudentId == studentId
                    && !studentAssignment.IsDeleted
                    && !assignment.IsDeleted
                    && assignment.IsActive
                    && assignment.ExerciseId == request.ExerciseId
                select studentAssignment.Id)
                .AnyAsync(cancellationToken);
            if (!assignmentMatches)
            {
                throw new KeyNotFoundException("Assignment not found or does not belong to the student.");
            }
        }

        var hasActiveSession = await db.ExerciseSessions
            .AsNoTracking()
            .AnyAsync(item => item.StudentId == studentId
                && item.ExerciseId == request.ExerciseId
                && (item.Status == (int)ExerciseSessionStatus.Active
                    || item.Status == (int)ExerciseSessionStatus.Paused)
                && !item.IsDeleted, cancellationToken);
        if (hasActiveSession)
        {
            throw new InvalidOperationException("An active session already exists for this exercise.");
        }

        var readingTextId = request.ReadingTextId;
        if (!readingTextId.HasValue && ReadingExerciseTypes.Contains(exerciseType.Name))
        {
            readingTextId = await db.ReadingTexts
                .AsNoTracking()
                .Where(item => !item.IsDeleted
                    && item.IsActive
                    && item.Content != string.Empty
                    && (item.ExerciseId == null || item.ExerciseId == request.ExerciseId))
                .OrderBy(item => item.Id)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var state = await CreateSessionStateAsync(
            studentId,
            request.ExerciseId,
            exercise,
            exerciseType.Name,
            readingTextId,
            request.CustomData,
            cancellationToken);
        var now = DateTime.UtcNow;
        var session = new LegacyExerciseSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            ExerciseId = exercise.Id,
            ReadingTextId = readingTextId,
            StudentAssignmentId = request.StudentAssignmentId,
            Status = (int)ExerciseSessionStatus.Active,
            StartTime = now,
            TotalSteps = state.TotalSteps,
            CurrentStep = 0,
            SessionDataJson = JsonSerializer.Serialize(state, JsonOptions),
            CustomDataJson = SerializeOptional(request.CustomData),
            ProcessedActionsJson = "{}",
            CreatedAt = now,
            CreatedBy = studentId,
            IsDeleted = false
        };
        session.TimeLimitSeconds = state.TimeLimitSeconds;
        db.ExerciseSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        var publicState = ToPublicJson(state);
        var publicConfiguration = RemoveAnswerKeys(ParseJsonOrEmpty(exercise.ConfigurationJson));
        return new StartExerciseSessionResponse(
            session.Id,
            session.ExerciseId,
            exerciseType.Name,
            ExerciseSessionStatus.Active,
            session.StartTime,
            session.TotalSteps,
            publicState,
            publicConfiguration);
    }

    public async Task<ExerciseActionValidationResponse> ValidateActionAsync(
        Guid studentId,
        Guid sessionId,
        ExerciseActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        if (TryGetCachedAction(session, request.ActionId, out var cached))
        {
            return cached!;
        }

        var now = DateTime.UtcNow;
        if (IsTimedOut(session, now))
        {
            session.Status = (int)ExerciseSessionStatus.Timeout;
            session.EndTime = now;
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("This exercise session has timed out.");
        }

        if (session.Status != (int)ExerciseSessionStatus.Active)
        {
            throw new InvalidOperationException("Actions can only be submitted to an active session.");
        }

        var state = DeserializeState(session.SessionDataJson);
        var response = request.Action?.Trim().ToLowerInvariant() switch
        {
            "start_reading" => StartReading(session, state, now),
            "finish_reading" => FinishReading(session, state, now),
            "answer_question" => AnswerQuestion(session, state, request),
            "advance" => Advance(session, state),
            _ when state.CurrentNumber.HasValue => ClickGrid(session, state, request),
            _ => AdvanceGeneric(session, state)
        };

        session.SessionDataJson = JsonSerializer.Serialize(state, JsonOptions);
        if (request.ActionId is { } actionId && actionId != Guid.Empty)
        {
            RecordCachedAction(session, actionId, response);
        }

        await db.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<ExerciseSessionResult> CompleteAsync(
        Guid studentId,
        Guid sessionId,
        CompleteExerciseSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        var now = DateTime.UtcNow;
        if (IsTimedOut(session, now))
        {
            session.Status = (int)ExerciseSessionStatus.Timeout;
            session.EndTime = now;
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("This exercise session has timed out.");
        }

        if (session.Status is not ((int)ExerciseSessionStatus.Active) and not ((int)ExerciseSessionStatus.Paused))
        {
            throw new InvalidOperationException("This session cannot be completed.");
        }

        var existingResult = await db.StudentExerciseResults
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.SessionId == session.Id && !item.IsDeleted, cancellationToken);
        if (existingResult is not null)
        {
            return ToResult(existingResult, session, DeserializeState(session.SessionDataJson));
        }

        var state = DeserializeState(session.SessionDataJson);
        if (request.CustomData is not null)
        {
            session.CustomDataJson = SerializeOptional(request.CustomData);
        }

        var answers = ResolveAnswers(state, request.QuestionAnswers);
        var correctQuestions = answers.Count(item => item.IsCorrect);
        var incorrectQuestions = answers.Count - correctQuestions;
        if (state.Questions.Count > 0 && answers.Count != state.Questions.Count)
        {
            throw new InvalidOperationException("All questions in the session must be answered.");
        }

        if (state.Questions.Count > 0)
        {
            session.CorrectCount = correctQuestions;
            session.IncorrectCount = incorrectQuestions;
        }

        if (session.Status == (int)ExerciseSessionStatus.Paused && session.PausedAt.HasValue)
        {
            session.TotalPausedSeconds += Math.Max(0, (int)(now - session.PausedAt.Value).TotalSeconds);
            session.PausedAt = null;
        }

        session.Status = (int)ExerciseSessionStatus.Completed;
        session.EndTime = now;
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
            ? Math.Round((decimal)correctQuestions / state.Questions.Count * 100, 2)
            : accuracy;
        var score = state.Questions.Count > 0
            ? Math.Round(Math.Clamp((comprehension * 0.6m) + ((rawWpm ?? 0) > 0
                ? Math.Min(rawWpm!.Value / 5, 100) * 0.4m
                : 0), 0, 100), 2)
            : accuracy;
        var weightedKdp = rawWpm.HasValue ? Math.Round(rawWpm.Value * comprehension / 100, 2) : (decimal?)null;
        var xp = SpeedReadingExerciseSessionRules.CalculateXp(score, accuracy, timeSpent);
        state.FinalWpm = rawWpm;
        state.ComprehensionScore = comprehension;
        state.WeightedKdp = weightedKdp;
        state.Answers = answers;
        session.SessionDataJson = JsonSerializer.Serialize(state, JsonOptions);

        var result = new LegacyStudentExerciseResult
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            StudentId = studentId,
            ExerciseId = session.ExerciseId,
            ReadingTextId = session.ReadingTextId,
            WordsRead = wordsRead ?? 0,
            TimeSpentSeconds = timeSpent,
            RawWPM = rawWpm ?? 0,
            ComprehensionScore = comprehension,
            WeightedKDP = weightedKdp ?? 0,
            QuestionAnswersJson = JsonSerializer.Serialize(answers, JsonOptions),
            ReadingMovementsJson = "[]",
            CompletedAt = now,
            CreatedAt = now,
            CreatedBy = studentId,
            IsDeleted = false
        };
        db.StudentExerciseResults.Add(result);

        if (session.ReadingTextId.HasValue)
        {
            db.ReadingSessions.Add(new LegacyReadingSession
            {
                Id = Guid.NewGuid(),
                UserId = studentId,
                ReadingTextId = session.ReadingTextId.Value,
                ReadingTimeSeconds = timeSpent,
                CalculatedWPM = (int)Math.Round(rawWpm ?? 0),
                CorrectAnswers = correctQuestions,
                TotalQuestions = state.Questions.Count,
                ComprehensionRate = comprehension,
                EfficiencyScore = weightedKdp ?? score,
                CompletedAt = now,
                CreatedAt = now,
                CreatedBy = studentId,
                IsDeleted = false
            });
        }

        if (session.StudentAssignmentId.HasValue)
        {
            var studentAssignment = await db.StudentAssignments.SingleOrDefaultAsync(
                item => item.Id == session.StudentAssignmentId.Value
                    && item.StudentId == studentId
                    && !item.IsDeleted,
                cancellationToken);
            if (studentAssignment is not null && !studentAssignment.IsCompleted)
            {
                studentAssignment.IsCompleted = true;
                studentAssignment.CompletionDate = now;
                studentAssignment.ResultId = result.Id;
                studentAssignment.Score = score;
                studentAssignment.KeyPerformanceMetric = weightedKdp;
                studentAssignment.UpdatedAt = now;
                studentAssignment.UpdatedBy = studentId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToResult(result, session, state, score, xp, now);
    }

    public async Task PauseAsync(Guid studentId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        if (session.Status != (int)ExerciseSessionStatus.Active)
        {
            throw new InvalidOperationException("Only an active session can be paused.");
        }

        session.Status = (int)ExerciseSessionStatus.Paused;
        session.PausedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResumeAsync(Guid studentId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        if (session.Status != (int)ExerciseSessionStatus.Paused)
        {
            throw new InvalidOperationException("Only a paused session can be resumed.");
        }

        var now = DateTime.UtcNow;
        if (session.PausedAt.HasValue)
        {
            session.TotalPausedSeconds += Math.Max(0, (int)(now - session.PausedAt.Value).TotalSeconds);
            session.PausedAt = null;
        }
        session.Status = (int)ExerciseSessionStatus.Active;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExerciseSessionProgress> GetProgressAsync(
        Guid studentId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        var now = DateTime.UtcNow;
        var timedOut = IsTimedOut(session, now);
        if (timedOut && session.Status == (int)ExerciseSessionStatus.Active)
        {
            session.Status = (int)ExerciseSessionStatus.Timeout;
            session.EndTime = now;
            await db.SaveChangesAsync(cancellationToken);
        }

        return new ExerciseSessionProgress(
            session.Id,
            (ExerciseSessionStatus)session.Status,
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
                session.Status == (int)ExerciseSessionStatus.Paused),
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
            (ExerciseSessionStatus)session.Status,
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
                session.Status == (int)ExerciseSessionStatus.Paused));
    }

    public async Task<IReadOnlyList<ActiveExerciseSession>> GetActiveAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var sessions = await db.ExerciseSessions
            .AsNoTracking()
            .Where(item => item.StudentId == studentId
                && !item.IsDeleted
                && (item.Status == (int)ExerciseSessionStatus.Active
                    || item.Status == (int)ExerciseSessionStatus.Paused))
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
                (ExerciseSessionStatus)session.Status,
                session.StartTime,
                session.CurrentStep,
                session.TotalSteps,
                session.TotalSteps == 0 ? 0 : Math.Round((decimal)session.CurrentStep / session.TotalSteps * 100, 2));
        }).ToList();
    }

    private async Task<LegacyExerciseSession> GetOwnedSessionAsync(
        Guid studentId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        await db.ExerciseSessions.SingleOrDefaultAsync(item => item.Id == sessionId
            && item.StudentId == studentId
            && !item.IsDeleted, cancellationToken)
        ?? throw new KeyNotFoundException("Exercise session not found.");

    private async Task<SessionState> CreateSessionStateAsync(
        Guid studentId,
        Guid exerciseId,
        LegacyExercise exercise,
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
            CurrentNumber = exerciseTypeName.Equals("SchulteTable", StringComparison.OrdinalIgnoreCase)
                ? 1
                : null,
            TimeLimitSeconds = ReadPositiveInt(config, "timeLimitSeconds")
        };

        if (readingTextId.HasValue)
        {
            var readingText = await db.ReadingTexts.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == readingTextId.Value && !item.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Reading text not found.");
            state.ReadingTextId = readingText.Id;
            state.ReadingTextTitle = readingText.Title;
            state.Content = readingText.Content;
            state.WordCount = readingText.WordCount > 0
                ? readingText.WordCount
                : CountWords(readingText.Content);
            state.Words = SplitWords(readingText.Content);
            var questions = await db.ReadingQuestions.AsNoTracking()
                .Where(item => item.ReadingTextId == readingText.Id && !item.IsDeleted)
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
            state.Questions = questions;
        }

        var gridSize = exerciseTypeName.Equals("SchulteTable", StringComparison.OrdinalIgnoreCase)
            ? ReadPositiveInt(config, "gridSize") ?? (int?)Math.Clamp(exercise.DifficultyLevel + 2, 3, 7)
            : null;
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
            if (state.TotalSteps <= 0)
            {
                state.TotalSteps = 1;
            }
        }

        if (exerciseTypeName.Equals("RSVP", StringComparison.OrdinalIgnoreCase)
            && state.Words.Length > 0)
        {
            state.TotalSteps = state.Words.Length;
        }

        if (customData is not null)
        {
            state.CustomData = customData;
        }

        return state;
    }

    private static ExerciseActionValidationResponse StartReading(
        LegacyExerciseSession session,
        SessionState state,
        DateTime now)
    {
        state.ReadingStartTime ??= now;
        session.CurrentStep = Math.Max(session.CurrentStep, 1);
        return Valid("Okuma başlatıldı. Zamanınız işliyor...", nextStep: session.CurrentStep);
    }

    private static ExerciseActionValidationResponse FinishReading(
        LegacyExerciseSession session,
        SessionState state,
        DateTime now)
    {
        state.ReadingStartTime ??= session.StartTime;
        state.ReadingEndTime = now;
        session.CurrentStep = Math.Max(session.CurrentStep, 1);
        var seconds = Math.Max(1, (int)(now - state.ReadingStartTime.Value).TotalSeconds);
        var wpm = state.WordCount == 0 ? 0 : Math.Round((decimal)state.WordCount / seconds * 60, 2);
        return Valid($"Okuma tamamlandı! Hızınız: {wpm} WPM.", nextStep: session.CurrentStep, currentWpm: wpm);
    }

    private static ExerciseActionValidationResponse AnswerQuestion(
        LegacyExerciseSession session,
        SessionState state,
        ExerciseActionRequest request)
    {
        if (!request.QuestionId.HasValue || string.IsNullOrWhiteSpace(request.Answer))
        {
            return Invalid("Soru ID ve cevap gereklidir.");
        }

        var question = state.Questions.SingleOrDefault(item => item.QuestionId == request.QuestionId.Value);
        if (question is null)
        {
            return Invalid("Soru bu oturuma ait değil.");
        }

        if (state.Answers.Any(item => item.QuestionId == question.QuestionId))
        {
            return Invalid("Bu soru zaten yanıtlandı.");
        }

        var isCorrect = string.Equals(
            request.Answer.Trim(),
            question.CorrectAnswer.Trim(),
            StringComparison.OrdinalIgnoreCase);
        state.Answers.Add(new SessionAnswer
        {
            QuestionId = question.QuestionId,
            Answer = request.Answer.Trim(),
            IsCorrect = isCorrect,
            TimeSpentSeconds = Math.Max(0, (request.ResponseTime ?? 0) / 1000),
            BloomLevel = question.BloomLevel
        });
        if (isCorrect) session.CorrectCount++;
        else session.IncorrectCount++;
        session.CurrentStep = Math.Min(session.TotalSteps, session.CurrentStep + 1);
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

    private static ExerciseActionValidationResponse Advance(
        LegacyExerciseSession session,
        SessionState state)
    {
        if (state.Words.Length == 0)
        {
            return AdvanceGeneric(session, state);
        }

        state.CurrentWordIndex = Math.Min(state.Words.Length, state.CurrentWordIndex + 1);
        session.CurrentStep = Math.Min(session.TotalSteps, state.CurrentWordIndex);
        return Valid(
            state.CurrentWordIndex >= state.Words.Length ? "Okuma tamamlandı." : "",
            nextStep: session.CurrentStep,
            nextWord: state.CurrentWordIndex < state.Words.Length ? state.Words[state.CurrentWordIndex] : null,
            isCompleted: state.CurrentWordIndex >= state.Words.Length);
    }

    private static ExerciseActionValidationResponse ClickGrid(
        LegacyExerciseSession session,
        SessionState state,
        ExerciseActionRequest request)
    {
        var expected = state.CurrentNumber!.Value;
        var isCorrect = request.Number == expected;
        if (isCorrect)
        {
            session.CorrectCount++;
            session.CurrentStep++;
            state.CurrentNumber++;
        }
        else
        {
            session.IncorrectCount++;
        }

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

    private static ExerciseActionValidationResponse AdvanceGeneric(
        LegacyExerciseSession session,
        SessionState state)
    {
        session.CurrentStep = Math.Min(session.TotalSteps, session.CurrentStep + 1);
        session.CorrectCount++;
        return Valid(
            session.CurrentStep >= session.TotalSteps ? "Egzersiz tamamlandı." : "",
            nextStep: session.CurrentStep,
            isCompleted: session.CurrentStep >= session.TotalSteps,
            isCorrect: true);
    }

    private static List<SessionAnswer> ResolveAnswers(
        SessionState state,
        IReadOnlyList<ExerciseQuestionAnswer>? submittedAnswers)
    {
        if (submittedAnswers is null)
        {
            return state.Answers;
        }

        var questionMap = state.Questions.ToDictionary(item => item.QuestionId);
        var ids = submittedAnswers.Select(item => item.QuestionId).ToList();
        if (ids.Count != ids.Distinct().Count())
        {
            throw new InvalidOperationException("A question cannot be submitted more than once.");
        }

        return submittedAnswers.Select(answer =>
        {
            if (!questionMap.TryGetValue(answer.QuestionId, out var question))
            {
                throw new InvalidOperationException("Submitted question does not belong to this session.");
            }

            return new SessionAnswer
            {
                QuestionId = answer.QuestionId,
                Answer = answer.Answer.Trim(),
                IsCorrect = string.Equals(answer.Answer.Trim(), question.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase),
                TimeSpentSeconds = Math.Max(answer.TimeSpentSeconds, 0),
                BloomLevel = question.BloomLevel
            };
        }).ToList();
    }

    private static ExerciseSessionResult ToResult(
        LegacyStudentExerciseResult result,
        LegacyExerciseSession session,
        SessionState state,
        decimal? score = null,
        int? xp = null,
        DateTime? now = null) =>
        new(
            session.Id,
            result.StudentId,
            result.ExerciseId,
            session.CorrectCount,
            session.IncorrectCount,
            SpeedReadingExerciseSessionRules.CalculateAccuracy(session.CorrectCount, session.IncorrectCount),
            result.TimeSpentSeconds,
            score ?? result.WeightedKDP,
            result.WordsRead == 0 ? null : result.WordsRead,
            result.RawWPM == 0 ? null : result.RawWPM,
            result.ComprehensionScore,
            result.WeightedKDP,
            xp ?? SpeedReadingExerciseSessionRules.CalculateXp(
                score ?? result.WeightedKDP,
                result.ComprehensionScore,
                result.TimeSpentSeconds),
            [],
            false,
            null,
            RemoveAnswerKeys(JsonSerializer.SerializeToElement(state, JsonOptions)),
            score.HasValue ? "Egzersiz tamamlandı." : "Bu oturum daha önce tamamlandı.",
            null);

    private static bool IsTimedOut(LegacyExerciseSession session, DateTime now) =>
        session.TimeLimitSeconds is > 0
        && SpeedReadingExerciseSessionRules.CalculateActiveSeconds(
            session.StartTime,
            now,
            session.TotalPausedSeconds,
            session.PausedAt,
            session.Status == (int)ExerciseSessionStatus.Paused) > session.TimeLimitSeconds.Value;

    private static bool TryGetCachedAction(
        LegacyExerciseSession session,
        Guid? actionId,
        out ExerciseActionValidationResponse? response)
    {
        response = null;
        if (actionId is not { } id || id == Guid.Empty || string.IsNullOrWhiteSpace(session.ProcessedActionsJson))
        {
            return false;
        }

        try
        {
            var actions = JsonSerializer.Deserialize<Dictionary<Guid, ExerciseActionValidationResponse>>(
                session.ProcessedActionsJson,
                JsonOptions);
            return actions is not null && actions.TryGetValue(id, out response);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void RecordCachedAction(
        LegacyExerciseSession session,
        Guid actionId,
        ExerciseActionValidationResponse response)
    {
        Dictionary<Guid, ExerciseActionValidationResponse> actions;
        try
        {
            actions = JsonSerializer.Deserialize<Dictionary<Guid, ExerciseActionValidationResponse>>(
                session.ProcessedActionsJson,
                JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            actions = [];
        }

        actions[actionId] = response;
        session.ProcessedActionsJson = JsonSerializer.Serialize(actions, JsonOptions);
    }

    private static SessionState DeserializeState(string json) =>
        string.IsNullOrWhiteSpace(json)
            ? new SessionState()
            : JsonSerializer.Deserialize<SessionState>(json, JsonOptions) ?? new SessionState();

    private static JsonElement ParseJsonOrEmpty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonSerializer.SerializeToElement(new { }, JsonOptions);
        }

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
                {
                    jsonObject.Remove(property.Key);
                }
                else
                {
                    RemoveAnswerKeys(property.Value);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                RemoveAnswerKeys(item);
            }
        }
    }

    private static int? ReadPositiveInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.TryGetInt32(out var value)
                && value > 0)
            {
                return value;
            }
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
