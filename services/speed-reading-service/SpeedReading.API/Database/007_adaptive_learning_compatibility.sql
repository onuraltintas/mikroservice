-- Adaptive learning tables used by the legacy content service.
-- Keep this additive and idempotent so an existing restored database remains
-- the source of truth.
CREATE TABLE IF NOT EXISTS "StudentLearningProfiles" (
    "Id" uuid PRIMARY KEY,
    "StudentId" uuid NOT NULL,
    "ProficiencyLevel" text NOT NULL,
    "PreferredContentTypes" text NULL,
    "LearningPace" text NOT NULL,
    "WeakAreas" text NULL,
    "StrongAreas" text NULL,
    "CreatedAt" timestamptz NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAt" timestamptz NULL,
    "DeletedBy" uuid NULL
);

CREATE TABLE IF NOT EXISTS "ContentRecommendations" (
    "Id" uuid PRIMARY KEY,
    "StudentId" uuid NOT NULL,
    "ReadingTextId" uuid NOT NULL,
    "ConfidenceScore" numeric NOT NULL,
    "RecommendationReason" text NULL,
    "CreatedAt" timestamptz NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAt" timestamptz NULL,
    "DeletedBy" uuid NULL
);

CREATE TABLE IF NOT EXISTS "DailyGoals" (
    "Id" uuid PRIMARY KEY,
    "StudentId" uuid NOT NULL,
    "Date" timestamptz NOT NULL,
    "TargetMinutes" integer NOT NULL,
    "ActualMinutes" integer NOT NULL DEFAULT 0,
    "IsCompleted" boolean NOT NULL DEFAULT false,
    "CreatedAt" timestamptz NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAt" timestamptz NULL,
    "DeletedBy" uuid NULL
);

CREATE INDEX IF NOT EXISTS "IX_StudentLearningProfiles_StudentId"
    ON "StudentLearningProfiles" ("StudentId");
CREATE INDEX IF NOT EXISTS "IX_ContentRecommendations_StudentId"
    ON "ContentRecommendations" ("StudentId");
CREATE INDEX IF NOT EXISTS "IX_ContentRecommendations_ReadingTextId"
    ON "ContentRecommendations" ("ReadingTextId");
CREATE INDEX IF NOT EXISTS "IX_DailyGoals_StudentId_Date"
    ON "DailyGoals" ("StudentId", "Date");
