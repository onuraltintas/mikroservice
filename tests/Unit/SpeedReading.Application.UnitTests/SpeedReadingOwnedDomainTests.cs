using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.AgeGroups;
using SpeedReading.Domain.Assignments;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.LearningPaths;
using SpeedReading.Domain.Gamification;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Profiles;
using SpeedReading.Domain.QuestionBank;
using SpeedReading.Domain.Visualization;
using SpeedReading.Domain.Vocabulary;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingOwnedDomainTests
{
    [Fact]
    public void Owned_model_uses_a_dedicated_schema_and_normalized_tables()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        context.Model.FindEntityType(typeof(Exercise))!.GetSchema().Should().Be("speed_reading");
        context.Model.FindEntityType(typeof(ExerciseTypeCategory))!.GetTableName().Should().Be("exercise_type_categories");
        context.Model.FindEntityType(typeof(ExerciseType))!.GetTableName().Should().Be("exercise_types");
        context.Model.FindEntityType(typeof(Exercise))!.GetTableName().Should().Be("exercises");
        context.Model.FindEntityType(typeof(ReadingText))!.GetTableName().Should().Be("reading_texts");
        context.Model.FindEntityType(typeof(ExerciseSession))!.GetTableName().Should().Be("exercise_sessions");
        context.Model.FindEntityType(typeof(ExerciseSessionResult))!.GetTableName().Should().Be("exercise_session_results");
        context.Model.FindEntityType(typeof(ReadingSession))!.GetTableName().Should().Be("reading_sessions");
        context.Model.FindEntityType(typeof(Assignment))!.GetTableName().Should().Be("assignments");
        context.Model.FindEntityType(typeof(StudentAssignment))!.GetTableName().Should().Be("student_assignments");
        context.Model.FindEntityType(typeof(ProgramTemplate))!.GetTableName().Should().Be("program_templates");
        context.Model.FindEntityType(typeof(StudentProgramProgress))!.GetTableName().Should().Be("student_program_progress");
        context.Model.FindEntityType(typeof(DailyExerciseLog))!.GetTableName().Should().Be("daily_exercise_logs");
        context.Model.FindEntityType(typeof(AgeGroupConfiguration))!.GetTableName()
            .Should().Be("age_group_configurations");
        context.Model.FindEntityType(typeof(SpeedReadingUserProfile))!.GetTableName()
            .Should().Be("user_profiles");
        context.Model.FindEntityType(typeof(LearningPathTemplate))!.GetTableName()
            .Should().Be("learning_path_templates");
        context.Model.FindEntityType(typeof(LearningPathNode))!.GetTableName()
            .Should().Be("learning_path_nodes");
        context.Model.FindEntityType(typeof(LearningPathNodeContent))!.GetTableName()
            .Should().Be("learning_path_node_contents");
        context.Model.FindEntityType(typeof(LearningPathPrerequisite))!.GetTableName()
            .Should().Be("learning_path_prerequisites");
        context.Model.FindEntityType(typeof(StudentLearningPathProgress))!.GetTableName()
            .Should().Be("student_learning_path_progress");
        context.Model.FindEntityType(typeof(StudentLearningNodeProgress))!.GetTableName()
            .Should().Be("student_learning_node_progress");
        context.Model.FindEntityType(typeof(PersonalizedLearningPathItem))!.GetTableName()
            .Should().Be("personalized_learning_path_items");
        context.Model.FindEntityType(typeof(EduPlatform.Shared.Infrastructure.Middleware.AdminAuditRecord))!
            .GetTableName()
            .Should().Be("admin_audit_records");
        context.Model.FindEntityType(typeof(Achievement))!.GetTableName()
            .Should().Be("achievements");
        context.Model.FindEntityType(typeof(UserAchievement))!.GetTableName()
            .Should().Be("user_achievements");
        context.Model.FindEntityType(typeof(UserGamification))!.GetTableName()
            .Should().Be("user_gamification");
        context.Model.FindEntityType(typeof(ExamQuestion))!.GetTableName()
            .Should().Be("exam_questions");
        context.Model.FindEntityType(typeof(VisualizationScene))!.GetTableName()
            .Should().Be("visualization_scenes");
        context.Model.FindEntityType(typeof(VisualizationQuestion))!.GetTableName()
            .Should().Be("visualization_questions");
        context.Model.FindEntityType(typeof(VocabularyItem))!.GetTableName()
            .Should().Be("vocabulary_items");
        context.Model.FindEntityType(typeof(UserVocabularyProgress))!.GetTableName()
            .Should().Be("user_vocabulary_progress");
        context.Model.GetEntityTypes()
            .Should()
            .Contain(entity => entity.GetTableName() == "idempotency_records");
    }

    [Fact]
    public void Owned_model_enforces_one_result_per_session()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        var hasUniqueSessionIndex = context.Model
            .FindEntityType(typeof(ExerciseSessionResult))!
            .GetIndexes()
            .Any(item => item.IsUnique
                && item.Properties.Count == 1
                && item.Properties.Single().Name == nameof(ExerciseSessionResult.SessionId));

        hasUniqueSessionIndex.Should().BeTrue();
    }

    [Fact]
    public void Owned_model_allows_reassignment_after_a_soft_removed_membership()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        var index = context.Model.FindEntityType(typeof(StudentAssignment))!
            .GetIndexes()
            .Single(item => item.IsUnique
                && item.Properties.Select(property => property.Name)
                    .SequenceEqual(new[]
                    {
                        nameof(StudentAssignment.AssignmentId),
                        nameof(StudentAssignment.StudentId)
                    }));

        index.GetFilter().Should().Be("\"IsActive\" = TRUE");
    }

    [Fact]
    public void Owned_create_script_does_not_reference_legacy_tables()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        var script = context.Database.GenerateCreateScript();

        script.Should().Contain("speed_reading");
        script.Should().Contain("reading_questions");
        script.Should().Contain("exercise_session_results");
        script.Should().NotContain("ContentBlocks");
        script.Should().NotContain("speed_reading.legacy_");
    }

    [Fact]
    public void Owned_context_discovers_only_its_owned_migration()
    {
        using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);

        context.Database.GetMigrations()
            .Should()
            .Contain("20260827110000_CreateOwnedSpeedReadingCore")
            .And.Contain("20260827120000_AddOwnedReadingSessionHistory")
            .And.Contain("20260827130000_AddOwnedAssignments")
            .And.Contain("20260827140000_AddOwnedProgramsAndDailyProgress")
            .And.Contain("20260827141000_AddOwnedWriteSupport")
            .And.Contain("20260827142000_AddOwnedAgeGroups")
            .And.Contain("20260827143000_AddOwnedUserProfiles")
            .And.Contain("20260827144000_AddOwnedCatalogWriteSupport")
            .And.Contain("20260827145000_AddOwnedLearningPaths")
            .And.Contain("20260827146000_AddOwnedAdminAudit")
            .And.Contain("20260827147000_AddOwnedGamification")
            .And.Contain("20260827148000_AddOwnedQuestionBank")
            .And.Contain("20260827149000_AddOwnedVisualizationAndVocabulary");
    }

    [Fact]
    public void Age_group_rejects_inconsistent_wpm_bounds()
    {
        var action = () => AgeGroupConfiguration.Create(
            Guid.NewGuid(),
            "Çocuk",
            "Çocuklar",
            6,
            10,
            200,
            150,
            300,
            70,
            20,
            2,
            1,
            true,
            null,
            Guid.NewGuid(),
            DateTime.UtcNow);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void User_profile_updates_assessment_targets_without_identity_fields()
    {
        var userId = Guid.NewGuid();
        var profile = SpeedReadingUserProfile.CreateDefault(
            Guid.NewGuid(),
            userId,
            DateTime.UtcNow,
            userId.ToString());

        profile.ApplyAssessment(4, 275, 82.5m, userId, DateTime.UtcNow);

        profile.CurrentLevel.Should().Be(4);
        profile.TargetWPM.Should().Be(275);
        profile.TargetComprehension.Should().Be(82.5m);
    }

    [Fact]
    public void Deleted_catalog_entities_are_inactive_and_audited()
    {
        var actorId = Guid.NewGuid();
        var type = ExerciseType.Create(
            Guid.NewGuid(),
            "speed",
            "Speed",
            "speed",
            null);

        type.Delete(actorId, DateTime.UtcNow);

        type.IsDeleted.Should().BeTrue();
        type.IsActive.Should().BeFalse();
        type.DeletedBy.Should().Be(actorId.ToString());
    }

    [Fact]
    public void Learning_path_template_tracks_nodes_and_soft_deletion()
    {
        var actorId = Guid.NewGuid();
        var template = LearningPathTemplate.Create(
            Guid.NewGuid(),
            "Odaklanma yolu",
            null,
            "Başlangıç yolu",
            14,
            true,
            actorId,
            DateTime.UtcNow);

        template.SetTotalNodes(3, actorId, DateTime.UtcNow);
        template.Delete(actorId, DateTime.UtcNow);

        template.TotalNodes.Should().Be(3);
        template.IsDeleted.Should().BeTrue();
        template.IsActive.Should().BeFalse();
        template.DeletedBy.Should().Be(actorId.ToString());
    }

    [Fact]
    public void Learning_path_progress_updates_completion_state()
    {
        var studentId = Guid.NewGuid();
        var progress = StudentLearningPathProgress.Import(
            Guid.NewGuid(),
            studentId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            false,
            false,
            null,
            null,
            DateTime.UtcNow,
            studentId.ToString(),
            null,
            null);

        progress.UpdateState(100, true, null, studentId, DateTime.UtcNow);

        progress.Progress.Should().Be(100);
        progress.IsCompleted.Should().BeTrue();
        progress.CurrentNodeId.Should().BeNull();
        progress.UpdatedBy.Should().Be(studentId.ToString());
    }

    [Fact]
    public void Gamification_recalculates_level_after_xp_award()
    {
        var userId = Guid.NewGuid();
        var stats = UserGamification.CreateDefault(
            Guid.NewGuid(),
            userId,
            DateTime.UtcNow,
            userId.ToString());

        stats.AwardXp(250, userId, DateTime.UtcNow);

        stats.TotalXP.Should().Be(250);
        stats.CurrentLevel.Should().Be(2);
        stats.CurrentLevelXP.Should().Be(100);
        stats.LevelTitle.Should().Be("Başlangıç Okuyucu");
    }

    [Fact]
    public void Exam_question_normalizes_answers_and_calculates_word_count()
    {
        var question = ExamQuestion.Create(
            Guid.NewGuid(),
            "Bir iki üç",
            "Hangisi doğrudur?",
            "Bir",
            "İki",
            "Üç",
            "Dört",
            null,
            " c ",
            1,
            2,
            0,
            null,
            3,
            null,
            DateTime.UtcNow,
            Guid.NewGuid());

        question.CorrectOption.Should().Be("C");
        question.WordCount.Should().Be(3);
    }

    [Fact]
    public void Vocabulary_progress_advances_boxes_and_is_reactivatable()
    {
        var userId = Guid.NewGuid();
        var progress = UserVocabularyProgress.Create(Guid.NewGuid(), userId, Guid.NewGuid(), DateTime.UtcNow);

        progress.Review(true, userId, DateTime.UtcNow);
        progress.Box.Should().Be(2);
        progress.NextReviewDate.Should().BeAfter(progress.LastReviewedAt);

        progress.Review(false, userId, DateTime.UtcNow);
        progress.Box.Should().Be(1);
    }

    [Fact]
    public void Exercise_requires_a_title_and_type_code()
    {
        var exerciseTypeId = Guid.NewGuid();
        var act = () => Exercise.Create(
            title: " ",
            typeCode: "SpeedReading",
            configurationJson: "{}",
            difficultyLevel: 1,
            creatorId: Guid.NewGuid(),
            exerciseTypeId: exerciseTypeId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Exercise_keeps_the_owned_exercise_type_reference()
    {
        var exerciseTypeId = Guid.NewGuid();
        var exercise = Exercise.Create(
            title: "Hızlı okuma",
            typeCode: "SpeedReading",
            configurationJson: "{}",
            difficultyLevel: 1,
            creatorId: Guid.NewGuid(),
            exerciseTypeId: exerciseTypeId);

        exercise.ExerciseTypeId.Should().Be(exerciseTypeId);
    }

    [Fact]
    public void Imported_catalog_entities_keep_source_identity_and_audit_metadata()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var creatorId = Guid.NewGuid();

        var exercise = Exercise.Import(
            id,
            title: "Hızlı okuma",
            typeCode: "SpeedReading",
            configurationJson: "{}",
            difficultyLevel: 1,
            creatorId,
            exerciseTypeId: Guid.NewGuid(),
            createdAt: createdAt,
            targetAgeGroupId: null,
            description: null,
            isActive: true,
            createdBy: creatorId.ToString(),
            updatedAt: null,
            updatedBy: null);

        exercise.Id.Should().Be(id);
        exercise.CreatedAt.Should().Be(createdAt);
        exercise.CreatedBy.Should().Be(creatorId.ToString());
    }

    [Fact]
    public void Session_starts_active_with_zero_progress()
    {
        var session = ExerciseSession.Start(
            studentId: Guid.NewGuid(),
            exerciseId: Guid.NewGuid(),
            readingTextId: null,
            totalSteps: 10,
            startedAt: DateTime.UtcNow,
            timeLimitSeconds: 120);

        session.Status.Should().Be(ExerciseSessionStatus.Active);
        session.CurrentStep.Should().Be(0);
        session.TotalSteps.Should().Be(10);
        session.CorrectCount.Should().Be(0);
        session.IncorrectCount.Should().Be(0);
    }

    [Fact]
    public void Imported_session_preserves_completed_state_and_answers()
    {
        var sessionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var session = ExerciseSession.Import(
            id: sessionId,
            studentId: Guid.NewGuid(),
            exerciseId: Guid.NewGuid(),
            readingTextId: Guid.NewGuid(),
            studentAssignmentId: null,
            status: ExerciseSessionStatus.Completed,
            startTime: DateTime.UtcNow.AddMinutes(-3),
            endTime: DateTime.UtcNow,
            totalPausedSeconds: 5,
            pausedAt: null,
            timeLimitSeconds: 120,
            currentStep: 2,
            totalSteps: 2,
            correctCount: 1,
            incorrectCount: 1,
            sessionDataJson: "{}",
            customDataJson: null,
            processedActionsJson: "{}",
            createdAt: DateTime.UtcNow.AddDays(-1),
            createdBy: null,
            updatedAt: null,
            updatedBy: null);

        session.ImportAnswer(ExerciseSessionAnswer.Import(
            Guid.NewGuid(), sessionId, questionId, "A", true, 2, 1));

        session.Status.Should().Be(ExerciseSessionStatus.Completed);
        session.CurrentStep.Should().Be(2);
        session.Answers.Should().ContainSingle(item => item.QuestionId == questionId);
    }

    [Fact]
    public void Imported_result_can_keep_a_missing_legacy_session_reference()
    {
        var result = ExerciseSessionResult.Import(
            id: Guid.NewGuid(),
            sessionId: null,
            studentId: Guid.NewGuid(),
            exerciseId: Guid.NewGuid(),
            readingTextId: null,
            wordsRead: 100,
            timeSpentSeconds: 60,
            rawWpm: 100,
            comprehensionScore: 80,
            weightedKdp: 80,
            score: 80,
            completedAt: DateTime.UtcNow,
            questionAnswersJson: "[]",
            readingMovementsJson: "[]",
            legacySessionId: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            createdBy: null,
            updatedAt: null,
            updatedBy: null);

        result.SessionId.Should().BeNull();
    }

    [Fact]
    public void Assignment_requires_a_teacher_exercise_and_title()
    {
        var act = () => Assignment.Create(
            teacherId: Guid.Empty,
            exerciseId: Guid.NewGuid(),
            readingTextId: null,
            title: "Ödev",
            description: null,
            dueDate: DateTime.UtcNow.AddDays(1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Student_assignment_completion_is_idempotent_and_preserves_first_result()
    {
        var studentAssignment = StudentAssignment.Assign(
            assignmentId: Guid.NewGuid(),
            studentId: Guid.NewGuid(),
            id: Guid.NewGuid(),
            assignedAt: DateTime.UtcNow);
        var firstResultId = Guid.NewGuid();
        var secondResultId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow;

        studentAssignment.Complete(firstResultId, 85, 80, completedAt);
        studentAssignment.Complete(secondResultId, 20, 10, completedAt.AddMinutes(1));

        studentAssignment.IsCompleted.Should().BeTrue();
        studentAssignment.ResultId.Should().Be(firstResultId);
        studentAssignment.Score.Should().Be(85);
        studentAssignment.KeyPerformanceMetric.Should().Be(80);
        studentAssignment.CompletionDate.Should().Be(completedAt);
    }

    [Fact]
    public void Student_program_progress_reset_clears_runtime_progress()
    {
        var progress = StudentProgramProgress.Import(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            programTemplateId: Guid.NewGuid(),
            assignedDate: DateTime.UtcNow.AddDays(-10),
            currentDay: 8,
            currentWeek: 2,
            currentDifficultyLevel: 3,
            daysCompleted: 7,
            exercisesCompleted: 18,
            lastCompletionDate: DateTime.UtcNow.AddDays(-1),
            isActive: true,
            completedDate: null,
            averageSuccessRate: 82,
            currentStreak: 4,
            longestStreak: 6,
            createdAt: DateTime.UtcNow.AddDays(-10),
            createdBy: null,
            updatedAt: null,
            updatedBy: null);

        progress.Reset(Guid.NewGuid(), DateTime.UtcNow);

        progress.CurrentDay.Should().Be(1);
        progress.CurrentWeek.Should().Be(1);
        progress.DaysCompleted.Should().Be(0);
        progress.ExercisesCompleted.Should().Be(0);
        progress.AverageSuccessRate.Should().Be(0);
        progress.CurrentStreak.Should().Be(0);
        progress.LongestStreak.Should().Be(0);
        progress.IsActive.Should().BeTrue();
        progress.CompletedDate.Should().BeNull();
    }

    [Fact]
    public void Program_template_update_and_clone_preserve_owned_domain_rules()
    {
        var template = ProgramTemplate.Create(
            name: "Başlangıç",
            description: "İlk program",
            targetAgeGroupConfigurationId: Guid.NewGuid(),
            minAssessmentScore: 0,
            maxAssessmentScore: 100,
            weeklyPatternJson: "{}",
            initialDifficultyLevel: 1,
            weeksPerDifficultyIncrease: 2,
            maxDifficultyLevel: 5,
            totalWeeks: 4,
            totalDays: 28,
            isActive: true,
            displayOrder: 1,
            programType: 1,
            examType: null,
            isAssessment: false,
            actorId: Guid.NewGuid(),
            createdAt: DateTime.UtcNow);

        var actorId = Guid.NewGuid();
        template.Update(
            "Güncel program",
            "Güncel açıklama",
            template.TargetAgeGroupConfigurationId,
            10,
            90,
            "{\"days\":28}",
            2,
            3,
            6,
            5,
            35,
            false,
            2,
            2,
            "TYT",
            true,
            actorId,
            DateTime.UtcNow);

        var clone = template.Clone(Guid.NewGuid(), actorId, DateTime.UtcNow);

        template.Name.Should().Be("Güncel program");
        template.IsActive.Should().BeFalse();
        clone.Id.Should().NotBe(template.Id);
        clone.Name.Should().Be("Güncel program - Kopya");
        clone.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Student_progress_applies_the_last_daily_completion_and_closes_program()
    {
        var at = DateTime.UtcNow;
        var progress = StudentProgramProgress.Import(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            at.AddDays(-1),
            currentDay: 1,
            currentWeek: 1,
            currentDifficultyLevel: 1,
            daysCompleted: 0,
            exercisesCompleted: 0,
            lastCompletionDate: null,
            isActive: true,
            completedDate: null,
            averageSuccessRate: 0,
            currentStreak: 0,
            longestStreak: 0,
            createdAt: at.AddDays(-1),
            createdBy: null,
            updatedAt: null,
            updatedBy: null);
        var template = ProgramTemplate.Import(
            Guid.NewGuid(),
            "Tek gün",
            "",
            Guid.NewGuid(),
            0,
            100,
            "{}",
            1,
            1,
            1,
            1,
            1,
            true,
            1,
            1,
            null,
            false,
            at.AddDays(-1),
            null,
            null,
            null);

        var result = progress.ApplyExerciseCompletion(
            averageSuccessRate: 85,
            wasPreviouslyPassed: false,
            completedCount: 1,
            expectedCount: 1,
            template,
            Guid.NewGuid(),
            at);

        result.DayCompleted.Should().BeTrue();
        result.ProgramCompleted.Should().BeTrue();
        progress.IsActive.Should().BeFalse();
        progress.CompletedDate.Should().Be(at);
        progress.ExercisesCompleted.Should().Be(1);
        progress.CurrentStreak.Should().Be(1);
    }

    [Fact]
    public void Session_rejects_the_same_question_twice()
    {
        var session = ExerciseSession.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            totalSteps: 2,
            DateTime.UtcNow,
            timeLimitSeconds: null);
        var questionId = Guid.NewGuid();

        session.RecordAnswer(questionId, "A", isCorrect: true, timeSpentSeconds: 3, bloomLevel: 2);

        var act = () => session.RecordAnswer(questionId, "A", isCorrect: true, 3, 2);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Pausing_and_resuming_accumulates_paused_seconds()
    {
        var startedAt = DateTime.UtcNow.AddMinutes(-2);
        var pausedAt = startedAt.AddSeconds(30);
        var resumedAt = pausedAt.AddSeconds(20);
        var session = ExerciseSession.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            totalSteps: 1,
            startedAt,
            timeLimitSeconds: null);

        session.Pause(pausedAt);
        session.Resume(resumedAt);

        session.Status.Should().Be(ExerciseSessionStatus.Active);
        session.TotalPausedSeconds.Should().Be(20);
        session.PausedAt.Should().BeNull();
    }
}
