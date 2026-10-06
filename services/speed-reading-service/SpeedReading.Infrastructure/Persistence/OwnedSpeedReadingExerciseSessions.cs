using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.Content;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Gamification;
using SpeedReading.Domain.Sessions;
using SpeedReading.Domain.Vocabulary;
using OwnedExerciseSessionStatus = SpeedReading.Domain.Sessions.ExerciseSessionStatus;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Core exercise/session use case backed only by the owned Speed Reading
/// database. Verified completion also updates gamification in the same unit of work.
/// </summary>
internal sealed partial class OwnedSpeedReadingExerciseSessions(
    OwnedSpeedReadingDbContext db) : ISpeedReadingExerciseSessions
{
    private const string TimeoutAnswer = "__timeout__";
    private const int MaxCompletionConflictRetries = 2;
    private const int MaxActionConflictRetries = 2;
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
        "SubvocalizationReduction",
        "AdaptiveFluency"
    };

    public async Task<StartExerciseSessionResponse> StartAsync(
        Guid studentId,
        StartExerciseSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (studentId == Guid.Empty || request.ExerciseId == Guid.Empty)
            throw new ArgumentException("A valid student and exercise are required.");
        var profileAgeGroupId = await GetAgeGroupIdAsync(studentId, cancellationToken);
        Guid? pinnedReadingTextId = null;
        AssessmentContentSnapshot? assessmentSnapshot = null;
        if (request.AssessmentAttemptId.HasValue)
        {
            var pinnedItem = await (
                from attempt in db.AssessmentAttempts.AsNoTracking()
                join formItem in db.AssessmentAttemptExercises.AsNoTracking()
                    on attempt.Id equals formItem.AssessmentAttemptId
                where attempt.Id == request.AssessmentAttemptId.Value
                    && attempt.StudentId == studentId
                    && attempt.Status == AssessmentAttemptStatus.InProgress
                    && formItem.ExerciseId == request.ExerciseId
                select new { formItem.ReadingTextId, formItem.ContentSnapshotJson })
                .SingleOrDefaultAsync(cancellationToken);
            if (pinnedItem is null)
            {
                throw new KeyNotFoundException("Assessment exercise is not part of the active form.");
            }

            pinnedReadingTextId = pinnedItem.ReadingTextId;
            assessmentSnapshot = DeserializeAssessmentSnapshot(pinnedItem.ContentSnapshotJson);
            if (assessmentSnapshot is not null
                && assessmentSnapshot.Exercise.Id != request.ExerciseId)
            {
                throw new InvalidOperationException("Assessment content snapshot does not match the requested exercise.");
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
                    && studentAssignment.IsActive
                    && assignment.IsActive
                    && assignment.ExerciseId == request.ExerciseId
                select studentAssignment.Id)
                .AnyAsync(cancellationToken);
            if (!assignmentMatches)
            {
                throw new KeyNotFoundException("Assignment not found or does not belong to the student.");
            }
        }

        string exerciseTypeName;
        string exerciseEngineType;
        string configurationJson;
        int difficultyLevel;
        if (assessmentSnapshot is not null)
        {
            exerciseTypeName = assessmentSnapshot.Exercise.TypeName;
            exerciseEngineType = assessmentSnapshot.Exercise.EngineType;
            configurationJson = assessmentSnapshot.Exercise.ConfigurationJson;
            difficultyLevel = assessmentSnapshot.Exercise.DifficultyLevel;
        }
        else
        {
            var exercise = await db.Exercises
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.ExerciseId
                    && item.IsActive
                    && !item.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Exercise not found.");
            var exerciseType = await db.ExerciseTypes
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == exercise.ExerciseTypeId
                    && item.IsActive
                    && !item.IsDeleted,
                    cancellationToken)
                ?? throw new KeyNotFoundException("Exercise type not found.");
            exerciseTypeName = exerciseType.Name;
            exerciseEngineType = exerciseType.EngineType;
            configurationJson = exercise.ConfigurationJson;
            difficultyLevel = exercise.DifficultyLevel;
        }

        if (string.IsNullOrWhiteSpace(exerciseEngineType))
        {
            var snapshotConfig = ParseJsonOrEmpty(configurationJson);
            exerciseEngineType = ReadString(snapshotConfig, "engineType")
                ?? ReadString(ReadObject(snapshotConfig, "engineConfig"), "engineType")
                ?? exerciseTypeName;
        }

        if (request.ReadingTextId.HasValue && assessmentSnapshot is null)
        {
            var strictTextLevel = IsGrouping(new SessionState { EngineType = exerciseEngineType, ExerciseTypeName = exerciseTypeName })
                || ExerciseConfigurationRules.NormalizeEngineType(exerciseEngineType) is "text_fade" or "regression_reduction";
            var readingTextMatches = await db.ReadingTexts
                .AsNoTracking()
                .AnyAsync(item => item.Id == request.ReadingTextId.Value
                    && item.IsActive
                    && !item.IsDeleted
                    && (!strictTextLevel || item.DifficultyLevel == difficultyLevel)
                    && (!profileAgeGroupId.HasValue
                        || item.TargetAgeGroupId == null
                        || item.TargetAgeGroupId == profileAgeGroupId.Value)
                    && (item.ExerciseId == null || item.ExerciseId == request.ExerciseId), cancellationToken);
            if (!readingTextMatches)
            {
                throw new KeyNotFoundException("Reading text not found or does not belong to the exercise.");
            }
        }

        // Starting a session is retried when a player is refreshed or the
        // browser restores its previous route. Serialize starts for the same
        // student/exercise pair so two tabs cannot both pass the active check
        // before either insert is committed.
        // PostgreSQL is the production provider and gives us a transaction
        // scoped advisory lock for the active-session check. The in-memory
        // provider used by application tests does not implement transactions
        // or SQL commands, so keep the same behavior there without trying to
        // execute provider-specific SQL.
        var isPostgres = db.Database.ProviderName?.Contains(
            "Npgsql",
            StringComparison.OrdinalIgnoreCase) == true;

        // Npgsql uses a retrying execution strategy in production. EF Core
        // requires the transaction and all of its reads/writes to run inside
        // that strategy so a transient failure can be retried safely.
        if (isPostgres)
        {
            var executionStrategy = db.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(
                () => StartSessionWithConcurrencyGuardAsync(cancellationToken));
        }

        return await StartSessionWithConcurrencyGuardAsync(cancellationToken);

        async Task<StartExerciseSessionResponse> StartSessionWithConcurrencyGuardAsync(
            CancellationToken token)
        {
            await using var startTransaction = isPostgres
                ? await db.Database.BeginTransactionAsync(token)
                : null;
            if (startTransaction is not null)
            {
                var startLockKey = $"speed-reading-session:{studentId:N}:{request.ExerciseId:N}";
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtext({startLockKey}))",
                    token);
            }

            // Re-check completion after taking the same lock as the active-session
            // query. A completion can commit between the initial validation above
            // and this start attempt; without this check a fresh session could be
            // created for an already completed assessment exercise.
            if (request.AssessmentAttemptId.HasValue)
            {
                var alreadyCompleted = await db.ExerciseSessionResults
                    .AsNoTracking()
                    .AnyAsync(item => item.StudentId == studentId
                        && item.ExerciseId == request.ExerciseId
                        && item.AssessmentAttemptId == request.AssessmentAttemptId.Value
                        && item.IsAssessmentMode,
                        token);
                if (alreadyCompleted)
                    throw new InvalidOperationException("Assessment exercise has already been completed.");
            }

            // Reuse the same session when the request carries the identical
            // assignment/assessment context; an unrelated active session must
            // still be rejected. Check all active sessions so a stale duplicate
            // cannot hide an older session with the matching context.
            var activeSessions = await db.ExerciseSessions
                .AsNoTracking()
                .Where(item => item.StudentId == studentId
                    && item.ExerciseId == request.ExerciseId
                    && (item.Status == OwnedExerciseSessionStatus.Active
                        || item.Status == OwnedExerciseSessionStatus.Paused))
                .OrderByDescending(item => item.StartTime)
                .ToListAsync(token);
            var activeSession = activeSessions.FirstOrDefault();
            var matchingSession = activeSessions.FirstOrDefault(item =>
                item.AssessmentAttemptId == request.AssessmentAttemptId
                && item.StudentAssignmentId == request.StudentAssignmentId);
            if (matchingSession is not null
                && ((IsTachistoscope(exerciseTypeName, exerciseEngineType, ParseJsonOrEmpty(configurationJson))
                        && DeserializeState(matchingSession.SessionDataJson).Tachistoscope is null)
                    || (IsScanning(new SessionState { EngineType = exerciseEngineType })
                        && DeserializeState(matchingSession.SessionDataJson).ScanningRounds.Count == 0)
                    || (IsGrouping(DeserializeState(matchingSession.SessionDataJson))
                        && DeserializeState(matchingSession.SessionDataJson).GroupingDisplayPaceWpm <= 0)
                    || (IsTextFade(DeserializeState(matchingSession.SessionDataJson))
                        && DeserializeState(matchingSession.SessionDataJson).FadeDisplayPaceWpm <= 0)
                    || (IsVisualExpansionExercise(DeserializeState(matchingSession.SessionDataJson))
                        && DeserializeState(matchingSession.SessionDataJson).VisualExpansionProtocolVersion != 1)
                    || (IsValidatedFixation(DeserializeState(matchingSession.SessionDataJson))
                        && DeserializeState(matchingSession.SessionDataJson).FixationProtocolVersion != 1)))
            {
                // Legacy client-scored attempts cannot be verified by the new round protocol.
                // Preserve their history, but start a fresh authoritative attempt.
                var legacySession = await db.ExerciseSessions.SingleAsync(item => item.Id == matchingSession.Id, token);
                if (legacySession.AssessmentAttemptId.HasValue)
                {
                    var upgradedState = await CreateSessionStateAsync(request.ExerciseId, exerciseTypeName,
                        exerciseEngineType, difficultyLevel, configurationJson, null, assessmentSnapshot,
                        true, request.CustomData, profileAgeGroupId, token);
                    legacySession.RestartForVerification(upgradedState.TotalSteps, upgradedState.TimeLimitSeconds,
                        JsonSerializer.Serialize(upgradedState, JsonOptions), DateTime.UtcNow);
                    await db.SaveChangesAsync(token);
                    if (startTransaction is not null) await startTransaction.CommitAsync(token);
                    return new StartExerciseSessionResponse(legacySession.Id, legacySession.ExerciseId, exerciseTypeName,
                        Application.ExerciseSessions.ExerciseSessionStatus.Active, legacySession.StartTime,
                        legacySession.TotalSteps, ToPublicJson(upgradedState),
                        SpeedReadingContentSecurity.SanitizeFocusAssessmentJson(ParseJsonOrEmpty(configurationJson)));
                }
                legacySession.Abandon(DateTime.UtcNow);
                await db.SaveChangesAsync(token);
                activeSessions.Remove(matchingSession);
                matchingSession = null;
                activeSession = activeSessions.FirstOrDefault();
            }
            if (matchingSession is not null)
            {
                var existingState = DeserializeState(matchingSession.SessionDataJson);
                if (existingState.Tachistoscope is { PresentedAt: not null } pending)
                {
                    pending.LastStimulus = pending.ExpectedStimulus;
                    pending.ExpectedStimulus = string.Empty;
                    pending.PresentedAt = null;
                    var tracked = await db.ExerciseSessions.SingleAsync(item => item.Id == matchingSession.Id, token);
                    tracked.SetState(JsonSerializer.Serialize(existingState, JsonOptions), tracked.CustomDataJson);
                    InvalidateTachistoscopePresentations(tracked);
                    await db.SaveChangesAsync(token);
                    if (startTransaction is not null) await startTransaction.CommitAsync(token);
                }
                var existingConfiguration = ParseJsonOrEmpty(configurationJson);
                return new StartExerciseSessionResponse(
                    matchingSession.Id,
                    matchingSession.ExerciseId,
                    exerciseTypeName,
                    (Application.ExerciseSessions.ExerciseSessionStatus)matchingSession.Status,
                    matchingSession.StartTime,
                    matchingSession.TotalSteps,
                    ToPublicJson(existingState),
                    existingState.IsAssessmentMode
                        ? SpeedReadingContentSecurity.SanitizeFocusAssessmentJson(existingConfiguration)
                        : RemoveAssessmentKeys(existingConfiguration));
            }
            if (activeSession is not null)
                throw new InvalidOperationException("An active session already exists for this exercise.");

            var readingTextId = assessmentSnapshot is not null
                ? assessmentSnapshot.ReadingText?.Id
                : pinnedReadingTextId ?? request.ReadingTextId;
            var parsedConfiguration = ParseJsonOrEmpty(configurationJson);
            if (IsTachistoscope(exerciseTypeName, exerciseEngineType, parsedConfiguration)) readingTextId = null;
            var requiresReadingText = IsReadingExerciseFlow(
                exerciseTypeName,
                exerciseEngineType,
                parsedConfiguration);
            if (assessmentSnapshot is null
                && !readingTextId.HasValue
                && requiresReadingText)
            {
                var normalizedEngineType = ExerciseConfigurationRules.NormalizeEngineType(exerciseEngineType);
                var isComprehension = normalizedEngineType == "reading_comprehension";
                var isScanning = normalizedEngineType is "scan_find" or "scanning" or "skimming";
                var isGrouping = normalizedEngineType == "word_highlight"
                    && exerciseTypeName.Equals("Chunking", StringComparison.OrdinalIgnoreCase);
                var isTextFade = normalizedEngineType == "text_fade";
                var isRegression = normalizedEngineType == "regression_reduction";
                var requiresScorableQuestions = ExerciseConfigurationRules.ResolveReadingPurpose(
                    normalizedEngineType,
                    ReadString(ReadObject(parsedConfiguration, "engineConfig"), "readingPurpose")
                        ?? ReadString(parsedConfiguration, "readingPurpose"),
                    request.AssessmentAttemptId.HasValue) == "evaluation";
                readingTextId = await db.ReadingTexts
                    .AsNoTracking()
                    .Where(item => item.IsActive
                        && !item.IsDeleted
                        && item.Content != string.Empty
                        && (!(isComprehension || isScanning || isGrouping || isTextFade || isRegression) || item.DifficultyLevel == difficultyLevel)
                        && (!profileAgeGroupId.HasValue
                            || item.TargetAgeGroupId == null
                            || item.TargetAgeGroupId == profileAgeGroupId.Value)
                        && (item.ExerciseId == null || item.ExerciseId == request.ExerciseId)
                        && (!requiresScorableQuestions || db.ReadingQuestions.Any(question =>
                            question.ReadingTextId == item.Id
                            && !question.IsDeleted
                            && (question.CorrectAnswer.Trim().ToUpper() == "A"
                                || question.CorrectAnswer.Trim().ToUpper() == "B"
                                || question.CorrectAnswer.Trim().ToUpper() == "C"
                                || question.CorrectAnswer.Trim().ToUpper() == "D"))))
                    .OrderBy(item => isScanning || isGrouping || isTextFade || isRegression
                        ? db.ExerciseSessions.Count(history => history.StudentId == studentId && history.ReadingTextId == item.Id)
                        : isComprehension
                        ? db.ReadingSessions.Count(history => history.UserId == studentId && history.ReadingTextId == item.Id)
                        : 0)
                    .ThenByDescending(item => item.ExerciseId == request.ExerciseId)
                    .ThenByDescending(item => item.DifficultyLevel == difficultyLevel)
                    .ThenBy(item => item.Id)
                    .Select(item => (Guid?)item.Id)
                    .FirstOrDefaultAsync(token);
                if (isGrouping && !readingTextId.HasValue)
                    throw new InvalidOperationException("Seçilen Gruplama seviyesine uygun aktif metin bulunamadı.");
                if (isTextFade && !readingTextId.HasValue)
                    throw new InvalidOperationException("Seçilen Metin Solma seviyesine uygun aktif metin bulunamadı.");
                if (isRegression && !readingTextId.HasValue)
                    throw new InvalidOperationException("Seçilen Regresyon Azaltma seviyesine uygun aktif metin bulunamadı.");
            }

            var state = await CreateSessionStateAsync(
                request.ExerciseId,
                exerciseTypeName,
                exerciseEngineType,
                difficultyLevel,
                configurationJson,
                readingTextId,
                assessmentSnapshot,
                request.AssessmentAttemptId.HasValue,
                request.CustomData,
                profileAgeGroupId,
                token);
            var now = DateTime.UtcNow;
            var session = ExerciseSession.Start(
                studentId,
                request.ExerciseId,
                readingTextId,
                state.TotalSteps,
                now,
                state.TimeLimitSeconds,
                studentAssignmentId: request.StudentAssignmentId,
                assessmentAttemptId: request.AssessmentAttemptId);
            session.SetState(
                JsonSerializer.Serialize(state, JsonOptions),
                SerializeOptional(request.CustomData));
            session.SetProcessedActions("{}");
            db.ExerciseSessions.Add(session);
            try
            {
                await db.SaveChangesAsync(token);
            }
            catch (DbUpdateException exception) when (IsAssessmentSessionConflict(exception))
            {
                db.ChangeTracker.Clear();
                throw new InvalidOperationException("Assessment exercise has already been started.");
            }

            if (startTransaction is not null)
                await startTransaction.CommitAsync(token);

            return new StartExerciseSessionResponse(
                session.Id,
                session.ExerciseId,
                exerciseTypeName,
                Application.ExerciseSessions.ExerciseSessionStatus.Active,
                session.StartTime,
                session.TotalSteps,
                ToPublicJson(state),
                state.IsAssessmentMode
                    ? SpeedReadingContentSecurity.SanitizeFocusAssessmentJson(ParseJsonOrEmpty(configurationJson))
                    : RemoveAssessmentKeys(ParseJsonOrEmpty(configurationJson)));
        }
    }

    public Task<ExerciseActionValidationResponse> ValidateActionAsync(
        Guid studentId,
        Guid sessionId,
        ExerciseActionRequest request,
        CancellationToken cancellationToken = default) =>
        ValidateActionAsync(studentId, sessionId, request, cancellationToken, actionRetryCount: 0);

    private async Task<ExerciseActionValidationResponse> ValidateActionAsync(
        Guid studentId,
        Guid sessionId,
        ExerciseActionRequest request,
        CancellationToken cancellationToken,
        int actionRetryCount)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        if (TryGetCachedAction(session.ProcessedActionsJson, request.ActionId, out var cached))
            return cached!;

        var state = DeserializeState(session.SessionDataJson);
        var now = DateTime.UtcNow;
        if (IsTimedOut(session, state, now))
        {
            session.MarkTimedOut(now);
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("This exercise session has timed out.");
        }

        if (session.Status != OwnedExerciseSessionStatus.Active)
            throw new InvalidOperationException("Actions can only be submitted to an active session.");

        var actionName = request.Action?.Trim().ToLowerInvariant();
        var response = IsScanning(state)
            ? ValidateScanning(session, state, request, now)
            : state.Tachistoscope is not null && actionName is not ("tachistoscope_present" or "tachistoscope_answer")
            ? Invalid("Takistoskop yalnız doğrulanmış tur aksiyonlarıyla ilerler.")
            : actionName == "vocabulary_review"
            ? await ReviewVocabularyAsync(session, state, request, studentId, now, cancellationToken)
            : actionName switch
            {
                "start_reading" => StartReading(session, state, now),
                "finish_reading" => FinishReading(session, state, now, request.IsTimeout),
                "adaptive_next_stage" => AdvanceAdaptiveStage(session, state),
                "focus_start" => StartFocus(session, state, now),
                "focus_step" => AdvanceFocus(session, state, request, now),
                "visual_expansion_present" => PresentVisualExpansion(session, state, now),
                "visual_expansion_answer" => AnswerVisualExpansion(session, state, request, now),
                "fixation_present" => PresentFixation(session, state, now),
                "fixation_answer" => AnswerFixation(session, state, request, now),
                "tachistoscope_present" => PresentTachistoscope(state, request, now),
                "tachistoscope_answer" => AnswerTachistoscope(session, state, request, now),
                "answer_question" => AnswerQuestion(session, state, request),
                "position_match" => ValidateFocusMatch(session, state, request, "position", now),
                "word_match" => ValidateFocusMatch(session, state, request, "word", now),
                "match_attempt" => ValidateFocusMatch(session, state, request, "position", now),
                "complete" when IsFocusExercise(state) => CompleteFocus(session, state),
                "advance" => Advance(session, state),
                "grid_click" when state.CurrentNumber.HasValue => ClickGrid(session, state, request),
                "grid_click" => Invalid("Grid cell action is not valid for this exercise."),
                _ when state.CurrentNumber.HasValue => Invalid("Grid cell action is required."),
                _ => Invalid("Unsupported exercise action.")
            };

        PersistSessionAnswers(session);

        // Start the server clock only after an action was understood. A wrong
        // grid attempt still counts as a real attempt and therefore starts it.
        if (response.IsValid || actionName == "grid_click" && state.CurrentNumber.HasValue)
            EnsureTimingStarted(session, state, now);

        session.SetState(JsonSerializer.Serialize(state, JsonOptions), session.CustomDataJson);
        if (request.ActionId is { } actionId && actionId != Guid.Empty)
            session.SetProcessedActions(RecordCachedAction(session.ProcessedActionsJson, actionId, response));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) when (actionRetryCount < MaxActionConflictRetries)
        {
            db.ChangeTracker.Clear();
            return await ValidateActionAsync(
                studentId,
                sessionId,
                request,
                cancellationToken,
                actionRetryCount + 1);
        }
        catch (DbUpdateException exception)
            when (actionRetryCount < MaxActionConflictRetries && IsActionConflict(exception))
        {
            db.ChangeTracker.Clear();
            return await ValidateActionAsync(
                studentId,
                sessionId,
                request,
                cancellationToken,
                actionRetryCount + 1);
        }
        return response;
    }

    public Task<SpeedReading.Application.ExerciseSessions.ExerciseSessionResult> CompleteAsync(
        Guid studentId,
        Guid sessionId,
        CompleteExerciseSessionRequest request,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(studentId, sessionId, request, cancellationToken, completionRetryCount: 0);

    private static BusinessRuleException IncompleteSession(string message) =>
        new("ExerciseSession.Incomplete", message);

    private async Task<SpeedReading.Application.ExerciseSessions.ExerciseSessionResult> CompleteAsync(
        Guid studentId,
        Guid sessionId,
        CompleteExerciseSessionRequest request,
        CancellationToken cancellationToken,
        int completionRetryCount)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        var isAssessmentSession = session.AssessmentAttemptId.HasValue;
        if (request.IsAssessmentMode != isAssessmentSession)
            throw new InvalidOperationException("Assessment mode must match the server-owned session.");
        var state = DeserializeState(session.SessionDataJson);
        var now = DateTime.UtcNow;
        if (IsTimedOut(session, state, now))
        {
            session.MarkTimedOut(now);
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("This exercise session has timed out.");
        }

        var existingResult = await db.ExerciseSessionResults
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.SessionId == session.Id, cancellationToken);
        if (existingResult is not null)
            return ToResult(existingResult, session, state);

        if ((IsGrouping(state) || IsTextFade(state)) && (DisplayPace(state) <= 0 || state.ReadingMinimumMs <= 0
            || !state.ReadingStartTime.HasValue || !state.ReadingEndTime.HasValue))
            throw IncompleteSession("Doğrulanmış gösterim tamamlanmadan oturum kaydedilemez. Egzersizi yeniden başlatın.");

        if (IsScanning(state))
        {
            if (state.ScanningRounds.Count == 0)
                throw IncompleteSession("Bu tarama oturumu doğrulanmış hedef içermiyor. Egzersizi yeniden açın.");
            if (ScanningExpired(session, state, now))
            {
                UpdateScanningTime(session, state, now);
                state.ReadingIncomplete = true;
            }
            if (!ScanningComplete(state) && !state.ReadingIncomplete)
                throw IncompleteSession("Tarama hedefleri doğrulanmadan oturum tamamlanamaz.");
            state.Questions.Clear();
        }

        if (state.CurrentNumber.HasValue && state.CurrentNumber.Value <= state.TotalSteps)
            throw IncompleteSession("All grid targets must be completed before the session can be completed.");
        if ((state.Tachistoscope is not null || state.ExerciseTypeName.Equals("Tachistoscope", StringComparison.OrdinalIgnoreCase))
            && (state.Tachistoscope is null || state.Tachistoscope.Round < state.TotalSteps))
            throw IncompleteSession("All tachistoscope rounds must be validated before completion.");
        if (IsFocusExercise(state) && !state.FocusCompleted)
        {
            if (IsObservationOnlyMotionPath(state) && state.FixationPeripheralCount == 0)
            {
                // motion_path is a timed, observation-only exercise. It has
                // no server stimulus/response sequence to validate, so its
                // normal client completion is the authoritative completion
                // signal and is intentionally stored as NotMeasured.
                state.FocusCompleted = true;
            }
            else
            {
                throw IncompleteSession("The focus exercise must be completed through its validated action flow.");
            }
        }
        if (IsVisualExpansionExercise(state)
            && (state.VisualExpansionProtocolVersion != 1 || state.VisualExpansionRound < state.TotalSteps
                || state.VisualExpansionRoundResults.Count != state.TotalSteps))
            throw IncompleteSession("All visual expansion rounds must be validated before completion.");
        if (IsValidatedFixation(state) && (!state.FocusCompleted || state.FixationProtocolVersion != 1
            || state.FixationRoundResults.Count != state.TotalSteps))
            throw IncompleteSession("All fixation rounds must be validated before completion.");
        if (state.VocabularyWords.Count > 0
            && state.VocabularyWords.Any(word => state.Answers.All(answer => answer.QuestionId != word.Id)))
            throw IncompleteSession("All vocabulary rounds must be reviewed before completion.");
        if (IsAdaptiveFluency(state) && !state.AdaptiveCompleted)
            throw IncompleteSession("The adaptive fluency flow must be completed before the session can be completed.");

        // Sessions started before this rule may still contain reading-text questions.
        if (string.Equals(state.ExerciseTypeName, "Tachistoscope", StringComparison.OrdinalIgnoreCase))
            state.Questions.Clear();

        var answers = ResolveAnswers(session, state, request.QuestionAnswers);
        PersistSessionAnswers(session);
        if (state.Questions.Count > 0 && answers.Count != state.Questions.Count)
            throw IncompleteSession("All questions in the session must be answered.");

        if (ExerciseConfigurationRules.NormalizeEngineType(state.EngineType) == "reading_comprehension")
        {
            var readingMs = state.ReadingStartTime.HasValue && state.ReadingEndTime.HasValue
                ? (state.ReadingEndTime.Value - state.ReadingStartTime.Value).TotalMilliseconds
                    - (state.ReadingPausedMilliseconds ?? state.ReadingPausedSeconds * 1000L)
                : -1;
            // Answers can be retained, but cannot prove that the reading phase was completed.
            state.ReadingIncomplete |= readingMs < state.ReadingMinimumMs
                || (state.ReadingMaximumMs > 0 && readingMs >= state.ReadingMaximumMs);
        }

        if (request.CustomData is not null)
        {
            var previousCustomData = ParseJsonOrEmpty(session.CustomDataJson ?? "{}");
            var unverified = ReadProperty(previousCustomData, "previousUnverifiedAttempt");
            var customJson = state.Tachistoscope is not null && unverified.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Serialize(new {
                    previousUnverifiedAttempt = unverified,
                    previousCustomData = ReadProperty(previousCustomData, "previousCustomData").ValueKind == JsonValueKind.Undefined
                        ? (JsonElement?)null : ReadProperty(previousCustomData, "previousCustomData"),
                    currentCustomData = request.CustomData
                }, JsonOptions)
                : SerializeOptional(request.CustomData);
            session.SetState(session.SessionDataJson, customJson);
        }
        session.Complete(now);

        if (state.ReadingPausedAt.HasValue)
        {
            if (state.ReadingPausedMilliseconds.HasValue)
                state.ReadingPausedMilliseconds += (long)Math.Max(0, (now - state.ReadingPausedAt.Value).TotalMilliseconds);
            state.ReadingPausedSeconds += Math.Max(
                0,
                (int)Math.Round((now - state.ReadingPausedAt.Value).TotalSeconds));
            state.ReadingPausedAt = null;
        }

        var pausedReadingSeconds = state.ReadingStartTime.HasValue
            ? state.ReadingPausedSeconds
            : GetTimingPausedSeconds(session, state);
        var timeSpent = IsScanning(state) && state.ScanningElapsedMs.HasValue
            ? Math.Max(0, (int)Math.Round(state.ScanningElapsedMs.Value / 1000d))
            : SpeedReadingExerciseSessionRules.CalculateReadingSeconds(
            GetTimingStartTime(session, state, now),
            now,
            state.ReadingStartTime,
            state.ReadingEndTime,
            pausedReadingSeconds);
        var accuracy = IsScanning(state) ? ScanningAccuracy(state)
            : SpeedReadingExerciseSessionRules.CalculateAccuracy(session.CorrectCount, session.IncorrectCount);
        var wordsRead = state.Tachistoscope is not null || IsScanning(state)
            || state.EngineType == "regression_reduction" ? null : state.WordCount > 0 ? (int?)state.WordCount : null;
        var adaptiveTransferResult = IsAdaptiveFluency(state)
            ? state.AdaptiveStageResults.SingleOrDefault(item => item.Stage == 3)
            : null;
        var rawWpmCandidate = adaptiveTransferResult?.Wpm
            ?? CalculateReadingWpm(state, timeSpent);
        var measurementStatus = SpeedReadingExerciseSessionRules.ResolveMeasurementStatus(
            state.Questions.Count,
            session.CorrectCount,
            session.IncorrectCount,
            hasValidWpm: (rawWpmCandidate.HasValue && SupportsServerReadingMeasurement(state))
                || (IsFocusExercise(state) && state.FocusCompleted && HasFocusStimulus(state))
                || (IsVisualExpansionExercise(state) && state.VisualExpansionRound >= state.TotalSteps
                    && state.VisualExpansionRoundResults.Count == state.TotalSteps)
                || (IsValidatedFixation(state) && state.FocusCompleted && session.CurrentStep > 0));
        var rawWpm = measurementStatus == SpeedReadingMeasurementStatus.Measured
            ? rawWpmCandidate
            : null;
        var comprehension = IsAdaptiveFluency(state) && state.AdaptiveTransferComprehension.HasValue
            ? state.AdaptiveTransferComprehension.Value
            : state.Questions.Count > 0
            ? Math.Round((decimal)answers.Count(item => item.IsCorrect) / state.Questions.Count * 100, 2)
            : 0;
        var score = measurementStatus == SpeedReadingMeasurementStatus.NotMeasured
            ? (decimal?)null
            : state.Questions.Count > 0
            ? SpeedReadingExerciseSessionRules.CalculateCompositeScore(comprehension, rawWpm)
            : IsScanning(state) ? Math.Max(0, accuracy - session.IncorrectCount * 10m) : accuracy;
        var weightedKdp = rawWpm.HasValue ? Math.Round(rawWpm.Value * comprehension / 100, 2) : (decimal?)null;
        var xpAwarded = measurementStatus == SpeedReadingMeasurementStatus.Measured && !state.ReadingIncomplete
            ? SpeedReadingExerciseSessionRules.CalculateXp(score ?? 0, accuracy, timeSpent)
            : 0;
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
            score ?? 0,
            now,
            JsonSerializer.Serialize(answers, JsonOptions),
            "[]",
            isAssessmentSession,
            measurementStatus == SpeedReadingMeasurementStatus.Measured,
            assessmentAttemptId: session.AssessmentAttemptId);
        db.ExerciseSessionResults.Add(result);
        if (!isAssessmentSession
            && session.ReadingTextId is not null
            && !IsScanning(state)
            && IsReadingExerciseFlow(state))
        {
            AddReadingSessionRecord(
                session,
                state,
                timeSpent,
                rawWpm,
                comprehension,
                now);
        }
        if (session.StudentAssignmentId.HasValue && !state.ReadingIncomplete)
        {
            var studentAssignment = await db.StudentAssignments.SingleOrDefaultAsync(
                item => item.Id == session.StudentAssignmentId.Value
                    && item.StudentId == studentId
                    && item.IsActive,
                cancellationToken);
            studentAssignment?.Complete(result.Id, score ?? 0, weightedKdp ?? 0, now);
        }

        var isVerifiedCompletion = measurementStatus == SpeedReadingMeasurementStatus.Measured && !state.ReadingIncomplete
            && (state.VocabularyWords.Count == 0 || state.VocabularyMode == "quiz");
        if (!isAssessmentSession && isVerifiedCompletion)
        {
            var stats = await GetOrCreateGamificationAsync(studentId, now, cancellationToken);
            var gamificationWpm = rawWpm.HasValue
                ? (int?)Math.Round(rawWpm.Value, MidpointRounding.AwayFromZero)
                : null;
            stats.RecordVerifiedExerciseCompletion(
                state.ExerciseTypeName,
                now,
                timeSpent,
                gamificationWpm,
                measurementStatus == SpeedReadingMeasurementStatus.Measured ? comprehension : null,
                gamificationWpm.HasValue,
                xpAwarded,
                studentId,
                now);
            await OwnedGamificationAchievementEvaluator.UnlockEligibleAsync(
                db,
                stats,
                studentId,
                now,
                cancellationToken);
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) when (completionRetryCount < MaxCompletionConflictRetries)
        {
            db.ChangeTracker.Clear();
            return await CompleteAsync(
                studentId,
                sessionId,
                request,
                cancellationToken,
                completionRetryCount + 1);
        }
        catch (DbUpdateException exception) when (IsExerciseSessionCompletionConflict(exception))
        {
            db.ChangeTracker.Clear();
            var concurrentResult = await db.ExerciseSessionResults
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.SessionId == session.Id, cancellationToken);
            if (concurrentResult is null)
            {
                if (completionRetryCount >= MaxCompletionConflictRetries)
                    throw;

                return await CompleteAsync(
                    studentId,
                    sessionId,
                    request,
                    cancellationToken,
                    completionRetryCount + 1);
            }

            var persistedSession = await db.ExerciseSessions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == session.Id && item.StudentId == studentId, cancellationToken);
            return ToResult(
                concurrentResult,
                persistedSession ?? session,
                persistedSession is null ? state : DeserializeState(persistedSession.SessionDataJson));
        }

        return ToResult(
            result,
            session,
            state,
            score,
            xpAwarded,
            feedback: "Egzersiz tamamlandı.");
    }

    private void AddReadingSessionRecord(
        ExerciseSession session,
        SessionState state,
        int timeSpentSeconds,
        decimal? rawWpm,
        decimal comprehension,
        DateTime completedAt)
    {
        if (session.ReadingTextId is not { } readingTextId)
            return;

        if (IsAdaptiveFluency(state))
        {
            var baseline = state.AdaptiveStageResults.SingleOrDefault(item => item.Stage == 0);
            if (baseline is not null)
            {
                AddReadingSessionRecord(
                    session,
                    session.Id,
                    readingTextId,
                    state.AdaptiveBaselineAnswers,
                    baseline.ReadingSeconds,
                    baseline.Wpm,
                    state.AdaptiveBaselineComprehension ?? 0,
                    completedAt);
            }

            var transfer = state.AdaptiveStageResults.SingleOrDefault(item => item.Stage == 3);
            if (transfer is not null && state.AdaptiveTransferTextId is { } transferTextId)
            {
                AddReadingSessionRecord(
                    session,
                    Guid.NewGuid(),
                    transferTextId,
                    state.Answers,
                    transfer.ReadingSeconds,
                    transfer.Wpm,
                    state.AdaptiveTransferComprehension ?? comprehension,
                    completedAt);
            }

            return;
        }

        // The universal player is the primary reading flow. Keep its completed
        // result visible to the reading-history and comprehension-report APIs
        // while retaining the exercise result as the authoritative score.
        AddReadingSessionRecord(
            session,
            session.Id,
            readingTextId,
            state.Answers,
            timeSpentSeconds,
            rawWpm,
            comprehension,
            completedAt);
    }

    private void AddReadingSessionRecord(
        ExerciseSession session,
        Guid readingSessionId,
        Guid readingTextId,
        IEnumerable<SessionAnswer> sourceAnswers,
        int timeSpentSeconds,
        decimal? rawWpm,
        decimal comprehension,
        DateTime completedAt)
    {
        var source = sourceAnswers.ToList();
        if (source.Any(answer => !IsPersistableReadingAnswer(answer)))
            throw new InvalidOperationException("Okuma oturumu cevapları geçerli soru türü, Bloom seviyesi ve A-D seçeneği içermelidir.");

        var answers = source
            .GroupBy(item => item.QuestionId)
            .Select(group => group
                .OrderBy(item => item.OrderIndex)
                .ThenBy(item => item.QuestionId)
                .First())
            .OrderBy(item => item.OrderIndex)
            .ThenBy(item => item.QuestionId)
            .ToList();
        var correctAnswers = answers.Count(item => item.IsCorrect);
        var totalQuestions = answers.Count;
        var readingComprehension = totalQuestions > 0
            ? Math.Round((decimal)correctAnswers / totalQuestions * 100, 2)
            : Math.Clamp(comprehension, 0, 100);
        var calculatedWpm = rawWpm is > 0
            ? (int)Math.Round(rawWpm.Value, MidpointRounding.AwayFromZero)
            : 0;
        var efficiencyScore = calculatedWpm * (readingComprehension / 100m);
        var readingSession = ReadingSession.Import(
            readingSessionId,
            session.StudentId,
            readingTextId,
            timeSpentSeconds,
            calculatedWpm,
            correctAnswers,
            totalQuestions,
            readingComprehension,
            efficiencyScore,
            completedAt,
            completedAt,
            session.StudentId.ToString(),
            null,
            null,
            isMeasured: rawWpm is > 0);
        db.ReadingSessions.Add(readingSession);
        db.ReadingSessionAnswers.AddRange(answers.Select(item => ReadingSessionAnswer.Import(
            Guid.NewGuid(),
            readingSessionId,
            item.QuestionId,
            item.QuestionType,
            item.BloomLevel,
            item.OrderIndex,
            item.Answer,
            item.IsCorrect,
            completedAt,
            session.StudentId.ToString())));
    }

    private static bool IsPersistableReadingAnswer(SessionAnswer answer) =>
        answer.QuestionType is >= 1 and <= 3
        && answer.BloomLevel is >= 1 and <= 6
        && (answer.Answer.Trim().ToUpperInvariant() is "A" or "B" or "C" or "D"
            || answer.Answer.Trim().Equals(TimeoutAnswer, StringComparison.OrdinalIgnoreCase));

    private static bool IsReadingExerciseFlow(SessionState state) =>
        IsReadingExerciseFlow(state.ExerciseTypeName, state.EngineType, default)
        || IsAdaptiveFluency(state);

    private static bool IsReadingExerciseFlow(
        string exerciseTypeName,
        string exerciseEngineType,
        JsonElement configuration)
    {
        if (IsTachistoscope(exerciseTypeName, exerciseEngineType, configuration)) return false;
        if (ReadingExerciseTypes.Contains(exerciseTypeName)
            || ReadingExerciseTypes.Contains(exerciseEngineType))
            return true;

        var engineType = ReadString(configuration, "engineType")
            ?? ReadString(ReadObject(configuration, "engineConfig"), "engineType")
            ?? exerciseEngineType;
        return engineType is
            "text_stream"
            or "text_fade"
            or "word_highlight"
            or "reading_comprehension"
            or "exam_simulation"
            or "free_reading"
            or "regression_reduction"
            or "subvocalization_reduction"
            or "scan_find"
            or "scanning"
            or "skimming"
            or "adaptive_fluency";
    }

    private void PersistSessionAnswers(ExerciseSession session)
    {
        foreach (var answer in session.Answers)
        {
            if (db.Entry(answer).State == EntityState.Detached)
                db.ExerciseSessionAnswers.Add(answer);
        }
    }

    private async Task<UserGamification> GetOrCreateGamificationAsync(
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var stats = await db.UserGamifications
            .SingleOrDefaultAsync(item => item.UserId == userId && !item.IsDeleted, cancellationToken);
        if (stats is not null)
            return stats;

        stats = UserGamification.CreateDefault(Guid.NewGuid(), userId, now, userId.ToString());
        db.UserGamifications.Add(stats);
        return stats;
    }

    public async Task PauseAsync(Guid studentId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        var now = DateTime.UtcNow;
        var state = DeserializeState(session.SessionDataJson);
        session.Pause(now);
        if (state.Tachistoscope is { } tachistoscope)
        {
            InvalidateTachistoscopePresentations(session);
            tachistoscope.LastStimulus = tachistoscope.ExpectedStimulus.Length > 0
                ? tachistoscope.ExpectedStimulus : tachistoscope.LastStimulus;
            tachistoscope.ExpectedStimulus = string.Empty;
            tachistoscope.PresentedAt = null;
        }
        if (IsScanning(state) || state.ReadingStartTime.HasValue && !state.ReadingEndTime.HasValue)
            state.ReadingPausedAt = now;
        if (IsVisualExpansionExercise(state) && state.VisualExpansionPresentedAt.HasValue)
            state.VisualExpansionPausedAt = now;
        if (IsValidatedFixation(state) && state.FixationPresentedAt.HasValue)
            state.FixationPausedAt = now;
        session.SetState(JsonSerializer.Serialize(state, JsonOptions), session.CustomDataJson);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResumeAsync(Guid studentId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetOwnedSessionAsync(studentId, sessionId, cancellationToken);
        var now = DateTime.UtcNow;
        var state = DeserializeState(session.SessionDataJson);
        session.Resume(now);
        if (state.FixationPausedAt.HasValue)
        {
            state.FixationPausedMilliseconds += (long)Math.Max(0, (now - state.FixationPausedAt.Value).TotalMilliseconds);
            state.FixationPausedAt = null;
        }
        if (state.VisualExpansionPausedAt.HasValue)
        {
            state.VisualExpansionPausedMilliseconds += (long)Math.Max(0, (now - state.VisualExpansionPausedAt.Value).TotalMilliseconds);
            state.VisualExpansionPausedAt = null;
        }
        if (state.ReadingPausedAt.HasValue)
        {
            if (state.ReadingPausedMilliseconds.HasValue)
                state.ReadingPausedMilliseconds += (long)Math.Max(0, (now - state.ReadingPausedAt.Value).TotalMilliseconds);
            state.ReadingPausedSeconds += Math.Max(
                0,
                (int)Math.Round((now - state.ReadingPausedAt.Value).TotalSeconds));
            state.ReadingPausedAt = null;
        }
        session.SetState(JsonSerializer.Serialize(state, JsonOptions), session.CustomDataJson);
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
                GetTimingStartTime(session, state, now),
                session.EndTime ?? now,
                GetTimingPausedSeconds(session, state),
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
        var state = DeserializeState(session.SessionDataJson);
        var exercise = await db.Exercises.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == session.ExerciseId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Exercise not found.");
        var typeName = await db.ExerciseTypes.AsNoTracking()
            .Where(item => item.Id == exercise.ExerciseTypeId && !item.IsDeleted)
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
                GetTimingStartTime(session, state, now),
                session.EndTime ?? now,
                GetTimingPausedSeconds(session, state),
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
            .Where(item => exerciseIds.Contains(item.Id) && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var typeIds = exercises.Values.Select(item => item.ExerciseTypeId).Distinct().ToArray();
        var types = await db.ExerciseTypes.AsNoTracking()
            .Where(item => typeIds.Contains(item.Id) && !item.IsDeleted)
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
            && item.StudentId == studentId,
            cancellationToken)
        ?? throw new KeyNotFoundException("Exercise session not found.");

    private async Task<Guid?> GetAgeGroupIdAsync(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var trainingAge = await (from progress in db.StudentProgramProgresses.AsNoTracking()
            join template in db.ProgramTemplates.AsNoTracking() on progress.ProgramTemplateId equals template.Id
            where progress.UserId == studentId && progress.IsActive && progress.IsStaffTraining
            select (Guid?)template.TargetAgeGroupConfigurationId).SingleOrDefaultAsync(cancellationToken);
        if (trainingAge.HasValue) return trainingAge;
        return await db.UserProfiles
            .AsNoTracking()
            .Where(item => item.UserId == studentId && item.IsActive)
            .Select(item => item.AgeGroupConfigurationId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<SessionState> CreateSessionStateAsync(
        Guid exerciseId,
        string exerciseTypeName,
        string exerciseEngineType,
        int difficultyLevel,
        string configurationJson,
        Guid? readingTextId,
        AssessmentContentSnapshot? assessmentSnapshot,
        bool isAssessmentMode,
        Dictionary<string, JsonElement>? customData,
        Guid? profileAgeGroupId,
        CancellationToken cancellationToken)
    {
        var config = ParseJsonOrEmpty(configurationJson);
        var state = new SessionState
        {
            ExerciseId = exerciseId,
            ExerciseTypeName = exerciseTypeName,
            EngineType = exerciseEngineType,
            IsAssessmentMode = isAssessmentMode,
            TimingStartsOnAction = true,
            DifficultyLevel = difficultyLevel,
            CurrentNumber = IsGridExercise(exerciseTypeName, config) ? 1 : null,
            TimeLimitSeconds = ReadPositiveInt(config, "timeLimitSeconds")
        };
        var engineConfig = ReadObject(config, "engineConfig");
        var effectiveConfig = engineConfig.ValueKind == JsonValueKind.Object ? engineConfig : config;
        if (ExerciseConfigurationRules.NormalizeEngineType(exerciseEngineType) == "reading_comprehension")
        {
            state.ReadingPausedMilliseconds = 0;
            var timing = ReadObject(effectiveConfig, "timing");
            var rootTiming = ReadObject(config, "timing");
            state.ReadingMinimumMs = ReadPositiveInt(timing, "minReadingTimeMs")
                ?? ReadPositiveInt(rootTiming, "minReadingTimeMs") ?? 0;
            state.ReadingMaximumMs = ReadPositiveInt(timing, "maxReadingTimeMs")
                ?? ReadPositiveInt(rootTiming, "maxReadingTimeMs") ?? 0;
        }
        if (IsReadingExerciseFlow(state) && state.EngineType is not ("scan_find" or "scanning" or "skimming"))
            state.ReadingPurpose = ExerciseConfigurationRules.ResolveReadingPurpose(
                exerciseEngineType,
                ReadString(effectiveConfig, "readingPurpose") ?? ReadString(config, "readingPurpose"),
                isAssessmentMode);
        if (IsTachistoscope(exerciseTypeName, exerciseEngineType, config))
        {
            state.Tachistoscope = await CreateTachistoscopeAsync(effectiveConfig, difficultyLevel, profileAgeGroupId, cancellationToken);
            if (isAssessmentMode) state.Tachistoscope.AdaptiveEnabled = false;
        }
        if (IsGridExercise(exerciseTypeName, config))
        {
            state.TimeLimitSeconds = ReadGridTimeLimit(effectiveConfig) ?? ReadGridTimeLimit(config);
        }
        if (IsVocabularyExercise(exerciseTypeName, effectiveConfig))
        {
            state.VocabularyMode = (ReadString(effectiveConfig, "mode") ?? "learning").Trim().ToLowerInvariant();
            state.VocabularyQuizType = (ReadString(effectiveConfig, "quizType") ?? "mixed")
                .Trim()
                .ToLowerInvariant() switch
                {
                    "word_to_definition" => "word_to_definition",
                    "definition_to_word" => "definition_to_word",
                    _ => "mixed"
                };
            state.VocabularyWords = await LoadVocabularyWordsAsync(
                effectiveConfig,
                profileAgeGroupId,
                difficultyLevel,
                cancellationToken);
            for (var index = 0; index < state.VocabularyWords.Count; index++)
            {
                state.VocabularyWords[index].QuestionType = state.VocabularyQuizType switch
                {
                    "word_to_definition" => "word",
                    "definition_to_word" => "definition",
                    _ => index % 2 == 0 ? "word" : "definition"
                };
            }
        }
        if (IsAdaptiveFluency(exerciseTypeName, exerciseEngineType, config))
        {
            state.AdaptiveEnabled = true;
            state.AdaptiveIncreaseThreshold = ReadDecimal(effectiveConfig, "increaseThreshold") ?? 85;
            state.AdaptiveMaintainThreshold = ReadDecimal(effectiveConfig, "maintainThreshold") ?? 75;
            state.AdaptiveSupportThreshold = ReadDecimal(effectiveConfig, "supportThreshold") ?? 65;
            state.AdaptiveIncreasePercent = ReadDecimal(effectiveConfig, "increasePercent") ?? 8;
            state.AdaptiveDecreasePercent = ReadDecimal(effectiveConfig, "decreasePercent") ?? 5;
            state.AdaptiveSupportDecreasePercent = ReadDecimal(effectiveConfig, "supportDecreasePercent") ?? 10;
            state.AdaptiveMinimumComprehension = ReadDecimal(effectiveConfig, "minimumComprehension") ?? 75;
            _ = AdaptiveFluencyRules.ResolveTargetChangePercent(
                100,
                state.AdaptiveIncreaseThreshold,
                state.AdaptiveMaintainThreshold,
                state.AdaptiveSupportThreshold,
                state.AdaptiveIncreasePercent,
                state.AdaptiveDecreasePercent,
                state.AdaptiveSupportDecreasePercent);
            if (state.AdaptiveMinimumComprehension is < 0 or > 100)
                throw new InvalidOperationException("Adaptive fluency minimumComprehension must be between 0 and 100.");
            state.AdaptivePurposes = ReadStringArray(effectiveConfig, "repeatPurposes") is { Length: > 0 } purposes
                ? purposes
                : ["Ana fikri belirleyin.", "Neden-sonuç ilişkilerine ve önemli ayrıntılara odaklanın."];
        }
        if (IsVisualExpansionExercise(exerciseTypeName)
            || IsEngineType(exerciseEngineType, "visual_expansion"))
        {
            var visualConfig = engineConfig.ValueKind == JsonValueKind.Object ? engineConfig : config;
            state.VisualExpansionProtocolVersion = 1;
            var expansion = ReadObject(visualConfig, "expansion");
            var content = ReadObject(visualConfig, "content");
            var timing = ReadObject(visualConfig, "timing");
            state.VisualExpansionStimulusType = ReadString(expansion, "stimulusType")
                ?? ReadString(content, "stimulusType")
                ?? "letter";
            state.VisualExpansionPattern = ReadString(expansion, "pattern")
                ?? ReadString(visualConfig, "pattern")
                ?? ReadString(visualConfig, "mode")
                ?? "horizontal";
            state.VisualExpansionDisplayDurationMs = Math.Clamp(
                ReadPositiveInt(timing, "durationMs")
                    ?? ReadPositiveInt(visualConfig, "displayDurationMs")
                    ?? ReadPositiveInt(config, "displayDurationMs")
                    ?? 250,
                100,
                5_000);
            state.VisualExpansionStartDegrees = Math.Clamp(
                ReadPositiveInt(expansion, "startDegrees")
                    ?? ReadPositiveInt(visualConfig, "startDegrees")
                    ?? ReadPositiveInt(config, "startDegrees")
                    ?? Math.Max(2, 2 + difficultyLevel * 2),
                2,
                60);
            state.VisualExpansionTargetDegrees = Math.Clamp(
                ReadPositiveInt(expansion, "targetDegrees")
                    ?? ReadPositiveInt(visualConfig, "targetDegrees")
                    ?? ReadPositiveInt(config, "targetDegrees")
                    ?? Math.Max(state.VisualExpansionStartDegrees, 30),
                state.VisualExpansionStartDegrees,
                60);
            state.VisualExpansionCurrentDegrees = CalculateVisualExpansionDegrees(state, 0);
        }
        if (IsFocusExercise(exerciseTypeName) || IsFocusEngineType(exerciseEngineType))
        {
            var focusConfig = engineConfig.ValueKind == JsonValueKind.Object ? engineConfig : config;
            state.FocusMode = ReadString(focusConfig, "mode") ?? ReadString(config, "mode") ?? "position";
            state.FocusNLevel = Math.Clamp(
                ReadPositiveInt(focusConfig, "nLevel") ?? ReadPositiveInt(config, "nLevel") ?? 1,
                1,
                5);
            state.FocusSpeedMs = Math.Clamp(
                ReadPositiveInt(focusConfig, "speedMs") ?? ReadPositiveInt(config, "speedMs") ?? 1500,
                100,
                10_000);
            state.GridSize = Math.Clamp(
                ReadPositiveInt(focusConfig, "gridSize") ?? ReadPositiveInt(config, "gridSize") ?? 3,
                3,
                7);
            state.PositionSequence = ReadProperty(focusConfig, "positionSequence").ValueKind == JsonValueKind.Array
                ? ReadIntArray(focusConfig, "positionSequence")
                : ReadIntArray(config, "positionSequence");
            state.WordSequence = ReadProperty(focusConfig, "wordSequence").ValueKind == JsonValueKind.Array
                ? ReadStringArray(focusConfig, "wordSequence")
                : ReadStringArray(config, "wordSequence");
            state.PositionTargetIndices = ReadProperty(focusConfig, "positionTargetIndices").ValueKind == JsonValueKind.Array
                ? ReadIntArray(focusConfig, "positionTargetIndices")
                : ReadIntArray(config, "positionTargetIndices");
            state.WordTargetIndices = ReadProperty(focusConfig, "wordTargetIndices").ValueKind == JsonValueKind.Array
                ? ReadIntArray(focusConfig, "wordTargetIndices")
                : ReadIntArray(config, "wordTargetIndices");
        }

        if (assessmentSnapshot?.ReadingText is { } snapshotText)
        {
            state.ReadingTextId = snapshotText.Id;
            state.ReadingTextTitle = snapshotText.Title;
            state.Content = snapshotText.Content;
            state.WordCount = state.ReadingPausedMilliseconds.HasValue ? CountWords(snapshotText.Content)
                : snapshotText.WordCount > 0 ? snapshotText.WordCount : CountWords(snapshotText.Content);
            state.Words = SplitWords(snapshotText.Content);
            state.Questions = assessmentSnapshot.Questions
                .Where(item => ReadingQuestionQualityRules.HasScorableAnswerKey(item.CorrectAnswer))
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
                    DifficultyLevel = item.DifficultyLevel,
                    OrderIndex = item.OrderIndex,
                    QuestionType = item.QuestionType
                })
                .ToList();
        }
        else if (readingTextId.HasValue)
        {
            var readingText = await db.ReadingTexts.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == readingTextId.Value
                    && item.IsActive
                    && !item.IsDeleted,
                    cancellationToken)
                ?? throw new KeyNotFoundException("Reading text not found.");
            state.ReadingTextId = readingText.Id;
            state.ReadingTextTitle = readingText.Title;
            state.Content = readingText.Content;
            state.WordCount = state.ReadingPausedMilliseconds.HasValue ? CountWords(readingText.Content)
                : readingText.WordCount > 0 ? readingText.WordCount : CountWords(readingText.Content);
            state.Words = SplitWords(readingText.Content);
            state.Questions = await db.ReadingQuestions.AsNoTracking()
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
                    DifficultyLevel = item.DifficultyLevel,
                    OrderIndex = item.OrderIndex,
                    QuestionType = item.Type
                })
                .ToListAsync(cancellationToken);
            state.Questions = state.Questions
                .Where(item => ReadingQuestionQualityRules.HasScorableAnswerKey(item.CorrectAnswer))
                .ToList();
        }

        if (state.ReadingPurpose == "practice" || !ExerciseConfigurationRules.ShouldIncludeComprehensionQuestions(
                exerciseTypeName, exerciseEngineType,
                ReadString(effectiveConfig, "mode") ?? ReadString(config, "mode")))
            state.Questions.Clear();

        if (state.ReadingPurpose is not null && string.IsNullOrWhiteSpace(state.Content))
            throw new BusinessRuleException("ExerciseSession.ReadingContentUnavailable",
                "Bu egzersiz için uygun okuma metni bulunamadı. Lütfen farklı bir egzersiz seçin.");
        if (state.ReadingPurpose == "evaluation" && state.Questions.Count == 0)
            throw new BusinessRuleException("ExerciseSession.ReadingQuestionsUnavailable",
                "Bu değerlendirme için anlama soruları henüz hazırlanmadı. Lütfen farklı bir değerlendirme seçin.");

        if (state.AdaptiveEnabled)
        {
            state.AdaptivePrimaryQuestions = state.Questions.Select(CloneQuestion).ToList();
            var transferTextId = ReadGuid(effectiveConfig, "transferReadingTextId")
                ?? throw new InvalidOperationException("Adaptive fluency requires a transferReadingTextId.");
            if (transferTextId == state.ReadingTextId)
                throw new InvalidOperationException("Adaptive fluency transfer text must differ from the primary text.");

            var transferText = await db.ReadingTexts.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == transferTextId
                    && item.IsActive
                    && !item.IsDeleted
                    && item.DifficultyLevel == state.DifficultyLevel
                    && (!profileAgeGroupId.HasValue
                        || item.TargetAgeGroupId == null
                        || item.TargetAgeGroupId == profileAgeGroupId.Value)
                    && (item.ExerciseId == null || item.ExerciseId == exerciseId),
                    cancellationToken);
            if (transferText is null)
                throw new KeyNotFoundException("Adaptive fluency transfer text was not found.");
            state.AdaptiveTransferTextId = transferText.Id;
            state.AdaptiveTransferTitle = transferText.Title;
            state.AdaptiveTransferContent = transferText.Content;
            state.AdaptiveTransferWordCount = transferText.WordCount > 0 ? transferText.WordCount : CountWords(transferText.Content);
            state.AdaptiveTransferQuestions = await db.ReadingQuestions.AsNoTracking()
                .Where(item => item.ReadingTextId == transferText.Id && !item.IsDeleted)
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
                    DifficultyLevel = item.DifficultyLevel,
                    OrderIndex = item.OrderIndex,
                    QuestionType = item.Type
                }).ToListAsync(cancellationToken);
            state.AdaptiveTransferQuestions = state.AdaptiveTransferQuestions
                .Where(item => ReadingQuestionQualityRules.HasScorableAnswerKey(item.CorrectAnswer))
                .ToList();
            if (state.AdaptivePrimaryQuestions.Count == 0 || state.AdaptiveTransferQuestions.Count == 0)
                throw new InvalidOperationException("Adaptive fluency primary and transfer texts both require questions.");
            state.TotalSteps = 4;
        }

        if ((IsVisualizationExercise(exerciseTypeName)
                || IsEngineType(exerciseEngineType, "visualization"))
            && state.Questions.Count == 0)
        {
            state.VisualizationScenes = await LoadVisualizationScenesAsync(
                exerciseId,
                config,
                profileAgeGroupId,
                cancellationToken);
            state.Questions = state.VisualizationScenes
                .SelectMany(scene => scene.Questions)
                .Select(ToSessionQuestion)
                .ToList();
        }

        var gridSize = !state.AdaptiveEnabled && IsGridExercise(exerciseTypeName, config)
            ? Math.Clamp(
                ReadPositiveInt(effectiveConfig, "gridSize")
                    ?? ReadPositiveInt(ReadObject(effectiveConfig, "grid"), "rows")
                    ?? ReadPositiveInt(config, "gridSize")
                    ?? difficultyLevel + 2,
                3,
                7)
            : (int?)null;
        if (state.AdaptiveEnabled)
        {
            state.TotalSteps = 4;
        }
        else if (gridSize.HasValue)
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
            state.TotalSteps = ReadPositiveInt(effectiveConfig, "totalSteps")
                ?? ReadPositiveInt(effectiveConfig, "itemCount")
                ?? ReadPositiveInt(effectiveConfig, "rounds")
                ?? ReadPositiveInt(config, "totalSteps")
                ?? ReadPositiveInt(config, "itemCount")
                ?? ReadPositiveInt(config, "rounds")
                ?? (state.VocabularyWords.Count > 0 ? state.VocabularyWords.Count : (int?)null)
                ?? (IsVisualizationExercise(exerciseTypeName)
                    || IsEngineType(exerciseEngineType, "visualization")
                    ? state.Questions.Count
                    : state.Questions.Count > 0 ? 1 + state.Questions.Count : state.Words.Length);
            if (state.TotalSteps <= 0) state.TotalSteps = 1;
        }

        if (state.VocabularyWords.Count > 0)
            state.TotalSteps = state.VocabularyWords.Count;

        if (IsFocusExercise(exerciseTypeName) || IsFocusEngineType(exerciseEngineType))
        {
            var needsWords = state.FocusMode.Equals("word", StringComparison.OrdinalIgnoreCase)
                || state.FocusMode.Equals("dual", StringComparison.OrdinalIgnoreCase);
            var needsPositions = !state.FocusMode.Equals("word", StringComparison.OrdinalIgnoreCase);
            if (!IsEngineType(exerciseEngineType, "motion_path")
                && (needsWords && state.WordSequence.Length == 0
                    || needsPositions && state.PositionSequence.Length == 0))
            {
                var sequenceLength = Math.Max(
                    Math.Max(state.TotalSteps > 1 ? state.TotalSteps : 20,
                        Math.Max(state.PositionSequence.Length, state.WordSequence.Length)),
                    state.FocusNLevel + 1);
                state.TotalSteps = Math.Clamp(sequenceLength, 1, 500);
                if (needsWords && state.WordSequence.Length == 0)
                    state.WordSequence = CreateFocusWordSequence(
                        state.TotalSteps, state.FocusNLevel, state.WordTargetIndices);
                if (needsPositions && state.PositionSequence.Length == 0)
                    state.PositionSequence = CreateFocusPositionSequence(
                        state.TotalSteps, state.FocusNLevel, state.GridSize, state.PositionTargetIndices);
            }

            state.TotalSteps = Math.Max(
                state.TotalSteps,
                Math.Max(state.PositionSequence.Length, state.WordSequence.Length));
            state.TotalSteps = Math.Clamp(state.TotalSteps, 1, 500);
        }

        if (IsVisualExpansionExercise(exerciseTypeName)
            || IsEngineType(exerciseEngineType, "visual_expansion"))
            state.TotalSteps = Math.Clamp(state.TotalSteps, 1, 100);

        if (IsEngineType(exerciseEngineType, "motion_path")
            && string.Equals(ReadString(effectiveConfig, "mode") ?? ReadString(config, "mode") ?? "fixation",
                "fixation", StringComparison.OrdinalIgnoreCase))
        {
            var content = ReadObject(effectiveConfig, "content");
            var fixation = ReadObject(effectiveConfig, "fixation");
            var timing = ReadObject(effectiveConfig, "timing");
            state.FixationPeripheralCount = Math.Clamp(
                ReadPositiveInt(content, "peripheralCount")
                    ?? ReadPositiveInt(fixation, "peripheralCount") ?? 0, 0, 4);
            state.FixationProtocolVersion = 1;
            state.FixationHoldMs = Math.Clamp(
                ReadPositiveInt(timing, "holdMs")
                    ?? ReadPositiveInt(ReadObject(effectiveConfig, "movement"), "fixationTimeMs") ?? 2_000,
                50, 10_000);
            state.TotalSteps = Math.Clamp(
                ReadPositiveInt(content, "points")
                    ?? ReadPositiveInt(fixation, "points")
                    ?? state.TotalSteps, 1, 500);
        }

        if (exerciseTypeName.Equals("RSVP", StringComparison.OrdinalIgnoreCase) && state.Words.Length > 0)
            state.TotalSteps = state.Words.Length;
        if (state.Tachistoscope is { } tachistoscope) state.TotalSteps = tachistoscope.Count;
        if (IsScanning(state)) InitializeScanning(state, effectiveConfig);
        if (IsGrouping(state))
        {
            var pacer = ReadObject(effectiveConfig, "pacer");
            var content = ReadObject(effectiveConfig, "content");
            var timing = ReadObject(effectiveConfig, "timing");
            state.GroupingChunkSize = Math.Clamp(ReadPositiveInt(effectiveConfig, "chunkSize")
                ?? ReadPositiveInt(config, "chunkSize") ?? ReadPositiveInt(pacer, "chunkSize")
                ?? ReadPositiveInt(ReadObject(config, "pacer"), "chunkSize") ?? ReadPositiveInt(content, "chunkSize")
                ?? ReadPositiveInt(ReadObject(config, "content"), "chunkSize") ?? 1, 1, 10);
            var explicitWpm = ReadPositiveInt(effectiveConfig, "targetWpm")
                ?? ReadPositiveInt(config, "targetWpm") ?? ReadPositiveInt(pacer, "speedWpm")
                ?? ReadPositiveInt(ReadObject(config, "pacer"), "speedWpm");
            var rootTiming = ReadObject(config, "timing");
            var duration = ReadPositiveInt(timing, "durationMs") ?? ReadPositiveInt(rootTiming, "durationMs");
            var delay = ReadNonNegativeInt(timing, "delayMs") ?? ReadNonNegativeInt(rootTiming, "delayMs") ?? 0;
            delay = Math.Clamp(delay, 0, 10000);
            state.GroupingDisplayPaceWpm = explicitWpm.HasValue ? Math.Clamp(explicitWpm.Value, 20, 1500)
                : duration.HasValue ? Math.Clamp(60000m * state.GroupingChunkSize /
                    (Math.Clamp(duration.Value, 1, 10000) + delay), 20, 1500) : 200;
            state.ReadingMinimumMs = (int)Math.Ceiling(state.Words.Length * 60000m / state.GroupingDisplayPaceWpm);
            state.ReadingMaximumMs = (ReadPositiveInt(timing, "timeLimitSec")
                ?? ReadPositiveInt(rootTiming, "timeLimitSec") ?? 0) * 1000;
            state.ReadingPausedMilliseconds = 0;
            state.TotalSteps = (int)Math.Ceiling((decimal)state.Words.Length / state.GroupingChunkSize);
        }
        state.CustomData = customData;
        if (IsTextFade(state))
        {
            if (state.Words.Length == 0)
                throw new InvalidOperationException("Metin Solma için geçerli bir metin gereklidir.");
            var fading = ReadObject(effectiveConfig, "fading");
            var rootFading = ReadObject(config, "fading");
            var timing = ReadObject(effectiveConfig, "timing");
            var rootTiming = ReadObject(config, "timing");
            state.FadeDisplayPaceWpm = Math.Clamp(ReadPositiveInt(effectiveConfig, "targetWpm")
                ?? ReadPositiveInt(config, "targetWpm") ?? ReadPositiveInt(fading, "speedWpm")
                ?? ReadPositiveInt(rootFading, "speedWpm") ?? 200, 20, 1500);
            state.FadeLagMs = Math.Clamp(ReadNonNegativeInt(effectiveConfig, "lagMs")
                ?? ReadNonNegativeInt(config, "lagMs") ?? ReadNonNegativeInt(fading, "lagMs")
                ?? ReadNonNegativeInt(rootFading, "lagMs") ?? 3000, 0, 10000);
            state.WordCount = state.Words.Length;
            state.ReadingMinimumMs = state.FadeLagMs + (int)Math.Ceiling(state.WordCount * 60000m / state.FadeDisplayPaceWpm);
            state.ReadingMaximumMs = (ReadNonNegativeInt(timing, "timeLimitSec")
                ?? ReadNonNegativeInt(rootTiming, "timeLimitSec") ?? 0) * 1000;
            state.ReadingPausedMilliseconds = 0;
            state.TotalSteps = state.WordCount;
        }
        return state;
    }

    private static ExerciseActionValidationResponse StartReading(
        ExerciseSession session,
        SessionState state,
        DateTime now)
    {
        if (IsAdaptiveFluency(state))
        {
            if (state.AdaptiveAwaitingQuestions)
                return Invalid("Complete the current comprehension check before starting the next reading.");
            if (state.AdaptiveStageStartedAt.HasValue)
                return Invalid("This adaptive fluency stage has already started.");
            EnsureTimingStarted(session, state, now);
            state.AdaptiveStageStartedAt = now;
            state.AdaptivePausedSecondsAtStart = session.TotalPausedSeconds;
            return Valid("Okuma aşaması başlatıldı.", state.AdaptiveStage + 1);
        }
        EnsureTimingStarted(session, state, now);
        state.ReadingStartTime ??= now;
        session.SetCurrentStep(Math.Max(session.CurrentStep, 1));
        return Valid("Okuma başlatıldı. Zamanınız işliyor...", nextStep: session.CurrentStep);
    }

    private static ExerciseActionValidationResponse FinishReading(
        ExerciseSession session,
        SessionState state,
        DateTime now,
        bool incomplete)
    {
        if (IsAdaptiveFluency(state))
            return FinishAdaptiveStage(session, state, now);
        if (ExerciseConfigurationRules.NormalizeEngineType(state.EngineType) == "reading_comprehension" || IsGrouping(state) || IsTextFade(state))
        {
            if ((IsGrouping(state) || IsTextFade(state)) && (DisplayPace(state) <= 0 || state.ReadingMinimumMs <= 0))
                return Invalid("Bu eski egzersiz oturumu doğrulanamıyor. Egzersizi yeniden başlatın.");
            if (!state.ReadingStartTime.HasValue)
                return Invalid("Önce okumayı başlatın.");
            if (!state.ReadingEndTime.HasValue)
            {
                var elapsedMs = Math.Max(0, (now - state.ReadingStartTime.Value).TotalMilliseconds
                    - (state.ReadingPausedMilliseconds ?? state.ReadingPausedSeconds * 1000L));
                if (IsGrouping(state) || IsTextFade(state))
                    incomplete |= state.ReadingMaximumMs > 0 && elapsedMs >= state.ReadingMaximumMs;
                if (!incomplete && elapsedMs < state.ReadingMinimumMs)
                    return Invalid("Minimum okuma süresi henüz dolmadı.");
                incomplete |= state.ReadingMaximumMs > 0 && elapsedMs >= state.ReadingMaximumMs;
            }
        }
        EnsureTimingStarted(session, state, now);
        state.ReadingStartTime ??= state.TimingStartedAt ?? now;
        if (!state.ReadingEndTime.HasValue)
        {
            state.ReadingEndTime = now;
            state.ReadingIncomplete = incomplete;
            if (IsGrouping(state))
            {
                var elapsedMs = Math.Max(0, (now - state.ReadingStartTime.Value).TotalMilliseconds
                    - (state.ReadingPausedMilliseconds ?? state.ReadingPausedSeconds * 1000L));
                state.GroupingCompletionPercent = incomplete
                    ? Math.Min(99, Math.Round((decimal)elapsedMs / Math.Max(1, state.ReadingMinimumMs) * 100, 2)) : 100;
            }
            if (IsTextFade(state))
            {
                var elapsedMs = Math.Max(0, (now - state.ReadingStartTime.Value).TotalMilliseconds
                    - (state.ReadingPausedMilliseconds ?? state.ReadingPausedSeconds * 1000L));
                if (state.ReadingMaximumMs > 0) elapsedMs = Math.Min(elapsedMs, state.ReadingMaximumMs);
                var fadedWords = Math.Floor(Math.Max(0, (decimal)elapsedMs - state.FadeLagMs) * state.FadeDisplayPaceWpm / 60000m);
                state.FadeCompletionPercent = incomplete
                    ? Math.Min(99, Math.Round(fadedWords / Math.Max(1, state.WordCount) * 100, 2)) : 100;
            }
        }
        session.SetCurrentStep(Math.Max(session.CurrentStep, 1));
        var seconds = SpeedReadingExerciseSessionRules.CalculateReadingSeconds(
            session.StartTime,
            now,
            state.ReadingStartTime,
            state.ReadingEndTime,
            state.ReadingPausedSeconds);
        var wpm = CalculateReadingWpm(state, seconds);
        var message = wpm.HasValue
            ? "Okuma tamamlandı! Hızınız: " + wpm.Value + " WPM."
            : "Okuma tamamlandı; güvenilir WPM için yeterli ölçüm alınamadı.";
        return Valid(message, session.CurrentStep, currentWpm: wpm);
    }

    private static ExerciseActionValidationResponse FinishAdaptiveStage(
        ExerciseSession session,
        SessionState state,
        DateTime now)
    {
        if (!state.AdaptiveStageStartedAt.HasValue)
            return Invalid("Adaptive fluency reading has not started.");
        var seconds = SpeedReadingExerciseSessionRules.CalculateActiveSeconds(
            state.AdaptiveStageStartedAt.Value,
            now,
            Math.Max(0, session.TotalPausedSeconds - state.AdaptivePausedSecondsAtStart),
            null,
            false);
        var wordCount = state.AdaptiveStage == 3 ? state.AdaptiveTransferWordCount : state.WordCount;
        var wpm = SpeedReadingExerciseSessionRules.CalculateValidatedRawWpm(wordCount, seconds);
        if (!wpm.HasValue)
            return Invalid("Reliable WPM could not be measured for this stage.");
        state.AdaptiveStageResults.Add(new AdaptiveStageResult
        {
            Stage = state.AdaptiveStage,
            Wpm = wpm.Value,
            ReadingSeconds = seconds
        });
        state.AdaptiveStageStartedAt = null;
        state.AdaptiveAwaitingQuestions = state.AdaptiveStage is 0 or 3;
        if (!state.AdaptiveAwaitingQuestions)
            session.SetCurrentStep(Math.Min(session.TotalSteps, state.AdaptiveStage + 1));
        return Valid(
            state.AdaptiveAwaitingQuestions ? "Okuma tamamlandı; anlama sorularına geçin." : "Amaçlı tekrar tamamlandı.",
            state.AdaptiveStage + 1,
            currentWpm: wpm,
            feedbackData: AdaptiveFeedback(state));
    }

    private static ExerciseActionValidationResponse AdvanceAdaptiveStage(ExerciseSession session, SessionState state)
    {
        if (!IsAdaptiveFluency(state) || state.AdaptiveStageStartedAt.HasValue)
            return Invalid("Adaptive fluency stage cannot be advanced now.");
        if (state.AdaptiveStageResults.All(item => item.Stage != state.AdaptiveStage))
            return Invalid("Complete the current reading before advancing.");
        if (state.AdaptiveAwaitingQuestions && state.Answers.Count != state.Questions.Count)
            return Invalid("Answer all comprehension questions before advancing.");

        if (state.AdaptiveStage == 0)
        {
            state.AdaptiveBaselineComprehension = Math.Round(
                (decimal)state.Answers.Count(item => item.IsCorrect) / state.Questions.Count * 100, 2);
            var baselineWpm = state.AdaptiveStageResults.Single(item => item.Stage == 0).Wpm;
            var change = AdaptiveFluencyRules.ResolveTargetChangePercent(
                state.AdaptiveBaselineComprehension.Value,
                state.AdaptiveIncreaseThreshold,
                state.AdaptiveMaintainThreshold,
                state.AdaptiveSupportThreshold,
                state.AdaptiveIncreasePercent,
                state.AdaptiveDecreasePercent,
                state.AdaptiveSupportDecreasePercent);
            state.AdaptiveTargetWpm = Math.Round(baselineWpm * (1 + change / 100), 0);
            state.AdaptiveBaselineAnswers = state.Answers.Select(item => new SessionAnswer
            {
                QuestionId = item.QuestionId,
                Answer = item.Answer,
                IsCorrect = item.IsCorrect,
                TimeSpentSeconds = item.TimeSpentSeconds,
                BloomLevel = item.BloomLevel,
                OrderIndex = item.OrderIndex,
                QuestionType = item.QuestionType
            }).ToList();
            state.Answers.Clear();
            state.Questions.Clear();
        }
        else if (state.AdaptiveStage == 2)
        {
            state.Questions = state.AdaptiveTransferQuestions.Select(CloneQuestion).ToList();
            state.Answers.Clear();
            state.Content = state.AdaptiveTransferContent;
            state.ReadingTextTitle = state.AdaptiveTransferTitle;
            state.WordCount = state.AdaptiveTransferWordCount;
            state.Words = SplitWords(state.Content);
        }
        else if (state.AdaptiveStage == 3)
        {
            state.AdaptiveTransferComprehension = Math.Round(
                (decimal)state.Answers.Count(item => item.IsCorrect) / state.Questions.Count * 100, 2);
            state.AdaptiveTransferGainPercent = AdaptiveFluencyRules.CalculateTransferGainPercent(
                state.AdaptiveStageResults.Single(item => item.Stage == 0).Wpm,
                state.AdaptiveBaselineComprehension ?? 0,
                state.AdaptiveStageResults.Single(item => item.Stage == 3).Wpm,
                state.AdaptiveTransferComprehension.Value,
                state.AdaptiveMinimumComprehension);
            state.AdaptiveCompleted = true;
            state.AdaptiveAwaitingQuestions = false;
            session.SetCurrentStep(session.TotalSteps);
            return Valid("Aktarım ölçümü tamamlandı.", session.CurrentStep, isCompleted: true, feedbackData: AdaptiveFeedback(state));
        }

        state.AdaptiveStage++;
        state.AdaptiveAwaitingQuestions = false;
        session.SetCurrentStep(Math.Min(session.TotalSteps, state.AdaptiveStage));
        return Valid("Sonraki aşama hazır.", session.CurrentStep, feedbackData: AdaptiveFeedback(state));
    }

    private static ExerciseActionValidationResponse StartFocus(
        ExerciseSession session,
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

    private static ExerciseActionValidationResponse PresentVisualExpansion(
        ExerciseSession session,
        SessionState state,
        DateTime now)
    {
        if (!IsVisualExpansionExercise(state))
            return Invalid("Visual expansion presentation is not valid for this exercise.");
        if (state.VisualExpansionExpectedStimuli.Length > 0)
        {
            // A refresh or a retried request can ask for the same round again.
            // Replaying the server-owned stimulus is idempotent and lets the
            // learner continue without creating a second round.
            var existingFeedback = JsonSerializer.SerializeToElement(new
            {
                round = state.VisualExpansionRound,
                stimuli = state.VisualExpansionExpectedStimuli,
                displayDurationMs = state.VisualExpansionDisplayDurationMs,
                degrees = state.VisualExpansionCurrentDegrees
            }, JsonOptions);
            return Valid("Görsel genişleme uyaranı hazır.", state.VisualExpansionRound, feedbackData: existingFeedback);
        }
        if (state.VisualExpansionRound >= state.TotalSteps)
            return Invalid("All visual expansion rounds are complete.");

        state.VisualExpansionExpectedStimuli = VisualExpansionRoundRules.CreateStimuli(
            session.Id.GetHashCode(),
            state.VisualExpansionRound,
            state.VisualExpansionStimulusType,
            state.VisualExpansionPattern.Equals("radial", StringComparison.OrdinalIgnoreCase) ? 4 : 2).ToArray();
        state.VisualExpansionPresentedAt = now.ToUniversalTime();
        state.VisualExpansionPausedMilliseconds = 0;
        state.VisualExpansionPausedAt = null;
        state.VisualExpansionPausedSecondsAtPresentation = session.TotalPausedSeconds;
        var feedback = JsonSerializer.SerializeToElement(new
        {
            round = state.VisualExpansionRound,
            stimuli = state.VisualExpansionExpectedStimuli,
            displayDurationMs = state.VisualExpansionDisplayDurationMs,
            degrees = state.VisualExpansionCurrentDegrees
        }, JsonOptions);
        return Valid("Görsel genişleme uyaranı hazır.", state.VisualExpansionRound, feedbackData: feedback);
    }

    private static ExerciseActionValidationResponse AnswerVisualExpansion(
        ExerciseSession session,
        SessionState state,
        ExerciseActionRequest request,
        DateTime now)
    {
        if (!IsVisualExpansionExercise(state)
            || state.VisualExpansionExpectedStimuli.Length == 0
            || !state.VisualExpansionPresentedAt.HasValue)
            return Invalid("No visual expansion round is awaiting an answer.");

        var elapsedMs = (int)Math.Round(Math.Max(0,
            (now.ToUniversalTime() - state.VisualExpansionPresentedAt.Value).TotalMilliseconds
            - state.VisualExpansionPausedMilliseconds));
        var result = VisualExpansionRoundRules.Evaluate(
            state.VisualExpansionExpectedStimuli,
            request.Answers ?? [],
            elapsedMs,
            state.VisualExpansionDisplayDurationMs,
            // The answer is sent after the stimulus is hidden. Keep a
            // generous response grace period so a learner who needs a few
            // seconds to type, or a gateway retry, cannot turn a valid round
            // into a fatal assessment error.
            state.VisualExpansionDisplayDurationMs + 60_000);
        if (!result.IsAccepted)
        {
            state.VisualExpansionExpectedStimuli = [];
            state.VisualExpansionPresentedAt = null;
            return Invalid("Visual expansion answer arrived outside its response window.");
        }

        session.Advance(result.IsCorrect);
        var responseTimeMs = Math.Max(0, elapsedMs - state.VisualExpansionDisplayDurationMs);
        state.VisualExpansionRoundResults.Add(new VisualExpansionRoundState(
            state.VisualExpansionRound + 1, state.VisualExpansionCurrentDegrees,
            state.VisualExpansionDisplayDurationMs, responseTimeMs, result.IsCorrect));
        state.VisualExpansionMaxPresentedDistance = Math.Max(state.VisualExpansionMaxPresentedDistance, state.VisualExpansionCurrentDegrees);
        state.VisualExpansionAverageResponseTimeMs = (int)Math.Round(state.VisualExpansionRoundResults.Average(item => item.ResponseTimeMs));
        state.VisualExpansionRound++;
        var nextDifficulty = VisualExpansionRoundRules.AdvanceDifficulty(
            state.VisualExpansionCurrentDegrees,
            state.VisualExpansionTargetDegrees,
            state.VisualExpansionDisplayDurationMs,
            result.IsCorrect);
        state.VisualExpansionCurrentDegrees = nextDifficulty.Degrees;
        state.VisualExpansionDisplayDurationMs = nextDifficulty.DisplayDurationMs;
        state.VisualExpansionExpectedStimuli = [];
        state.VisualExpansionPresentedAt = null;
        return Valid(
            session.AssessmentAttemptId.HasValue ? "Yanıt kaydedildi." : result.IsCorrect ? "Doğru." : "Yanlış.",
            state.VisualExpansionRound,
            isCompleted: state.VisualExpansionRound >= state.TotalSteps,
            isCorrect: session.AssessmentAttemptId.HasValue ? null : result.IsCorrect,
            feedbackData: JsonSerializer.SerializeToElement(new { responseTimeMs }, JsonOptions));
    }

    private static ExerciseActionValidationResponse PresentFixation(
        ExerciseSession session, SessionState state, DateTime now)
    {
        if (!IsValidatedFixation(state))
            return Invalid("Fixation presentation is not valid for this exercise.");
        if (state.FixationRound >= state.TotalSteps)
            return Invalid("All fixation rounds are complete.");
        if (state.FixationExpectedStimuli.Length == 0)
        {
            state.FixationExpectedStimuli = VisualExpansionRoundRules.CreateStimuli(
                session.Id.GetHashCode(), state.FixationRound, "letter", state.FixationPeripheralCount).ToArray();
            state.FixationPresentedAt = now.ToUniversalTime();
            state.FixationPausedMilliseconds = 0;
            state.FixationPausedAt = null;
        }

        var feedback = JsonSerializer.SerializeToElement(new
        {
            round = state.FixationRound,
            stimuli = state.FixationExpectedStimuli,
            holdMs = state.FixationHoldMs
        }, JsonOptions);
        return Valid("Sabitleme uyaranı hazır.", state.FixationRound, feedbackData: feedback);
    }

    private static ExerciseActionValidationResponse AnswerFixation(
        ExerciseSession session, SessionState state, ExerciseActionRequest request, DateTime now)
    {
        if (!IsValidatedFixation(state) || state.FixationExpectedStimuli.Length == 0
            || !state.FixationPresentedAt.HasValue)
            return Invalid("No fixation round is awaiting an answer.");

        // The client first moves the target (150 ms), then shows its cue (200 ms).
        var exposureMs = state.FixationHoldMs + 350;
        var elapsedMs = Math.Max(0, (now.ToUniversalTime() - state.FixationPresentedAt.Value).TotalMilliseconds
            - state.FixationPausedMilliseconds);
        if (elapsedMs < exposureMs)
            return Invalid("Sabitleme gösterimi henüz tamamlanmadı.");
        var responseTimeMs = (int)Math.Min(int.MaxValue, Math.Round(elapsedMs - exposureMs));
        var expected = state.FixationExpectedStimuli.Select(item => item.Trim().ToUpperInvariant()).Order().ToArray();
        var submitted = (request.Answers ?? []).Select(item => item.Trim().ToUpperInvariant()).Order().ToArray();
        var isCorrect = expected.SequenceEqual(submitted);
        session.Advance(isCorrect);
        state.FixationRoundResults.Add(new FixationRoundState(state.FixationRound + 1, state.FixationHoldMs, responseTimeMs, isCorrect));
        state.FixationRound++;
        state.FixationExpectedStimuli = [];
        state.FixationPresentedAt = null;
        state.FocusCompleted = state.FixationRound >= state.TotalSteps;
        return Valid("Sabitleme yanıtı kaydedildi.", state.FixationRound,
            isCompleted: state.FocusCompleted,
            isCorrect: session.AssessmentAttemptId.HasValue ? null : isCorrect,
            feedbackData: JsonSerializer.SerializeToElement(new { responseTimeMs }, JsonOptions));
    }

    private static int CalculateVisualExpansionDegrees(SessionState state, int round)
    {
        var start = Math.Clamp(state.VisualExpansionStartDegrees, 2, 60);
        var target = Math.Clamp(state.VisualExpansionTargetDegrees, start, 60);
        if (state.TotalSteps <= 1)
            return start;

        var progress = Math.Clamp((double)round / (state.TotalSteps - 1), 0d, 1d);
        return (int)Math.Round(start + ((target - start) * progress), MidpointRounding.AwayFromZero);
    }

    private static ExerciseActionValidationResponse AdvanceFocus(
        ExerciseSession session,
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
        ExerciseSession session,
        SessionState state,
        ExerciseActionRequest request)
    {
        if (!request.QuestionId.HasValue
            || (!request.IsTimeout && string.IsNullOrWhiteSpace(request.Answer)))
            return Invalid("Soru ID ve cevap gereklidir.");
        var question = state.Questions.SingleOrDefault(item => item.QuestionId == request.QuestionId.Value);
        if (question is null)
            return Invalid("Soru bu oturuma ait değil.");
        var existingAnswer = state.Answers.SingleOrDefault(item => item.QuestionId == question.QuestionId);
        if (existingAnswer is not null)
        {
            // A refresh, timeout race, or retry can repeat the same question
            // action after the answer has already been persisted. Treat that
            // request as idempotent so the student can continue the session.
            var assessmentSession = session.AssessmentAttemptId.HasValue;
            return new ExerciseActionValidationResponse(
                true,
                "Bu soru daha önce yanıtlandı; kayıt mevcut.",
                null,
                session.CurrentStep,
                null,
                state.Answers.Count == state.Questions.Count,
                assessmentSession ? null : existingAnswer.IsCorrect,
                assessmentSession ? null : question.CorrectAnswer,
                assessmentSession ? null : question.Explanation,
                null,
                null);
        }

        var answer = request.IsTimeout
            ? TimeoutAnswer
            : NormalizeOptionAnswer(request.Answer);
        if (answer is null)
            return Invalid("Cevap A, B, C veya D olmalıdır.");

        var isCorrect = !request.IsTimeout
            && string.Equals(answer, question.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
        session.RecordAnswer(
            question.QuestionId,
            answer,
            isCorrect,
            Math.Max(0, (request.ResponseTime ?? 0) / 1000),
            question.BloomLevel,
            question.QuestionType);
        state.Answers.Add(new SessionAnswer
        {
            QuestionId = question.QuestionId,
            Answer = answer,
            IsCorrect = isCorrect,
            TimeSpentSeconds = Math.Max(0, (request.ResponseTime ?? 0) / 1000),
            BloomLevel = question.BloomLevel,
            OrderIndex = question.OrderIndex,
            QuestionType = question.QuestionType
        });
        var isAssessment = session.AssessmentAttemptId.HasValue;
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
        if (request.Number is not { } number)
            return Invalid("A grid number is required.");

        if (state.Grid is { Length: > 0 })
        {
            var grid = state.Grid.SelectMany(row => row ?? []).ToArray();
            if (request.Index is not { } cellIndex
                || cellIndex < 0
                || cellIndex >= grid.Length
                || grid[cellIndex] != number)
                return Invalid("Grid cell does not match the server-owned layout.");
        }

        var expected = state.CurrentNumber!.Value;
        var isCorrect = number == expected;
        if (isCorrect)
        {
            session.Advance();
            state.CurrentNumber++;
        }
        else
        {
            session.RecordIncorrectAttempt();
        }
        var completed = state.CurrentNumber > state.TotalSteps;
        var isAssessment = session.AssessmentAttemptId.HasValue;
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

    private static ExerciseActionValidationResponse AdvanceGeneric(ExerciseSession session)
    {
        session.AdvanceUnscored();
        return Valid(
            session.CurrentStep >= session.TotalSteps ? "Egzersiz tamamlandı." : string.Empty,
            session.CurrentStep,
            isCompleted: session.CurrentStep >= session.TotalSteps,
            isCorrect: null);
    }

    private async Task<ExerciseActionValidationResponse> ReviewVocabularyAsync(
        ExerciseSession session,
        SessionState state,
        ExerciseActionRequest request,
        Guid studentId,
        DateTime reviewedAt,
        CancellationToken cancellationToken)
    {
        var vocabularyItemId = ReadGuid(request.CustomData, "vocabularyItemId");
        if (!vocabularyItemId.HasValue)
            return Invalid("Kelime yanıtı bu oturumdaki merkezi kelime havuzuyla eşleşmiyor.");
        var itemId = vocabularyItemId.Value;
        var vocabularyWord = state.VocabularyWords.SingleOrDefault(item => item.Id == itemId);
        var reviewKind = ReadString(request.CustomData, "reviewKind")?.Trim().ToLowerInvariant();
        if (vocabularyWord is null || string.IsNullOrWhiteSpace(reviewKind))
        {
            return Invalid("Kelime yanıtı bu oturumdaki merkezi kelime havuzuyla eşleşmiyor.");
        }

        bool isCorrect;
        if (state.VocabularyMode == "quiz" && reviewKind == "quiz")
        {
            var submittedQuestionType = ReadString(request.CustomData, "questionType")?.Trim().ToLowerInvariant();
            var selectedAnswer = ReadString(request.CustomData, "selectedAnswer")?.Trim();
            if (submittedQuestionType is not ("word" or "definition") || string.IsNullOrWhiteSpace(selectedAnswer))
                return Invalid("Kelime quiz yanıtı doğrulanabilir bir seçenek içermiyor.");
            if (submittedQuestionType != vocabularyWord.QuestionType)
                return Invalid("Kelime quiz yönü bu oturumdaki soruyla eşleşmiyor.");
            var expectedAnswer = vocabularyWord.QuestionType == "word"
                ? vocabularyWord.Definition
                : vocabularyWord.Word;
            isCorrect = string.Equals(selectedAnswer, expectedAnswer, StringComparison.OrdinalIgnoreCase);
        }
        else if (state.VocabularyMode != "quiz" && reviewKind == "known")
        {
            isCorrect = true;
        }
        else if ((state.VocabularyMode != "quiz" && reviewKind is "unknown" or "timeout")
            || (state.VocabularyMode == "quiz" && reviewKind == "timeout"))
        {
            isCorrect = false;
        }
        else
        {
            return Invalid("Kelime yanıt türü desteklenmiyor.");
        }

        if (state.Answers.Any(item => item.QuestionId == itemId))
            return Invalid("Bu kelime yanıtı daha önce kaydedildi.");

        var review = await OwnedVocabularyProgressRecorder.RecordAsync(
            db,
            studentId,
            itemId,
            isCorrect,
            reviewedAt,
            awardVerifiedGamification: state.VocabularyMode == "quiz",
            cancellationToken);
        if (!review.Found)
            return Invalid("Kelime artık merkezi havuzda bulunmuyor.");

        session.RecordAnswer(
            itemId,
            reviewKind == "quiz" ? ReadString(request.CustomData, "selectedAnswer")! : reviewKind,
            isCorrect,
            Math.Max(request.ResponseTime ?? 0, 0) / 1_000,
            bloomLevel: 0);
        state.Answers.Add(new SessionAnswer
        {
            QuestionId = itemId,
            Answer = reviewKind == "quiz" ? ReadString(request.CustomData, "selectedAnswer")! : reviewKind,
            IsCorrect = isCorrect,
            TimeSpentSeconds = Math.Max(request.ResponseTime ?? 0, 0) / 1_000,
            BloomLevel = 0
        });

        return Valid(
            isCorrect ? "Kelime bilindi olarak kaydedildi." : "Kelime tekrar havuzuna alındı.",
            session.CurrentStep,
            isCompleted: session.CurrentStep >= session.TotalSteps,
            isCorrect: isCorrect,
            feedbackData: JsonSerializer.SerializeToElement(new
            {
                box = review.CurrentBox,
                masteredNow = review.IsNewMastery
            }, JsonOptions));
    }

    private static ExerciseActionValidationResponse ValidateFocusMatch(
        ExerciseSession session,
        SessionState state,
        ExerciseActionRequest request,
        string channel,
        DateTime now)
    {
        if (!IsFocusExercise(state)
            || !FocusChannels(state).Contains(channel, StringComparer.OrdinalIgnoreCase)
            || (channel == "position" && state.PositionSequence.Length == 0)
            || (channel == "word" && state.WordSequence.Length == 0))
            return Invalid("Focus exercise data is unavailable for server validation.");
        if (!state.FocusStartTime.HasValue)
            return Invalid("Focus exercise has not been started.");

        if (request.Index is not { } index
            || index < 0
            || index >= state.TotalSteps)
            return Invalid("A valid focus trial index is required.");

        if (state.FocusResponses.Any(item =>
                item.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase)
                && item.Index == index))
            return Invalid("This focus trial was already recorded.");

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
            return Invalid("Focus responses must be submitted in trial order.");

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
                return Invalid("Focus response arrived outside its trial time window.");
        }

        var isCorrect = IsFocusTarget(state, channel, index);
        state.FocusResponses.Add(new FocusResponse { Channel = channel, Index = index, IsCorrect = isCorrect });
        if (channel.Equals("position", StringComparison.OrdinalIgnoreCase))
            state.FocusLastPositionIndex = index;
        else
            state.FocusLastWordIndex = index;
        session.Advance(isCorrect);
        var isAssessment = session.AssessmentAttemptId.HasValue;
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
        ExerciseSession session,
        SessionState state)
    {
        if (!IsFocusExercise(state))
            return AdvanceGeneric(session);
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
                        session.Advance(false);
                    }
                }
            }
            state.FocusCompleted = true;
            session.SetCurrentStep(session.TotalSteps);
        }

        return Valid(
            "Odak egzersizi tamamlandı.",
            session.TotalSteps,
            isCompleted: true,
            isCorrect: null,
            feedbackData: session.AssessmentAttemptId.HasValue ? null : BuildFocusFeedback(state));
    }

    private static string[] CreateFocusWordSequence(
        int totalSteps,
        int nLevel,
        IReadOnlyCollection<int> configuredTargetIndices)
    {
        var wordPool = new[]
        {
            "kitap", "kalem", "masa", "bulut", "deniz", "orman", "şehir", "bardak",
            "çiçek", "güneş", "anahtar", "köprü", "tren", "elma", "kedi", "kuş",
            "dağ", "pencere", "çanta", "yıldız", "nehir", "ekmek", "saat", "top",
            "kapı", "telefon", "ağaç", "yol", "ay", "su"
        };
        var targetIndices = configuredTargetIndices.ToHashSet();
        var hasConfiguredTargets = targetIndices.Count > 0;
        var sequence = new string[totalSteps];

        for (var index = 0; index < sequence.Length; index++)
        {
            var isTarget = index >= nLevel
                && (hasConfiguredTargets
                    ? targetIndices.Contains(index)
                    : index == nLevel || Random.Shared.Next(4) == 0);
            if (isTarget)
            {
                sequence[index] = sequence[index - nLevel];
                continue;
            }

            var nextWord = wordPool[Random.Shared.Next(wordPool.Length)];
            while (index >= nLevel
                && string.Equals(nextWord, sequence[index - nLevel], StringComparison.OrdinalIgnoreCase))
            {
                nextWord = wordPool[Random.Shared.Next(wordPool.Length)];
            }
            sequence[index] = nextWord;
        }

        return sequence;
    }

    private static int[] CreateFocusPositionSequence(
        int totalSteps,
        int nLevel,
        int gridSize,
        IReadOnlyCollection<int> configuredTargetIndices)
    {
        var targetIndices = configuredTargetIndices.ToHashSet();
        var hasConfiguredTargets = targetIndices.Count > 0;
        var sequence = new int[totalSteps];
        var positionCount = gridSize * gridSize;

        for (var index = 0; index < sequence.Length; index++)
        {
            var isTarget = index >= nLevel
                && (hasConfiguredTargets
                    ? targetIndices.Contains(index)
                    : index == nLevel || Random.Shared.Next(4) == 0);
            if (isTarget)
            {
                sequence[index] = sequence[index - nLevel];
                continue;
            }

            var nextPosition = Random.Shared.Next(1, positionCount + 1);
            while (index >= nLevel && nextPosition == sequence[index - nLevel])
                nextPosition = Random.Shared.Next(1, positionCount + 1);
            sequence[index] = nextPosition;
        }

        return sequence;
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
        IsFocusExercise(state.ExerciseTypeName) || IsFocusEngineType(state.EngineType);

    private static bool IsTachistoscope(string typeName, string engineType, JsonElement config)
    {
        if (typeName.Equals("Tachistoscope", StringComparison.OrdinalIgnoreCase)) return true;
        if (typeName.Equals("RSVP", StringComparison.OrdinalIgnoreCase)) return false;
        var nested = ReadObject(config, "engineConfig");
        var mode = ReadString(nested, "mode") ?? ReadString(config, "mode");
        var type = ReadString(nested, "engineType") ?? ReadString(config, "engineType") ?? engineType;
        return type.Equals("text_stream", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mode, "rsvp", StringComparison.OrdinalIgnoreCase);
    }

    private static void InvalidateTachistoscopePresentations(ExerciseSession session)
    {
        var cache = JsonSerializer.SerializeToNode(ParseJsonOrEmpty(session.ProcessedActionsJson), JsonOptions)!.AsObject();
        foreach (var item in cache.ToArray())
        {
            if (item.Value?["feedbackData"]?["stimulus"] is not null)
                cache.Remove(item.Key);
        }
        session.SetProcessedActions(cache.ToJsonString());
    }

    private async Task<TachistoscopeState> CreateTachistoscopeAsync(JsonElement config, int difficulty,
        Guid? ageGroupId, CancellationToken cancellationToken)
    {
        var timing = ReadObject(config, "timing");
        var content = ReadObject(config, "content");
        var adaptive = ReadObject(config, "adaptive");
        var type = (ReadString(content, "type") ?? "word").ToLowerInvariant();
        var level = Math.Clamp(difficulty, 1, 5);
        var duration = Math.Clamp(ReadPositiveInt(config, "displayDurationMs")
            ?? ReadPositiveInt(timing, "durationMs") ?? 500, 50, 5000);
        var min = Math.Clamp(ReadPositiveInt(adaptive, "minDurationMs") ?? 50, 50, 5000);
        var max = Math.Clamp(ReadPositiveInt(adaptive, "maxDurationMs") ?? Math.Max(1000, duration), min, 5000);
        var state = new TachistoscopeState
        {
            ContentType = type,
            Count = Math.Clamp(ReadPositiveInt(config, "totalStimuli") ?? ReadPositiveInt(content, "count") ?? 20, 1, 500),
            DisplayDurationMs = Math.Clamp(duration, min, max),
            InitialDurationMs = Math.Clamp(duration, min, max),
            MinDurationMs = min, MaxDurationMs = max,
            AdaptiveEnabled = ReadProperty(adaptive, "enabled").ValueKind != JsonValueKind.False,
            TargetLength = type == "letter" ? level : type == "number" ? level + 2 : level * 2 + 1
        };
        if (type is not ("word" or "phrase" or "number" or "letter"))
            throw new ArgumentException("Takistoskop içerik türü word, phrase, number veya letter olmalıdır.");
        var customItems = ReadStringArray(content, "items").Select(item => item.Trim())
            .Where(item => item.Length is > 0 and <= 100).Distinct().Take(500).ToArray();
        if (string.Equals(ReadString(content, "source"), "custom", StringComparison.OrdinalIgnoreCase))
        {
            if (customItems.Length == 0) throw new ArgumentException("Takistoskop özel içerik listesi boş olamaz.");
            state.Pool = customItems;
            state.Source = "custom";
            // Explicit custom content must never be replaced with generated numbers or letters.
            state.ContentType = type;
        }
        else if (type is "word" or "phrase")
        {
            var words = await db.VocabularyItems.AsNoTracking()
                .Where(item => !item.IsDeleted && item.DifficultyLevel <= level
                    && (item.TargetAgeGroupId == null || item.TargetAgeGroupId == ageGroupId))
                .OrderBy(item => item.Word).Select(item => item.Word).Take(1000).ToListAsync(cancellationToken);
            state.Pool = words.Select(word => word.Trim()).Where(word => word.Length is >= 2 and <= 20
                && word.All(char.IsLetter)).Distinct().ToArray();
            state.Source = state.Pool.Length > 0 ? "vocabulary_items" : "default_pool";
            if (state.Pool.Length == 0)
                state.Pool = ["su", "ev", "bir", "göz", "yol", "masa", "kapı", "okul", "ağaç", "kitap", "kalem", "zihin",
                    "anlam", "dikkat", "kelime", "görsel", "öğrenme", "çalışma", "düşünce", "başlangıç", "verimlilik",
                    "odaklanma", "araştırma", "motivasyon", "geliştirmek", "değerlendirme", "konsantrasyon", "sürdürülebilirlik"];
            if (type == "phrase")
                state.Pool = state.Pool.Select((word, index) => word + " " + state.Pool[(index + 1) % state.Pool.Length]).ToArray();
        }
        if (state.Pool.Length > 0)
            state.TargetLength = Math.Clamp(state.TargetLength, state.Pool.Min(word => word.Length), state.Pool.Max(word => word.Length));
        state.InitialTargetLength = state.TargetLength;
        return state;
    }

    private static ExerciseActionValidationResponse PresentTachistoscope(SessionState state, ExerciseActionRequest request, DateTime now)
    {
        var tachistoscope = state.Tachistoscope;
        if (tachistoscope is null || request.Index != tachistoscope.Round || tachistoscope.Round >= tachistoscope.Count)
            return Invalid("Takistoskop turu geçerli değil. Egzersizi yeniden başlatın.");
        if (tachistoscope.PresentedAt is null)
        {
            tachistoscope.ExpectedStimulus = tachistoscope.SelectStimulus();
            tachistoscope.PresentedAt = now;
        }
        return Valid("Takistoskop uyaranı hazır.", tachistoscope.Round, feedbackData: JsonSerializer.SerializeToElement(new
        {
            round = tachistoscope.Round, stimulus = tachistoscope.ExpectedStimulus,
            displayDurationMs = tachistoscope.DisplayDurationMs, targetLength = tachistoscope.TargetLength
        }, JsonOptions));
    }

    private static ExerciseActionValidationResponse AnswerTachistoscope(ExerciseSession session, SessionState state,
        ExerciseActionRequest request, DateTime now)
    {
        var tachistoscope = state.Tachistoscope;
        if (tachistoscope is null || request.Index != tachistoscope.Round || tachistoscope.PresentedAt is null
            || tachistoscope.ExpectedStimulus.Length == 0 || request.Answer is null || request.Answer.Length > 100)
            return Invalid("Yanıt bekleyen geçerli bir Takistoskop turu yok.");
        var elapsed = (now - tachistoscope.PresentedAt.Value).TotalMilliseconds;
        if (elapsed < tachistoscope.DisplayDurationMs)
            return Invalid("Takistoskop gösterimi bitmeden yanıt gönderilemez.");
        var correct = tachistoscope.Record(tachistoscope.ExpectedStimulus, request.Answer,
            (int)Math.Clamp(elapsed - tachistoscope.DisplayDurationMs, 0, int.MaxValue));
        session.Advance(correct);
        tachistoscope.ExpectedStimulus = string.Empty;
        tachistoscope.PresentedAt = null;
        return Valid(session.AssessmentAttemptId.HasValue ? "Yanıt kaydedildi." : correct ? "Doğru." : "Yanlış.",
            tachistoscope.Round, isCompleted: tachistoscope.Round >= tachistoscope.Count,
            isCorrect: session.AssessmentAttemptId.HasValue ? null : correct,
            feedbackData: JsonSerializer.SerializeToElement(new
            {
                round = tachistoscope.Round, displayDurationMs = tachistoscope.DisplayDurationMs,
                targetLength = tachistoscope.TargetLength,
                trial = session.AssessmentAttemptId.HasValue ? null : tachistoscope.Trials.Last()
            }, JsonOptions));
    }

    private static int? ReadGridTimeLimit(JsonElement config)
    {
        var timing = ReadObject(config, "timing");
        var milliseconds = ReadPositiveInt(timing, "maxReadingTimeMs");
        return ReadPositiveInt(config, "timeLimitSeconds")
            ?? ReadPositiveInt(config, "timeLimit")
            ?? ReadPositiveInt(ReadObject(config, "rules"), "timeLimit")
            ?? ReadPositiveInt(timing, "timeLimitSec")
            ?? (milliseconds.HasValue ? (int)Math.Ceiling(milliseconds.Value / 1000d) : null);
    }

    private static bool IsTimedOut(ExerciseSession session, SessionState state, DateTime now) =>
        !IsScanning(state) && !(state.TimingStartsOnAction && !state.TimingStartedAt.HasValue)
        && session.IsTimedOut(now, state.TimingStartedAt, state.TimingPausedSecondsBeforeStart);

    private static void EnsureTimingStarted(
        ExerciseSession session,
        SessionState state,
        DateTime now)
    {
        if (state.TimingStartedAt.HasValue)
            return;

        state.TimingStartedAt = now.ToUniversalTime();
        state.TimingPausedSecondsBeforeStart = Math.Max(0, session.TotalPausedSeconds);
    }

    private static int GetTimingPausedSeconds(ExerciseSession session, SessionState state) =>
        state.TimingStartedAt.HasValue
            ? Math.Max(0, session.TotalPausedSeconds - Math.Max(0, state.TimingPausedSecondsBeforeStart))
            : Math.Max(0, session.TotalPausedSeconds);

    private static DateTime GetTimingStartTime(
        ExerciseSession session,
        SessionState state,
        DateTime now) =>
        state.TimingStartedAt
        ?? (state.TimingStartsOnAction ? now : session.StartTime);

    private static decimal? CalculateReadingWpm(SessionState state, int fallbackSeconds)
    {
        if (!SupportsServerReadingMeasurement(state)) return null;
        var seconds = state.ReadingPausedMilliseconds.HasValue
            ? (decimal)Math.Max(0, (state.ReadingEndTime!.Value - state.ReadingStartTime!.Value).TotalMilliseconds
                - state.ReadingPausedMilliseconds.Value) / 1000m
            : fallbackSeconds;
        return SpeedReadingExerciseSessionRules.CalculateValidatedRawWpm(state.WordCount, seconds);
    }

    private static bool SupportsServerReadingMeasurement(SessionState state) =>
        state.Tachistoscope is null && IsReadingExerciseFlow(state)
        && !state.ReadingIncomplete
        && state.EngineType is not ("word_highlight" or "text_stream" or "text_fade" or "regression_reduction" or "scan_find" or "scanning" or "skimming")
        && state.ReadingStartTime.HasValue
        && state.ReadingEndTime.HasValue;

    private static bool IsGrouping(SessionState state) =>
        ExerciseConfigurationRules.NormalizeEngineType(state.EngineType) == "word_highlight"
        && state.ExerciseTypeName.Equals("Chunking", StringComparison.OrdinalIgnoreCase);

    private static bool IsTextFade(SessionState state) =>
        ExerciseConfigurationRules.NormalizeEngineType(state.EngineType) == "text_fade";

    private static decimal DisplayPace(SessionState state) =>
        IsTextFade(state) ? state.FadeDisplayPaceWpm : state.GroupingDisplayPaceWpm;

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

    private static bool IsFocusEngineType(string engineType)
    {
        try
        {
            return ExerciseConfigurationRules.NormalizeEngineType(engineType) is
                "focus" or "attention_training" or "motion_path";
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsObservationOnlyMotionPath(SessionState state) =>
        IsEngineType(state.EngineType, "motion_path");

    private static bool IsValidatedFixation(SessionState state) =>
        IsEngineType(state.EngineType, "motion_path") && state.FixationPeripheralCount > 0;

    private static bool IsEngineType(string engineType, string expectedEngineType)
    {
        try
        {
            return ExerciseConfigurationRules.NormalizeEngineType(engineType)
                .Equals(expectedEngineType, StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsAdaptiveFluency(SessionState state) => state.AdaptiveEnabled;

    private static bool IsAdaptiveFluency(string exerciseTypeName, JsonElement config)
    {
        if (exerciseTypeName.Contains("adaptivefluency", StringComparison.OrdinalIgnoreCase)
            || exerciseTypeName.Contains("adaptive fluency", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ReadString(config, "engineType"), "adaptive_fluency", StringComparison.OrdinalIgnoreCase))
            return true;
        var nestedConfig = ReadObject(config, "engineConfig");
        return string.Equals(ReadString(nestedConfig, "engineType"), "adaptive_fluency", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdaptiveFluency(
        string exerciseTypeName,
        string exerciseEngineType,
        JsonElement config) =>
        string.Equals(exerciseEngineType, "adaptive_fluency", StringComparison.OrdinalIgnoreCase)
        || IsAdaptiveFluency(exerciseTypeName, config);

    private static SessionQuestion CloneQuestion(SessionQuestion question) => new()
    {
        QuestionId = question.QuestionId,
        QuestionText = question.QuestionText,
        OptionA = question.OptionA,
        OptionB = question.OptionB,
        OptionC = question.OptionC,
        OptionD = question.OptionD,
        CorrectAnswer = question.CorrectAnswer,
        Explanation = question.Explanation,
        BloomLevel = question.BloomLevel,
        DifficultyLevel = question.DifficultyLevel,
        OrderIndex = question.OrderIndex,
        QuestionType = question.QuestionType
    };

    private static JsonElement AdaptiveFeedback(SessionState state) => JsonSerializer.SerializeToElement(new
    {
        stage = state.AdaptiveStage,
        targetWpm = state.AdaptiveTargetWpm,
        purpose = state.AdaptiveStage is 1 or 2 && state.AdaptivePurposes.Length >= state.AdaptiveStage
            ? state.AdaptivePurposes[state.AdaptiveStage - 1]
            : state.AdaptiveStage == 3 ? "Yeni metinde hızınızı ve anlamanızı koruyun." : "Başlangıç düzeyinizi ölçün.",
        baselineComprehension = state.AdaptiveBaselineComprehension,
        transferComprehension = state.AdaptiveTransferComprehension,
        transferGainPercent = state.AdaptiveTransferGainPercent,
        completed = state.AdaptiveCompleted,
        stageResults = state.AdaptiveStageResults
    }, JsonOptions);

    private static bool IsVisualExpansionExercise(string exerciseTypeName) =>
        exerciseTypeName.Contains("visualexpansion", StringComparison.OrdinalIgnoreCase)
        || exerciseTypeName.Contains("visual expansion", StringComparison.OrdinalIgnoreCase)
        || exerciseTypeName.Contains("görsel genişleme", StringComparison.OrdinalIgnoreCase);

    private static bool IsVisualExpansionExercise(SessionState state) =>
        IsVisualExpansionExercise(state.ExerciseTypeName)
        || IsEngineType(state.EngineType, "visual_expansion");

    private static bool IsVisualizationExercise(string exerciseTypeName) =>
        exerciseTypeName.Contains("visualization", StringComparison.OrdinalIgnoreCase)
        || exerciseTypeName.Contains("visualisation", StringComparison.OrdinalIgnoreCase);

    private static bool IsVocabularyExercise(string exerciseTypeName, JsonElement config) =>
        exerciseTypeName.Contains("vocabulary", StringComparison.OrdinalIgnoreCase)
        || exerciseTypeName.Contains("kelime", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ReadString(config, "engineType"), "vocabulary_builder", StringComparison.OrdinalIgnoreCase);

    private static bool IsGridExercise(string exerciseTypeName, JsonElement config)
    {
        if (exerciseTypeName.Contains("schulte", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ReadString(config, "engineType"), "grid_interaction", StringComparison.OrdinalIgnoreCase))
            return true;

        var nestedConfig = ReadObject(config, "engineConfig");
        return string.Equals(ReadString(nestedConfig, "engineType"), "grid_interaction", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<VocabularyWordState>> LoadVocabularyWordsAsync(
        JsonElement config,
        Guid? profileAgeGroupId,
        int exerciseDifficultyLevel,
        CancellationToken cancellationToken)
    {
        var vocabulary = ReadObject(config, "vocabulary");
        var configuredIds = ReadGuidArray(config, "vocabularyItemIds")
            .Concat(ReadGuidArray(vocabulary, "itemIds"))
            .Distinct()
            .ToArray();
        if (configuredIds.Length == 0 && vocabulary.ValueKind != JsonValueKind.Object)
            return [];

        var query = db.VocabularyItems.AsNoTracking()
            .Where(item => !item.IsDeleted
                && (item.TargetAgeGroupId == null
                    || (profileAgeGroupId.HasValue && item.TargetAgeGroupId == profileAgeGroupId.Value)));
        if (configuredIds.Length > 0)
        {
            var items = await query.Where(item => configuredIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
            var byId = items.ToDictionary(item => item.Id);
            return configuredIds
                .Where(byId.ContainsKey)
                .Select(id => ToVocabularyWordState(byId[id]))
                .ToList();
        }

        var category = ReadString(vocabulary, "category");
        var difficulty = ReadPositiveInt(vocabulary, "difficultyLevel") ?? exerciseDifficultyLevel;
        var count = Math.Clamp(ReadPositiveInt(vocabulary, "count") ?? 10, 1, 50);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(item => item.Category == category.Trim());
        if (difficulty is >= 1 and <= 5)
            query = query.Where(item => item.DifficultyLevel == difficulty);

        return (await query.OrderBy(item => item.Word).Take(count).ToListAsync(cancellationToken))
            .Select(ToVocabularyWordState)
            .ToList();
    }

    private static VocabularyWordState ToVocabularyWordState(VocabularyItem item) => new()
    {
        Id = item.Id,
        Word = item.Word,
        Definition = item.Definition,
        ExampleSentence = item.ExampleSentence,
        Synonyms = item.Synonyms,
        Antonyms = item.Antonyms,
        Category = item.Category,
        DifficultyLevel = item.DifficultyLevel
    };

    private async Task<List<VisualizationSceneState>> LoadVisualizationScenesAsync(
        Guid exerciseId,
        JsonElement config,
        Guid? profileAgeGroupId,
        CancellationToken cancellationToken)
    {
        var configuredScenes = ReadVisualizationScenes(config);
        if (configuredScenes.Count > 0)
            return configuredScenes;

        var scenes = await db.VisualizationScenes.AsNoTracking()
            .Where(item => item.ExerciseId == exerciseId
                && !item.IsDeleted
                && (!profileAgeGroupId.HasValue
                    || item.TargetAgeGroupId == null
                    || item.TargetAgeGroupId == profileAgeGroupId.Value))
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        if (scenes.Count == 0)
            return [];

        var sceneIds = scenes.Select(item => item.Id).ToHashSet();
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
        DifficultyLevel = 1,
        OrderIndex = question.DisplayOrder
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

    private static bool IsAssessmentSessionConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation
        && string.Equals(
            postgres.ConstraintName,
            "ux_exercise_sessions_assessment_exercise",
            StringComparison.Ordinal);

    private static bool IsExerciseSessionCompletionConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation
        && postgres.ConstraintName is
            "ix_exercise_session_results_session_id"
            or "pk_reading_sessions"
            or "ix_reading_session_answers_session_id_question_id"
            or "ix_exercise_session_answers_session_id_question_id"
            or "ix_user_gamification_user_id"
            or "ix_user_achievements_user_id_achievement_id_is_deleted";

    private static bool IsActionConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation
        && postgres.ConstraintName is
            "ix_exercise_session_answers_session_id_question_id"
            or "ix_user_gamification_user_id"
            or "ix_user_achievements_user_id_achievement_id_is_deleted";

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
            var normalizedAnswer = NormalizeCompletedAnswer(answer.Answer);
            return new SessionAnswer
            {
                QuestionId = question.QuestionId,
                Answer = normalizedAnswer,
                IsCorrect = string.Equals(normalizedAnswer, question.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase),
                TimeSpentSeconds = Math.Max(answer.TimeSpentSeconds, 0),
                BloomLevel = question.BloomLevel,
                OrderIndex = question.OrderIndex,
                QuestionType = question.QuestionType
            };
        }).ToList();

        var merged = state.Answers.ToList();
        foreach (var answer in resolved)
        {
            var existing = state.Answers.SingleOrDefault(item => item.QuestionId == answer.QuestionId);
            if (existing is not null)
            {
                if (!string.Equals(existing.Answer.Trim(), answer.Answer, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("A submitted answer cannot change after it is recorded.");
                continue;
            }

            session.RecordAnswer(
                answer.QuestionId,
                answer.Answer,
                answer.IsCorrect,
                answer.TimeSpentSeconds,
                answer.BloomLevel,
                answer.QuestionType);
            merged.Add(answer);
        }

        return merged;
    }

    private static string? NormalizeOptionAnswer(string? answer)
    {
        var normalized = answer?.Trim().ToUpperInvariant();
        return normalized is "A" or "B" or "C" or "D" ? normalized : null;
    }

    private static string NormalizeCompletedAnswer(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)
            || answer.Trim().Equals(TimeoutAnswer, StringComparison.OrdinalIgnoreCase))
            return TimeoutAnswer;

        return NormalizeOptionAnswer(answer)
            ?? throw new ArgumentException("Cevap A, B, C veya D olmalıdır.", nameof(answer));
    }

    private static SpeedReading.Application.ExerciseSessions.ExerciseSessionResult ToResult(
        SpeedReading.Domain.Sessions.ExerciseSessionResult result,
        ExerciseSession session,
        SessionState state,
        decimal? score = null,
        int? xp = null,
        string? feedback = null) =>
        new(
            session.Id,
            result.StudentId,
            result.ExerciseId,
            session.CorrectCount,
            session.IncorrectCount,
            result.IsMeasured
                ? IsScanning(state) ? ScanningAccuracy(state)
                    : SpeedReadingExerciseSessionRules.CalculateAccuracy(session.CorrectCount, session.IncorrectCount)
                : null,
            result.TimeSpentSeconds,
            result.IsMeasured ? score ?? result.Score : null,
            IsScanning(state) || result.WordsRead == 0 ? null : result.WordsRead,
            !IsScanning(state) && result.IsMeasured && result.RawWpm > 0 ? result.RawWpm : null,
            result.IsMeasured && state.Questions.Count > 0 ? result.ComprehensionScore : null,
            !IsScanning(state) && result.IsMeasured && result.RawWpm > 0 ? result.WeightedKdp : null,
            xp ?? (result.IsMeasured && !state.ReadingIncomplete
                ? SpeedReadingExerciseSessionRules.CalculateXp(
                    score ?? result.Score,
                    result.ComprehensionScore,
                    result.TimeSpentSeconds)
                : 0),
            [],
            false,
            null,
            ToPublicJson(state),
            feedback ?? (score.HasValue ? "Egzersiz tamamlandı." : "Bu oturum daha önce tamamlandı."),
            null,
            result.IsMeasured ? nameof(SpeedReadingMeasurementStatus.Measured) : nameof(SpeedReadingMeasurementStatus.NotMeasured));

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

    private static AssessmentContentSnapshot DeserializeAssessmentSnapshot(string? json)
        => AssessmentContentSnapshotRules.DeserializeRequired(json);

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

    private static JsonElement ToPublicJson(SessionState state)
    {
        var json = JsonSerializer.SerializeToNode(state, JsonOptions)!.AsObject();
        if (state.Tachistoscope is { } tachistoscope)
        {
            json["tachistoscope"] = JsonSerializer.SerializeToNode(new
            {
                tachistoscope.Round, tachistoscope.Count, tachistoscope.ContentType, tachistoscope.Source,
                tachistoscope.DisplayDurationMs, tachistoscope.InitialDurationMs, tachistoscope.TargetLength,
                tachistoscope.AdaptiveEnabled,
                correctCount = state.IsAssessmentMode ? (int?)null : tachistoscope.Trials.Count(trial => trial.IsCorrect),
                incorrectCount = state.IsAssessmentMode ? (int?)null : tachistoscope.Trials.Count(trial => !trial.IsCorrect),
                trials = tachistoscope.Round == tachistoscope.Count ? tachistoscope.Trials : []
            }, JsonOptions);
        }
        var result = JsonSerializer.SerializeToElement(json, JsonOptions);
        var sanitized = JsonSerializer.SerializeToNode(state.IsAssessmentMode
            ? SpeedReadingContentSecurity.SanitizeFocusAssessmentJson(result)
            : RemoveAssessmentKeys(result), JsonOptions)!.AsObject();
        if (IsVisualExpansionExercise(state) && state.VisualExpansionProtocolVersion != 1)
        {
            sanitized.Remove("visualExpansionRoundResults");
            sanitized.Remove("visualExpansionMaxPresentedDistance");
            sanitized.Remove("visualExpansionAverageResponseTimeMs");
        }
        if (IsValidatedFixation(state) && state.FixationProtocolVersion != 1)
            sanitized.Remove("fixationRoundResults");
        if (!state.IsAssessmentMode && state.Tachistoscope is { } completed && completed.Round == completed.Count)
            sanitized["tachistoscope"]!["trials"] = JsonSerializer.SerializeToNode(completed.Trials, JsonOptions);
        return JsonSerializer.SerializeToElement(sanitized, JsonOptions);
    }

    private static JsonElement RemoveAssessmentKeys(JsonElement element) =>
        SpeedReadingContentSecurity.SanitizeAssessmentJson(element);

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

    private static int? ReadNonNegativeInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in element.EnumerateObject())
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.TryGetInt32(out var value) && value >= 0)
                return value;
        return null;
    }

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

    private static decimal? ReadDecimal(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.TryGetDecimal(out var value))
                return value;
        }
        return null;
    }

    private static Guid? ReadGuid(JsonElement element, string propertyName)
    {
        var value = ReadString(element, propertyName);
        return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
    }

    private static Guid[] ReadGuidArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return [];
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.Array)
            {
                return property.Value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String
                        && Guid.TryParse(item.GetString(), out _))
                    .Select(item => Guid.Parse(item.GetString()!))
                    .Where(item => item != Guid.Empty)
                    .ToArray();
            }
        }
        return [];
    }

    private static Guid? ReadGuid(
        IReadOnlyDictionary<string, JsonElement>? values,
        string propertyName)
    {
        if (values is null)
            return null;
        var pair = values.FirstOrDefault(item => item.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
        return pair.Value.ValueKind == JsonValueKind.String
            && Guid.TryParse(pair.Value.GetString(), out var id)
            && id != Guid.Empty
            ? id
            : null;
    }

    private static bool? ReadBoolean(
        IReadOnlyDictionary<string, JsonElement>? values,
        string propertyName)
    {
        if (values is null)
            return null;
        var pair = values.FirstOrDefault(item => item.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
        return pair.Value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? pair.Value.GetBoolean()
            : null;
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, JsonElement>? values,
        string propertyName)
    {
        if (values is null)
            return null;
        var pair = values.FirstOrDefault(item => item.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
        return pair.Value.ValueKind == JsonValueKind.String ? pair.Value.GetString() : null;
    }

    private static int[] ReadIntArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return [];
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.Array)
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
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.Array)
                return property.Value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .ToArray();
        }
        return [];
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

    private sealed record VisualExpansionRoundState(int Round, int Distance, int DisplayDurationMs, int ResponseTimeMs, bool IsCorrect);
    private sealed record FixationRoundState(int Round, int HoldMs, int ResponseTimeMs, bool IsCorrect);

    private sealed class SessionState
    {
        public Guid ExerciseId { get; set; }
        public string ExerciseTypeName { get; set; } = string.Empty;
        public string EngineType { get; set; } = string.Empty;
        public bool IsAssessmentMode { get; set; }
        public Guid? ReadingTextId { get; set; }
        public string ReadingTextTitle { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int WordCount { get; set; }
        public int DifficultyLevel { get; set; }
        public int TotalSteps { get; set; }
        public int CurrentWordIndex { get; set; }
        public TachistoscopeState? Tachistoscope { get; set; }
        public int? CurrentNumber { get; set; }
        public int GridSize { get; set; }
        public int[][]? Grid { get; set; }
        public int? TimeLimitSeconds { get; set; }
        public bool TimingStartsOnAction { get; set; }
        public DateTime? TimingStartedAt { get; set; }
        public int TimingPausedSecondsBeforeStart { get; set; }
        public DateTime? ReadingStartTime { get; set; }
        public DateTime? ReadingEndTime { get; set; }
        public bool ReadingIncomplete { get; set; }
        public int GroupingChunkSize { get; set; }
        public decimal FadeDisplayPaceWpm { get; set; }
        public int FadeLagMs { get; set; }
        public decimal FadeCompletionPercent { get; set; }
        public decimal GroupingDisplayPaceWpm { get; set; }
        public decimal GroupingCompletionPercent { get; set; }
        public string? ReadingPurpose { get; set; }
        public DateTime? ReadingPausedAt { get; set; }
        public int ReadingPausedSeconds { get; set; }
        public long? ReadingPausedMilliseconds { get; set; }
        public int ReadingMinimumMs { get; set; }
        public int ReadingMaximumMs { get; set; }
        public decimal? FinalWpm { get; set; }
        public decimal? ComprehensionScore { get; set; }
        public decimal? WeightedKdp { get; set; }
        public bool AdaptiveEnabled { get; set; }
        public int AdaptiveStage { get; set; }
        public DateTime? AdaptiveStageStartedAt { get; set; }
        public int AdaptivePausedSecondsAtStart { get; set; }
        public bool AdaptiveAwaitingQuestions { get; set; }
        public bool AdaptiveCompleted { get; set; }
        public decimal AdaptiveIncreaseThreshold { get; set; }
        public decimal AdaptiveMaintainThreshold { get; set; }
        public decimal AdaptiveSupportThreshold { get; set; }
        public decimal AdaptiveIncreasePercent { get; set; }
        public decimal AdaptiveDecreasePercent { get; set; }
        public decimal AdaptiveSupportDecreasePercent { get; set; }
        public decimal AdaptiveMinimumComprehension { get; set; }
        public decimal? AdaptiveTargetWpm { get; set; }
        public decimal? AdaptiveBaselineComprehension { get; set; }
        public decimal? AdaptiveTransferComprehension { get; set; }
        public decimal? AdaptiveTransferGainPercent { get; set; }
        public Guid? AdaptiveTransferTextId { get; set; }
        public string AdaptiveTransferTitle { get; set; } = string.Empty;
        public string AdaptiveTransferContent { get; set; } = string.Empty;
        public int AdaptiveTransferWordCount { get; set; }
        public string[] AdaptivePurposes { get; set; } = [];
        public List<SessionQuestion> AdaptivePrimaryQuestions { get; set; } = [];
        public List<SessionQuestion> AdaptiveTransferQuestions { get; set; } = [];
        public List<SessionAnswer> AdaptiveBaselineAnswers { get; set; } = [];
        public List<AdaptiveStageResult> AdaptiveStageResults { get; set; } = [];
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
        public int VisualExpansionProtocolVersion { get; set; }
        public List<VisualExpansionRoundState> VisualExpansionRoundResults { get; set; } = [];
        public int VisualExpansionMaxPresentedDistance { get; set; }
        public int VisualExpansionAverageResponseTimeMs { get; set; }
        public long VisualExpansionPausedMilliseconds { get; set; }
        public DateTime? VisualExpansionPausedAt { get; set; }
        public string VisualExpansionPattern { get; set; } = "horizontal";
        public int VisualExpansionDisplayDurationMs { get; set; } = 250;
        public int VisualExpansionStartDegrees { get; set; } = 4;
        public int VisualExpansionTargetDegrees { get; set; } = 30;
        public int VisualExpansionCurrentDegrees { get; set; } = 4;
        public int VisualExpansionRound { get; set; }
        public string[] VisualExpansionExpectedStimuli { get; set; } = [];
        public DateTime? VisualExpansionPresentedAt { get; set; }
        public int VisualExpansionPausedSecondsAtPresentation { get; set; }
        public int FixationPeripheralCount { get; set; }
        public int FixationProtocolVersion { get; set; }
        public int FixationHoldMs { get; set; } = 2_000;
        public int FixationRound { get; set; }
        public string[] FixationExpectedStimuli { get; set; } = [];
        public DateTime? FixationPresentedAt { get; set; }
        public DateTime? FixationPausedAt { get; set; }
        public long FixationPausedMilliseconds { get; set; }
        public List<FixationRoundState> FixationRoundResults { get; set; } = [];
        public List<SessionQuestion> Questions { get; set; } = [];
        public List<SessionAnswer> Answers { get; set; } = [];
        public List<ScanningRound> ScanningRounds { get; set; } = [];
        public int CurrentRound { get; set; }
        public bool ScanningCaseSensitive { get; set; }
        public bool ScanningFindAny { get; set; }
        public long? ScanningElapsedMs { get; set; }
        public List<VisualizationSceneState> VisualizationScenes { get; set; } = [];
        public string VocabularyMode { get; set; } = "learning";
        public string VocabularyQuizType { get; set; } = "mixed";
        public List<VocabularyWordState> VocabularyWords { get; set; } = [];
        public Dictionary<string, JsonElement>? CustomData { get; set; }
    }

    private sealed class AdaptiveStageResult
    {
        public int Stage { get; set; }
        public decimal Wpm { get; set; }
        public int ReadingSeconds { get; set; }
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

    private sealed class VocabularyWordState
    {
        public Guid Id { get; set; }
        public string Word { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
        public string? ExampleSentence { get; set; }
        public string? Synonyms { get; set; }
        public string? Antonyms { get; set; }
        public string Category { get; set; } = string.Empty;
        public int DifficultyLevel { get; set; }
        public string QuestionType { get; set; } = "word";
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
        public int OrderIndex { get; set; }
        public int QuestionType { get; set; }
    }

    private sealed class SessionAnswer
    {
        public Guid QuestionId { get; set; }
        public string Answer { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public int TimeSpentSeconds { get; set; }
        public int BloomLevel { get; set; }
        public int OrderIndex { get; set; }
        public int QuestionType { get; set; }
    }

    private sealed class FocusResponse
    {
        public string Channel { get; set; } = string.Empty;
        public int Index { get; set; }
        public bool IsCorrect { get; set; }
        public bool IsMiss { get; set; }
    }
}
