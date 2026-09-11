using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;
using SpeedReading.Application.ExerciseSessions;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingExerciseSessions(SpeedReadingDbContext db) : ISpeedReadingExerciseSessions
{
    private const string TimeoutAnswer = "__timeout__";
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
        "Scanning",
        "RegressionReduction",
        "SubvocalizationReduction"
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

        // Legacy storage has no authoritative assessment-attempt table. A
        // client-supplied attempt id must therefore never be allowed to turn
        // an ordinary exercise session into an assessment result.
        if (request.AssessmentAttemptId.HasValue)
        {
            throw new NotSupportedException(
                "Versioned assessment attempts require owned Speed Reading data.");
        }

        var profileAgeGroupId = await GetAgeGroupIdAsync(studentId, cancellationToken);

        var exercise = await db.Exercises
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.ExerciseId
                && !item.IsDeleted
                && item.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Exercise not found.");
        var exerciseType = await db.ExerciseTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == exercise.ExerciseTypeId
                && !item.IsDeleted
                && item.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Exercise type not found.");

        if (request.ReadingTextId.HasValue)
        {
            var readingTextMatches = await db.ReadingTexts
                .AsNoTracking()
                .AnyAsync(item => item.Id == request.ReadingTextId.Value
                    && !item.IsDeleted
                    && item.IsActive
                    && (!profileAgeGroupId.HasValue
                        || item.TargetAgeGroupConfigurationId == null
                        || item.TargetAgeGroupConfigurationId == profileAgeGroupId.Value)
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
                    && (!profileAgeGroupId.HasValue
                        || item.TargetAgeGroupConfigurationId == null
                        || item.TargetAgeGroupConfigurationId == profileAgeGroupId.Value)
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
            request.AssessmentAttemptId,
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
        var publicConfiguration = state.IsAssessmentMode
            ? SpeedReadingContentSecurity.SanitizeFocusAssessmentJson(ParseJsonOrEmpty(exercise.ConfigurationJson))
            : RemoveAssessmentKeys(ParseJsonOrEmpty(exercise.ConfigurationJson));
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
        var state = DeserializeState(session.SessionDataJson);
        if (IsTimedOut(session, state, now))
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

        var actionName = request.Action?.Trim().ToLowerInvariant();
        var response = actionName switch
        {
            "start_reading" => StartReading(session, state, now),
            "finish_reading" => FinishReading(session, state, now),
            "focus_start" => StartFocus(session, state, now),
            "focus_step" => AdvanceFocus(session, state, request, now),
            "visual_expansion_present" => PresentVisualExpansion(session, state, now),
            "visual_expansion_answer" => AnswerVisualExpansion(session, state, request, now),
            "answer_question" => AnswerQuestion(session, state, request),
            "position_match" => ValidateFocusMatch(session, state, request, "position", now),
            "word_match" => ValidateFocusMatch(session, state, request, "word", now),
            "match_attempt" => ValidateFocusMatch(session, state, request, "position", now),
            "complete" when IsFocusExercise(state) => CompleteFocus(session, state),
            "advance" => Advance(session, state),
            "grid_click" when state.CurrentNumber.HasValue => ClickGrid(session, state, request),
            "grid_click" => Invalid("Grid cell action is not valid for this exercise."),
            _ when state.CurrentNumber.HasValue => Invalid("Grid cell action is required."),
            _ => AdvanceGeneric(session, state)
        };

        // Start the server clock only after an action was understood. A wrong
        // grid attempt still counts as a real attempt and therefore starts it.
        if (response.IsValid || actionName == "grid_click" && state.CurrentNumber.HasValue)
        {
            EnsureTimingStarted(session, state, now);
        }

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
        var state = DeserializeState(session.SessionDataJson);
        if (request.IsAssessmentMode != state.IsAssessmentMode)
        {
            throw new InvalidOperationException("Assessment mode must match the server-owned session.");
        }
        var now = DateTime.UtcNow;
        if (IsTimedOut(session, state, now))
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

        if (state.CurrentNumber.HasValue && state.CurrentNumber.Value <= state.TotalSteps)
        {
            throw new InvalidOperationException("All grid targets must be completed before the session can be completed.");
        }
        if (IsFocusExercise(state) && !state.FocusCompleted)
        {
            throw new InvalidOperationException("The focus exercise must be completed through its validated action flow.");
        }
        if (IsVisualExpansionExercise(state.ExerciseTypeName)
            && state.VisualExpansionRound < state.TotalSteps)
            throw new InvalidOperationException("All visual expansion rounds must be validated before completion.");

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
            GetTimingStartTime(session, state, now),
            now,
            GetTimingPausedSeconds(session, state),
            null,
            isPaused: false);
        var accuracy = SpeedReadingExerciseSessionRules.CalculateAccuracy(
            session.CorrectCount,
            session.IncorrectCount);
        var wordsRead = state.WordCount > 0 ? (int?)state.WordCount : null;
        var rawWpmCandidate = wordsRead.HasValue
            ? SpeedReadingExerciseSessionRules.CalculateValidatedRawWpm(wordsRead.Value, timeSpent)
            : null;
        var measurementStatus = SpeedReadingExerciseSessionRules.ResolveMeasurementStatus(
            state.Questions.Count,
            session.CorrectCount,
            session.IncorrectCount,
            hasValidWpm: (rawWpmCandidate.HasValue && SupportsServerReadingMeasurement(state))
                || (IsFocusExercise(state) && state.FocusCompleted && HasFocusStimulus(state)));
        var isMeasured = measurementStatus == SpeedReadingMeasurementStatus.Measured;
        var rawWpm = isMeasured ? rawWpmCandidate : null;
        var comprehension = state.Questions.Count > 0
            ? Math.Round((decimal)correctQuestions / state.Questions.Count * 100, 2)
            : accuracy;
        var score = state.Questions.Count > 0
            ? Math.Round(Math.Clamp((comprehension * 0.6m) + ((rawWpm ?? 0) > 0
                ? Math.Min(rawWpm!.Value / 5, 100) * 0.4m
                : 0), 0, 100), 2)
            : accuracy;
        var weightedKdp = rawWpm.HasValue ? Math.Round(rawWpm.Value * comprehension / 100, 2) : (decimal?)null;
        var xp = isMeasured
            ? SpeedReadingExerciseSessionRules.CalculateXp(score, accuracy, timeSpent)
            : 0;
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
            IsMeasured = isMeasured,
            IsAssessmentMode = state.IsAssessmentMode,
            AssessmentAttemptId = state.AssessmentAttemptId,
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

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.StudentExerciseResults
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.SessionId == sessionId && !item.IsDeleted, cancellationToken);
            if (concurrent is null)
                throw;

            var persistedSession = await db.ExerciseSessions
                .AsNoTracking()
                .SingleAsync(item => item.Id == sessionId && item.StudentId == studentId && !item.IsDeleted, cancellationToken);
            return ToResult(concurrent, persistedSession, DeserializeState(persistedSession.SessionDataJson));
        }

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
        var state = DeserializeState(session.SessionDataJson);
        var now = DateTime.UtcNow;
        var timedOut = IsTimedOut(session, state, now);
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
                GetTimingStartTime(session, state, now),
                session.EndTime ?? now,
                GetTimingPausedSeconds(session, state),
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
        var state = DeserializeState(session.SessionDataJson);
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
                GetTimingStartTime(session, state, now),
                session.EndTime ?? now,
                GetTimingPausedSeconds(session, state),
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

    private async Task<Guid?> GetAgeGroupIdAsync(
        Guid studentId,
        CancellationToken cancellationToken) =>
        await db.Users
            .AsNoTracking()
            .Where(item => item.Id == studentId && !item.IsDeleted)
            .Select(item => item.AgeGroupConfigurationId)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<SessionState> CreateSessionStateAsync(
        Guid studentId,
        Guid exerciseId,
        LegacyExercise exercise,
        string exerciseTypeName,
        Guid? readingTextId,
        Guid? assessmentAttemptId,
        Dictionary<string, JsonElement>? customData,
        CancellationToken cancellationToken)
    {
        var config = ParseJsonOrEmpty(exercise.ConfigurationJson);
        var state = new SessionState
        {
            ExerciseId = exerciseId,
            ExerciseTypeName = exerciseTypeName,
            DifficultyLevel = exercise.DifficultyLevel,
            IsAssessmentMode = assessmentAttemptId.HasValue,
            AssessmentAttemptId = assessmentAttemptId,
            TimingStartsOnAction = true,
            CurrentNumber = IsGridExercise(exerciseTypeName, config) ? 1 : null,
            TimeLimitSeconds = ReadPositiveInt(config, "timeLimitSeconds")
        };
        var engineConfig = ReadObject(config, "engineConfig");
        if (IsVisualExpansionExercise(exerciseTypeName))
        {
            var expansion = ReadObject(engineConfig.ValueKind == JsonValueKind.Object ? engineConfig : config, "expansion");
            var timing = ReadObject(engineConfig.ValueKind == JsonValueKind.Object ? engineConfig : config, "timing");
            state.VisualExpansionStimulusType = ReadString(expansion, "stimulusType") ?? "letter";
            state.VisualExpansionPattern = ReadString(expansion, "pattern") ?? "horizontal";
            state.VisualExpansionDisplayDurationMs = Math.Clamp(ReadPositiveInt(timing, "durationMs") ?? 250, 100, 5_000);
        }
        if (IsFocusExercise(exerciseTypeName))
        {
            var focusConfig = engineConfig.ValueKind == JsonValueKind.Object ? engineConfig : config;
            state.FocusMode = ReadString(focusConfig, "mode") ?? "position";
            state.FocusNLevel = ReadPositiveInt(focusConfig, "nLevel") ?? 1;
            state.FocusSpeedMs = ReadPositiveInt(focusConfig, "speedMs") ?? 1500;
            state.PositionSequence = ReadIntArray(focusConfig, "positionSequence");
            state.WordSequence = ReadStringArray(focusConfig, "wordSequence");
            state.PositionTargetIndices = ReadIntArray(focusConfig, "positionTargetIndices");
            state.WordTargetIndices = ReadIntArray(focusConfig, "wordTargetIndices");
        }

        if (readingTextId.HasValue)
        {
            var readingText = await db.ReadingTexts.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == readingTextId.Value
                    && !item.IsDeleted
                    && item.IsActive, cancellationToken)
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

        if (IsVisualizationExercise(exerciseTypeName) && state.Questions.Count == 0)
        {
            state.VisualizationScenes = await LoadVisualizationScenesAsync(exerciseId, config, cancellationToken);
            state.Questions = state.VisualizationScenes
                .SelectMany(scene => scene.Questions)
                .Select(ToSessionQuestion)
                .ToList();
        }

        var gridSize = IsGridExercise(exerciseTypeName, config)
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
                ?? (IsVisualizationExercise(exerciseTypeName)
                    ? state.Questions.Count
                    : state.Questions.Count > 0 ? 1 + state.Questions.Count : state.Words.Length);
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

        if (IsFocusExercise(state))
        {
            state.TotalSteps = Math.Max(
                state.TotalSteps,
                Math.Max(state.PositionSequence.Length, state.WordSequence.Length));
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
        EnsureTimingStarted(session, state, now);
        state.ReadingStartTime ??= now;
        session.CurrentStep = Math.Max(session.CurrentStep, 1);
        return Valid("Okuma başlatıldı. Zamanınız işliyor...", nextStep: session.CurrentStep);
    }

    private static ExerciseActionValidationResponse FinishReading(
        LegacyExerciseSession session,
        SessionState state,
        DateTime now)
    {
        EnsureTimingStarted(session, state, now);
        state.ReadingStartTime ??= state.TimingStartedAt ?? now;
        state.ReadingEndTime = now;
        session.CurrentStep = Math.Max(session.CurrentStep, 1);
        var seconds = Math.Max(0, (int)(now - state.ReadingStartTime.Value).TotalSeconds);
        var wpm = SpeedReadingExerciseSessionRules.CalculateValidatedRawWpm(state.WordCount, seconds);
        var message = wpm.HasValue
            ? $"Okuma tamamlandı! Hızınız: {wpm.Value} WPM."
            : "Okuma tamamlandı; güvenilir WPM için yeterli ölçüm alınamadı.";
        return Valid(message, nextStep: session.CurrentStep, currentWpm: wpm);
    }

    private static ExerciseActionValidationResponse PresentVisualExpansion(
        LegacyExerciseSession session,
        SessionState state,
        DateTime now)
    {
        if (!IsVisualExpansionExercise(state.ExerciseTypeName))
            return Invalid("Visual expansion presentation is not valid for this exercise.");
        if (state.VisualExpansionExpectedStimuli.Length > 0)
            return Invalid("The current visual expansion round is still awaiting an answer.");
        if (state.VisualExpansionRound >= state.TotalSteps)
            return Invalid("All visual expansion rounds are complete.");

        state.VisualExpansionExpectedStimuli = VisualExpansionRoundRules.CreateStimuli(
            session.Id.GetHashCode(), state.VisualExpansionRound, state.VisualExpansionStimulusType,
            state.VisualExpansionPattern.Equals("radial", StringComparison.OrdinalIgnoreCase) ? 4 : 2).ToArray();
        state.VisualExpansionPresentedAt = now.ToUniversalTime();
        state.VisualExpansionPausedSecondsAtPresentation = session.TotalPausedSeconds;
        var feedback = JsonSerializer.SerializeToElement(new
        {
            round = state.VisualExpansionRound,
            stimuli = state.VisualExpansionExpectedStimuli,
            displayDurationMs = state.VisualExpansionDisplayDurationMs
        }, JsonOptions);
        return Valid("Görsel genişleme uyaranı hazır.", state.VisualExpansionRound, feedbackData: feedback);
    }

    private static ExerciseActionValidationResponse AnswerVisualExpansion(
        LegacyExerciseSession session,
        SessionState state,
        ExerciseActionRequest request,
        DateTime now)
    {
        if (!IsVisualExpansionExercise(state.ExerciseTypeName)
            || state.VisualExpansionExpectedStimuli.Length == 0
            || !state.VisualExpansionPresentedAt.HasValue)
            return Invalid("No visual expansion round is awaiting an answer.");

        var elapsedMs = (int)Math.Round(Math.Max(0,
            (now.ToUniversalTime() - state.VisualExpansionPresentedAt.Value).TotalMilliseconds
            - Math.Max(0, session.TotalPausedSeconds - state.VisualExpansionPausedSecondsAtPresentation) * 1000d));
        var result = VisualExpansionRoundRules.Evaluate(
            state.VisualExpansionExpectedStimuli, request.Answers ?? [], elapsedMs,
            state.VisualExpansionDisplayDurationMs, state.VisualExpansionDisplayDurationMs + 5_000);
        if (!result.IsAccepted)
        {
            state.VisualExpansionExpectedStimuli = [];
            state.VisualExpansionPresentedAt = null;
            return Invalid("Visual expansion answer arrived outside its response window.");
        }

        if (result.IsCorrect) session.CorrectCount++; else session.IncorrectCount++;
        session.CurrentStep = Math.Min(session.TotalSteps, session.CurrentStep + 1);
        state.VisualExpansionRound++;
        state.VisualExpansionExpectedStimuli = [];
        state.VisualExpansionPresentedAt = null;
        return Valid(
            state.IsAssessmentMode ? "Yanıt kaydedildi." : result.IsCorrect ? "Doğru." : "Yanlış.",
            state.VisualExpansionRound,
            isCompleted: state.VisualExpansionRound >= state.TotalSteps,
            isCorrect: state.IsAssessmentMode ? null : result.IsCorrect);
    }

    private static ExerciseActionValidationResponse StartFocus(
        LegacyExerciseSession session,
        SessionState state,
        DateTime now)
    {
        if (!IsFocusExercise(state))
            return Invalid("Focus start is only valid for focus exercises.");
        if (!HasFocusStimulus(state))
            return Invalid("Focus exercise data is unavailable for server validation.");

        state.FocusStartTime ??= now.ToUniversalTime();
        return Valid(
            "Odak egzersizi başlatıldı.",
            nextStep: session.CurrentStep,
            isCompleted: false,
            isCorrect: null);
    }

    private static ExerciseActionValidationResponse AdvanceFocus(
        LegacyExerciseSession session,
        SessionState state,
        ExerciseActionRequest request,
        DateTime now)
    {
        if (!IsFocusExercise(state) || !state.IsAssessmentMode)
            return Invalid("Focus step streaming is only valid for assessments.");
        if (!HasFocusStimulus(state))
            return Invalid("Focus exercise data is unavailable for server validation.");
        if (!state.FocusStartTime.HasValue)
            return Invalid("Focus exercise has not been started.");
        if (request.Index is not { } index
            || index < 0
            || index >= state.TotalSteps)
            return Invalid("A valid focus trial index is required.");
        if (index != state.FocusPresentedIndex + 1)
            return Invalid("Focus trials must be requested in sequence.");

        var speedMs = Math.Max(1, state.FocusSpeedMs);
        var focusStart = state.FocusStartTime ?? session.StartTime;
        var expectedIndex = FocusTrialTimingRules.ExpectedIndex(
            focusStart, now, GetTimingPausedSeconds(session, state), speedMs, state.TotalSteps);
        if (!FocusTrialTimingRules.CanPresent(index, state.FocusPresentedIndex, expectedIndex))
            return Invalid("Focus step arrived outside its trial time window.");

        state.FocusPresentedIndex = index;
        state.FocusPresentedAt = now.ToUniversalTime();
        state.FocusPausedSecondsAtPresentation = session.TotalPausedSeconds;
        var feedback = JsonSerializer.SerializeToElement(new
        {
            index,
            position = (state.FocusMode.Equals("position", StringComparison.OrdinalIgnoreCase)
                || state.FocusMode.Equals("dual", StringComparison.OrdinalIgnoreCase))
                && index < state.PositionSequence.Length
                ? state.PositionSequence[index]
                : (int?)null,
            word = (state.FocusMode.Equals("word", StringComparison.OrdinalIgnoreCase)
                || state.FocusMode.Equals("dual", StringComparison.OrdinalIgnoreCase))
                && index < state.WordSequence.Length
                ? state.WordSequence[index]
                : null,
            mode = state.FocusMode,
            level = state.FocusNLevel
        }, JsonOptions);
        return Valid("Odak uyaranı hazır.", index, feedbackData: feedback);
    }

    private static ExerciseActionValidationResponse AnswerQuestion(
        LegacyExerciseSession session,
        SessionState state,
        ExerciseActionRequest request)
    {
        if (!request.QuestionId.HasValue
            || (!request.IsTimeout && string.IsNullOrWhiteSpace(request.Answer)))
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

        var answer = request.IsTimeout ? TimeoutAnswer : request.Answer!.Trim();
        var isCorrect = !request.IsTimeout && string.Equals(
            answer,
            question.CorrectAnswer.Trim(),
            StringComparison.OrdinalIgnoreCase);
        state.Answers.Add(new SessionAnswer
        {
            QuestionId = question.QuestionId,
            Answer = answer,
            IsCorrect = isCorrect,
            TimeSpentSeconds = Math.Max(0, (request.ResponseTime ?? 0) / 1000),
            BloomLevel = question.BloomLevel
        });
        if (isCorrect) session.CorrectCount++;
        else session.IncorrectCount++;
        session.CurrentStep = Math.Min(session.TotalSteps, session.CurrentStep + 1);
        var isAssessment = state.IsAssessmentMode;
        return new ExerciseActionValidationResponse(
            true,
            request.IsTimeout
                ? (isAssessment ? "Süre doldu; cevap kaydedildi." : "Süre doldu!")
                : (isAssessment ? "Cevap kaydedildi." : (isCorrect ? "Doğru cevap!" : "Yanlış cevap.")),
            null,
            session.CurrentStep,
            null,
            state.Answers.Count == state.Questions.Count,
            isAssessment ? null : isCorrect,
            isAssessment ? null : question.CorrectAnswer,
            isAssessment ? null : question.Explanation,
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
        if (request.Number is not { } number)
        {
            return Invalid("A grid number is required.");
        }

        if (state.Grid is { Length: > 0 })
        {
            var grid = state.Grid.SelectMany(row => row ?? []).ToArray();
            if (request.Index is not { } cellIndex
                || cellIndex < 0
                || cellIndex >= grid.Length
                || grid[cellIndex] != number)
            {
                return Invalid("Grid cell does not match the server-owned layout.");
            }
        }

        var expected = state.CurrentNumber!.Value;
        var isCorrect = number == expected;
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
        var isAssessment = state.IsAssessmentMode;
        return new ExerciseActionValidationResponse(
            isAssessment || isCorrect,
            isAssessment ? "Yanıt kaydedildi." : (isCorrect ? "Doğru!" : $"Yanlış! Doğru sıra: {expected}"),
            isAssessment ? null : (completed ? null : state.CurrentNumber),
            session.CurrentStep,
            null,
            completed,
            isAssessment ? null : isCorrect,
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
        return Valid(
            session.CurrentStep >= session.TotalSteps ? "Egzersiz tamamlandı." : "",
            nextStep: session.CurrentStep,
            isCompleted: session.CurrentStep >= session.TotalSteps,
            isCorrect: null);
    }

    private static ExerciseActionValidationResponse ValidateFocusMatch(
        LegacyExerciseSession session,
        SessionState state,
        ExerciseActionRequest request,
        string channel,
        DateTime now)
    {
        if (!IsFocusExercise(state)
            || !FocusChannels(state).Contains(channel, StringComparer.OrdinalIgnoreCase)
            || (channel == "position" && state.PositionSequence.Length == 0)
            || (channel == "word" && state.WordSequence.Length == 0))
        {
            return Invalid("Focus exercise data is unavailable for server validation.");
        }
        if (!state.FocusStartTime.HasValue)
        {
            return Invalid("Focus exercise has not been started.");
        }

        if (request.Index is not { } index
            || index < 0
            || index >= state.TotalSteps)
        {
            return Invalid("A valid focus trial index is required.");
        }

        if (state.FocusResponses.Any(item =>
                item.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase)
                && item.Index == index))
        {
            return Invalid("This focus trial was already recorded.");
        }

        var lastIndex = channel.Equals("position", StringComparison.OrdinalIgnoreCase)
            ? state.FocusLastPositionIndex
            : state.FocusLastWordIndex;
        if (!lastIndex.HasValue)
        {
            lastIndex = state.FocusResponses
                .Where(item => item.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase))
                .Select(item => (int?)item.Index)
                .Max();
        }
        if (lastIndex.HasValue && index < lastIndex.Value)
        {
            return Invalid("Focus responses must be submitted in trial order.");
        }

        var speedMs = Math.Max(1, state.FocusSpeedMs);
        if (state.IsAssessmentMode)
        {
            if (!FocusTrialTimingRules.IsCurrentTrial(index, state.FocusPresentedIndex))
                return Invalid("Focus response does not match the currently presented trial.");
            if (state.FocusPresentedAt.HasValue
                && !FocusTrialTimingRules.IsAssessmentResponseOnTime(
                    state.FocusPresentedAt.Value,
                    now,
                    Math.Max(0, session.TotalPausedSeconds - state.FocusPausedSecondsAtPresentation),
                    speedMs))
                return Invalid("Focus response arrived after the trial window.");
        }
        else
        {
            var focusStart = state.FocusStartTime ?? session.StartTime;
            var expectedIndex = FocusTrialTimingRules.ExpectedIndex(
                focusStart, now, GetTimingPausedSeconds(session, state), speedMs, state.TotalSteps);
            if (index < expectedIndex - 1 || index > expectedIndex + 1)
            {
                return Invalid("Focus response arrived outside its trial time window.");
            }
        }

        var isCorrect = IsFocusTarget(state, channel, index);
        state.FocusResponses.Add(new FocusResponse
        {
            Channel = channel,
            Index = index,
            IsCorrect = isCorrect
        });
        if (channel.Equals("position", StringComparison.OrdinalIgnoreCase))
            state.FocusLastPositionIndex = index;
        else
            state.FocusLastWordIndex = index;

        if (isCorrect) session.CorrectCount++;
        else session.IncorrectCount++;
        session.CurrentStep = Math.Min(session.TotalSteps, session.CurrentStep + 1);

        var isAssessment = state.IsAssessmentMode;
        return new ExerciseActionValidationResponse(
            true,
            isAssessment ? "Yanıt kaydedildi." : (isCorrect ? "Doğru." : "Yanlış."),
            null,
            session.CurrentStep,
            null,
            false,
            isAssessment ? null : isCorrect,
            null,
            null,
            null,
            isAssessment ? null : BuildFocusFeedback(state));
    }

    private static ExerciseActionValidationResponse CompleteFocus(
        LegacyExerciseSession session,
        SessionState state)
    {
        if (!IsFocusExercise(state))
            return AdvanceGeneric(session, state);

        if (!HasFocusStimulus(state))
            return Invalid("Focus exercise data is unavailable for server validation.");
        if (state.IsAssessmentMode && state.FocusPresentedIndex < state.TotalSteps - 1)
            return Invalid("All focus trials must be presented before completion.");
        if (!state.IsAssessmentMode && state.FocusResponses.Count == 0)
            return Invalid("At least one focus response is required before completion.");

        if (!state.FocusCompleted)
        {
            foreach (var channel in FocusChannels(state))
            {
                var length = channel == "position"
                    ? state.PositionSequence.Length
                    : state.WordSequence.Length;
                for (var index = 0; index < length; index++)
                {
                    if (IsFocusTarget(state, channel, index)
                        && !state.FocusResponses.Any(item =>
                            item.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase)
                            && item.Index == index))
                    {
                        state.FocusResponses.Add(new FocusResponse
                        {
                            Channel = channel,
                            Index = index,
                            IsCorrect = false,
                            IsMiss = true
                        });
                        session.IncorrectCount++;
                    }
                }
            }
            state.FocusCompleted = true;
            session.CurrentStep = session.TotalSteps;
        }

        return Valid(
            "Odak egzersizi tamamlandı.",
            nextStep: session.TotalSteps,
            isCompleted: true,
            isCorrect: null,
            feedbackData: state.IsAssessmentMode ? null : BuildFocusFeedback(state));
    }

    private static bool IsFocusTarget(SessionState state, string channel, int index)
    {
        var configuredTargets = channel == "position"
            ? state.PositionTargetIndices
            : state.WordTargetIndices;
        if (configuredTargets.Length > 0)
            return configuredTargets.Contains(index);

        var nLevel = Math.Max(1, state.FocusNLevel);
        if (index < nLevel)
            return false;
        if (channel == "position")
            return state.PositionSequence[index] == state.PositionSequence[index - nLevel];
        return string.Equals(
            state.WordSequence[index],
            state.WordSequence[index - nLevel],
            StringComparison.OrdinalIgnoreCase);
    }

    private static string[] FocusChannels(SessionState state) =>
        state.FocusMode.Equals("dual", StringComparison.OrdinalIgnoreCase)
            ? ["position", "word"]
            : state.FocusMode.Equals("word", StringComparison.OrdinalIgnoreCase)
                ? ["word"]
                : ["position"];

    private static bool IsFocusExercise(SessionState state) =>
        IsFocusExercise(state.ExerciseTypeName);

    private static bool HasFocusStimulus(SessionState state) =>
        state.FocusMode.Equals("word", StringComparison.OrdinalIgnoreCase)
            ? state.WordSequence.Length >= state.TotalSteps
            : state.FocusMode.Equals("dual", StringComparison.OrdinalIgnoreCase)
                ? state.PositionSequence.Length >= state.TotalSteps
                    && state.WordSequence.Length >= state.TotalSteps
                : state.PositionSequence.Length >= state.TotalSteps;

    private static bool IsFocusExercise(string exerciseTypeName) =>
        exerciseTypeName.Contains("focus", StringComparison.OrdinalIgnoreCase)
        || exerciseTypeName.Contains("attention", StringComparison.OrdinalIgnoreCase)
        || exerciseTypeName.Contains("fixation", StringComparison.OrdinalIgnoreCase);

    private static bool IsVisualExpansionExercise(string exerciseTypeName) =>
        exerciseTypeName.Contains("visualexpansion", StringComparison.OrdinalIgnoreCase)
        || exerciseTypeName.Contains("visual expansion", StringComparison.OrdinalIgnoreCase)
        || exerciseTypeName.Contains("görsel genişleme", StringComparison.OrdinalIgnoreCase);

    private static bool IsVisualizationExercise(string exerciseTypeName) =>
        exerciseTypeName.Contains("visualization", StringComparison.OrdinalIgnoreCase)
        || exerciseTypeName.Contains("visualisation", StringComparison.OrdinalIgnoreCase);

    private static bool IsGridExercise(string exerciseTypeName, JsonElement config)
    {
        if (exerciseTypeName.Contains("schulte", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ReadString(config, "engineType"), "grid_interaction", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var nestedConfig = ReadObject(config, "engineConfig");
        return string.Equals(ReadString(nestedConfig, "engineType"), "grid_interaction", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<VisualizationSceneState>> LoadVisualizationScenesAsync(
        Guid exerciseId,
        JsonElement config,
        CancellationToken cancellationToken)
    {
        var configuredScenes = ReadVisualizationScenes(config);
        if (configuredScenes.Count > 0)
            return configuredScenes;

        var scenes = await db.VisualizationScenes.AsNoTracking()
            .Where(item => item.ExerciseId == exerciseId && !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        if (scenes.Count == 0)
            return [];

        var sceneIds = scenes.Select(item => item.Id).ToArray();
        var questions = await db.VisualizationQuestions.AsNoTracking()
            .Where(item => sceneIds.Contains(item.SceneId) && !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        var questionsByScene = questions
            .GroupBy(item => item.SceneId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return scenes.Select(scene => new VisualizationSceneState
        {
            SceneId = scene.Id.ToString("D"),
            Description = scene.Description,
            ImageUrl = scene.ImageUrl,
            Duration = scene.Duration,
            DisplayOrder = scene.DisplayOrder,
            Questions = questionsByScene.GetValueOrDefault(scene.Id, [])
                .Select(question => new VisualizationQuestionState
                {
                    QuestionId = question.Id,
                    QuestionText = question.QuestionText,
                    Options = ParseOptions(question.OptionsJson).ToList(),
                    CorrectAnswer = question.CorrectAnswer,
                    QuestionType = question.QuestionType,
                    DisplayOrder = question.DisplayOrder,
                    HintText = question.HintText
                })
                .ToList()
        }).ToList();
    }

    private static List<VisualizationSceneState> ReadVisualizationScenes(JsonElement config)
    {
        var scenesElement = ReadProperty(config, "scenes");
        if (scenesElement.ValueKind != JsonValueKind.Array)
            scenesElement = ReadProperty(config, "Scenes");
        if (scenesElement.ValueKind != JsonValueKind.Array)
            return [];

        var scenes = new List<VisualizationSceneState>();
        foreach (var scene in scenesElement.EnumerateArray())
        {
            if (scene.ValueKind != JsonValueKind.Object)
                continue;

            var questions = new List<VisualizationQuestionState>();
            var questionsElement = ReadProperty(scene, "questions");
            if (questionsElement.ValueKind != JsonValueKind.Array)
                questionsElement = ReadProperty(scene, "Questions");
            if (questionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var question in questionsElement.EnumerateArray())
                {
                    var questionId = ReadString(question, "questionId") ?? ReadString(question, "id");
                    if (!Guid.TryParse(questionId, out var parsedQuestionId))
                        continue;
                    questions.Add(new VisualizationQuestionState
                    {
                        QuestionId = parsedQuestionId,
                        QuestionText = ReadString(question, "questionText") ?? ReadString(question, "text") ?? string.Empty,
                        Options = ReadStringArray(question, "options").ToList(),
                        CorrectAnswer = ReadString(question, "correctAnswer") ?? string.Empty,
                        QuestionType = ReadString(question, "questionType") ?? "detail",
                        DisplayOrder = ReadPositiveInt(question, "displayOrder") ?? questions.Count,
                        HintText = ReadString(question, "hintText")
                    });
                }
            }

            scenes.Add(new VisualizationSceneState
            {
                SceneId = ReadString(scene, "sceneId") ?? ReadString(scene, "id") ?? Guid.NewGuid().ToString("D"),
                Description = ReadString(scene, "description") ?? string.Empty,
                ImageUrl = ReadString(scene, "imageUrl"),
                Duration = ReadPositiveInt(scene, "duration") ?? 30,
                DisplayOrder = ReadPositiveInt(scene, "displayOrder") ?? scenes.Count,
                Steps = ReadStringArray(scene, "steps").ToList(),
                StepDurationMs = ReadPositiveInt(scene, "stepDurationMs") ?? 3000,
                Questions = questions
            });
        }

        return scenes.OrderBy(item => item.DisplayOrder).ToList();
    }

    private static SessionQuestion ToSessionQuestion(VisualizationQuestionState question) => new()
    {
        QuestionId = question.QuestionId,
        QuestionText = question.QuestionText,
        OptionA = question.Options.ElementAtOrDefault(0) ?? string.Empty,
        OptionB = question.Options.ElementAtOrDefault(1) ?? string.Empty,
        OptionC = question.Options.ElementAtOrDefault(2) ?? string.Empty,
        OptionD = question.Options.ElementAtOrDefault(3) ?? string.Empty,
        CorrectAnswer = question.CorrectAnswer,
        BloomLevel = 1,
        DifficultyLevel = 1
    };

    private static JsonElement ReadProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return default;
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }
        return default;
    }

    private static string[] ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
            return [];
        try
        {
            using var document = JsonDocument.Parse(optionsJson);
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .ToArray()
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static JsonElement BuildFocusFeedback(SessionState state) =>
        JsonSerializer.SerializeToElement(new
        {
            hits = state.FocusResponses.Count(item => item.IsCorrect && !item.IsMiss),
            misses = state.FocusResponses.Count(item => item.IsMiss),
            falseAlarms = state.FocusResponses.Count(item => !item.IsCorrect && !item.IsMiss)
        }, JsonOptions);

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

        var resolved = submittedAnswers.Select(answer =>
        {
            if (!questionMap.TryGetValue(answer.QuestionId, out var question))
            {
                throw new InvalidOperationException("Submitted question does not belong to this session.");
            }

            var normalizedAnswer = string.IsNullOrWhiteSpace(answer.Answer)
                ? TimeoutAnswer
                : answer.Answer.Trim();
            var existing = state.Answers.SingleOrDefault(item => item.QuestionId == question.QuestionId);
            if (existing is not null)
            {
                if (!string.Equals(existing.Answer, normalizedAnswer, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("A submitted answer cannot change after it is recorded.");
                }

                // Preserve the server-recorded correctness, timing and Bloom
                // metadata when a completed session is retried.
                return existing;
            }

            return new SessionAnswer
            {
                QuestionId = answer.QuestionId,
                Answer = normalizedAnswer,
                IsCorrect = normalizedAnswer != TimeoutAnswer
                    && string.Equals(normalizedAnswer, question.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase),
                TimeSpentSeconds = Math.Max(answer.TimeSpentSeconds, 0),
                BloomLevel = question.BloomLevel
            };
        }).ToList();

        return resolved;
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
            result.IsMeasured
                ? SpeedReadingExerciseSessionRules.CalculateAccuracy(session.CorrectCount, session.IncorrectCount)
                : null,
            result.TimeSpentSeconds,
            result.IsMeasured ? score ?? CalculateServerScore(result) : null,
            result.WordsRead == 0 ? null : result.WordsRead,
            result.IsMeasured && result.RawWPM > 0 ? result.RawWPM : null,
            result.IsMeasured ? result.ComprehensionScore : null,
            result.IsMeasured && result.RawWPM > 0 ? result.WeightedKDP : null,
            result.IsMeasured
                ? xp ?? SpeedReadingExerciseSessionRules.CalculateXp(
                    score ?? CalculateServerScore(result),
                    result.ComprehensionScore,
                    result.TimeSpentSeconds)
                : 0,
            [],
            false,
            null,
            ToPublicJson(state),
            score.HasValue ? "Egzersiz tamamlandı." : "Bu oturum daha önce tamamlandı.",
            null,
            result.IsMeasured
                ? nameof(SpeedReadingMeasurementStatus.Measured)
                : nameof(SpeedReadingMeasurementStatus.NotMeasured));

    private static decimal CalculateServerScore(LegacyStudentExerciseResult result)
    {
        var comprehension = Math.Clamp(result.ComprehensionScore, 0, 100);
        var speed = Math.Clamp(result.RawWPM / 5m, 0, 100);
        return Math.Round(Math.Clamp((comprehension * 0.6m) + (speed * 0.4m), 0, 100), 2);
    }

    private static bool IsTimedOut(LegacyExerciseSession session, SessionState state, DateTime now) =>
        state.TimingStartsOnAction
            ? state.TimingStartedAt.HasValue
                && session.TimeLimitSeconds is > 0
                && SpeedReadingExerciseSessionRules.CalculateActiveSeconds(
                    state.TimingStartedAt.Value,
                    now,
                    GetTimingPausedSeconds(session, state),
                    session.PausedAt,
                    session.Status == (int)ExerciseSessionStatus.Paused) > session.TimeLimitSeconds.Value
            : session.TimeLimitSeconds is > 0
                && SpeedReadingExerciseSessionRules.CalculateActiveSeconds(
                    session.StartTime,
                    now,
                    GetTimingPausedSeconds(session, state),
                    session.PausedAt,
                    session.Status == (int)ExerciseSessionStatus.Paused) > session.TimeLimitSeconds.Value;

    private static void EnsureTimingStarted(
        LegacyExerciseSession session,
        SessionState state,
        DateTime now)
    {
        if (state.TimingStartedAt.HasValue)
        {
            return;
        }

        state.TimingStartedAt = now.ToUniversalTime();
        state.TimingPausedSecondsBeforeStart = Math.Max(0, session.TotalPausedSeconds);
    }

    private static int GetTimingPausedSeconds(LegacyExerciseSession session, SessionState state) =>
        state.TimingStartedAt.HasValue
            ? Math.Max(0, session.TotalPausedSeconds - Math.Max(0, state.TimingPausedSecondsBeforeStart))
            : Math.Max(0, session.TotalPausedSeconds);

    private static DateTime GetTimingStartTime(
        LegacyExerciseSession session,
        SessionState state,
        DateTime now) =>
        state.TimingStartedAt
        ?? (state.TimingStartsOnAction ? now : session.StartTime);

    private static bool SupportsServerReadingMeasurement(SessionState state) =>
        ReadingExerciseTypes.Contains(state.ExerciseTypeName)
        && state.ReadingStartTime.HasValue
        && state.ReadingEndTime.HasValue;

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
        state.IsAssessmentMode
            ? SpeedReadingContentSecurity.SanitizeFocusAssessmentJson(
                JsonSerializer.SerializeToElement(state, JsonOptions))
            : RemoveAssessmentKeys(JsonSerializer.SerializeToElement(state, JsonOptions));

    private static JsonElement RemoveAssessmentKeys(JsonElement element) =>
        SpeedReadingContentSecurity.SanitizeAssessmentJson(element);

    private static JsonElement ReadObject(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return default;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.Object)
                return property.Value;
        }

        return default;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString();
        }

        return null;
    }

    private static int[] ReadIntArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return [];

        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                || property.Value.ValueKind != JsonValueKind.Array)
                continue;

            return property.Value.EnumerateArray()
                .Where(item => item.TryGetInt32(out _))
                .Select(item => item.GetInt32())
                .ToArray();
        }

        return [];
    }

    private static string[] ReadStringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return [];

        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                || property.Value.ValueKind != JsonValueKind.Array)
                continue;

            return property.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .ToArray();
        }

        return [];
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
        decimal? currentWpm = null,
        JsonElement? feedbackData = null) =>
        new(true, message, null, nextStep, nextWord, isCompleted, isCorrect, null, null, currentWpm, feedbackData);

    private static ExerciseActionValidationResponse Invalid(string message) =>
        new(false, message, null, null, null, false, false, null, null, null, null);

    private sealed class SessionState
    {
        public Guid ExerciseId { get; set; }
        public string ExerciseTypeName { get; set; } = string.Empty;
        public bool IsAssessmentMode { get; set; }
        public Guid? AssessmentAttemptId { get; set; }
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
        public bool TimingStartsOnAction { get; set; }
        public DateTime? TimingStartedAt { get; set; }
        public int TimingPausedSecondsBeforeStart { get; set; }
        public DateTime? ReadingStartTime { get; set; }
        public DateTime? ReadingEndTime { get; set; }
        public decimal? FinalWpm { get; set; }
        public decimal? ComprehensionScore { get; set; }
        public decimal? WeightedKdp { get; set; }
        public string[] Words { get; set; } = [];
        public string FocusMode { get; set; } = "position";
        public int FocusNLevel { get; set; } = 1;
        public int FocusSpeedMs { get; set; } = 1500;
        public DateTime? FocusStartTime { get; set; }
        public int[] PositionSequence { get; set; } = [];
        public string[] WordSequence { get; set; } = [];
        public int FocusPresentedIndex { get; set; } = -1;
        public DateTime? FocusPresentedAt { get; set; }
        public int FocusPausedSecondsAtPresentation { get; set; }
        public int[] PositionTargetIndices { get; set; } = [];
        public int[] WordTargetIndices { get; set; } = [];
        public int? FocusLastPositionIndex { get; set; }
        public int? FocusLastWordIndex { get; set; }
        public List<FocusResponse> FocusResponses { get; set; } = [];
        public bool FocusCompleted { get; set; }
        public string VisualExpansionStimulusType { get; set; } = "letter";
        public string VisualExpansionPattern { get; set; } = "horizontal";
        public int VisualExpansionDisplayDurationMs { get; set; } = 250;
        public int VisualExpansionRound { get; set; }
        public string[] VisualExpansionExpectedStimuli { get; set; } = [];
        public DateTime? VisualExpansionPresentedAt { get; set; }
        public int VisualExpansionPausedSecondsAtPresentation { get; set; }
        public List<SessionQuestion> Questions { get; set; } = [];
        public List<SessionAnswer> Answers { get; set; } = [];
        public List<VisualizationSceneState> VisualizationScenes { get; set; } = [];
        public Dictionary<string, JsonElement>? CustomData { get; set; }
    }

    private sealed class VisualizationSceneState
    {
        public string SceneId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int Duration { get; set; }
        public int DisplayOrder { get; set; }
        public List<string> Steps { get; set; } = [];
        public int StepDurationMs { get; set; } = 3000;
        public List<VisualizationQuestionState> Questions { get; set; } = [];
    }

    private sealed class VisualizationQuestionState
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public List<string> Options { get; set; } = [];
        public string CorrectAnswer { get; set; } = string.Empty;
        public string QuestionType { get; set; } = "detail";
        public int DisplayOrder { get; set; }
        public string? HintText { get; set; }
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

    private sealed class FocusResponse
    {
        public string Channel { get; set; } = string.Empty;
        public int Index { get; set; }
        public bool IsCorrect { get; set; }
        public bool IsMiss { get; set; }
    }
}
