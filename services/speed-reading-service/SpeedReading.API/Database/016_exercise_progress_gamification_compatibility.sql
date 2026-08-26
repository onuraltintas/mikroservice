-- Additive compatibility for the exercise-session, daily-progress and
-- gamification fields used by the independent Speed Reading service.
-- Existing rows remain untouched; defaults only make newly introduced
-- non-null columns readable by the legacy database model.

ALTER TABLE IF EXISTS "ExerciseSessions"
    ADD COLUMN IF NOT EXISTS "PausedAt" timestamp with time zone NULL;

ALTER TABLE IF EXISTS "ExerciseSessions"
    ADD COLUMN IF NOT EXISTS "TimeLimitSeconds" integer NULL;

ALTER TABLE IF EXISTS "ExerciseSessions"
    ADD COLUMN IF NOT EXISTS "ProcessedActionsJson" text NOT NULL DEFAULT '{}';

ALTER TABLE IF EXISTS "StudentExerciseResults"
    ADD COLUMN IF NOT EXISTS "SessionId" uuid NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_StudentExerciseResults_SessionId"
    ON "StudentExerciseResults" ("SessionId")
    WHERE "SessionId" IS NOT NULL;

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "ResultDataJson" text NOT NULL DEFAULT '{}';

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "DayOfWeek" integer NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "TimeOfDay" interval NOT NULL DEFAULT interval '0 seconds';

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "AverageResponseTimeMs" numeric NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "MedianResponseTimeMs" numeric NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "StdDevResponseTimeMs" numeric NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "PauseCount" integer NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "TotalPausedSeconds" integer NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "PerformanceTrend" numeric NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "IsPersonalBest" boolean NOT NULL DEFAULT false;

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "PreviousAverageScore" numeric NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "CurrentStreak" integer NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "EngagementScore" numeric NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "FrustrationScore" numeric NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "LearningRate" numeric NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "DailyExerciseLogs"
    ADD COLUMN IF NOT EXISTS "ConsistencyScore" numeric NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "MaxWPM" integer NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "MaxComprehensionScore" numeric NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "TotalExercisesCompleted" integer NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "TotalReadingSessionsCompleted" integer NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "CompletedExerciseTypesJson" text NOT NULL DEFAULT '[]';

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "MaxRSVPWPM" integer NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "MaxRSVPComprehension" numeric NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "TotalVocabularyWordsLearned" integer NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "MaxVocabularyBoxReached" integer NOT NULL DEFAULT 1;

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "TotalVocabularyQuestionsAnswered" integer NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "VocabularyMasteryLevel" integer NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "MaxVocabularyStreak" integer NOT NULL DEFAULT 0;

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "LearnedVocabularyCategoriesJson" text NOT NULL DEFAULT '[]';

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "LearnedVocabularyCategoriesMapJson" text NOT NULL DEFAULT '{}';

ALTER TABLE IF EXISTS "UserGameifications"
    ADD COLUMN IF NOT EXISTS "LearnedVocabularyDifficultiesJson" text NOT NULL DEFAULT '{}';
