-- Adaptive text profile and recommendation history tables.
CREATE TABLE IF NOT EXISTS "StudentReadingProfiles" (
    "Id" uuid PRIMARY KEY,
    "StudentId" uuid NOT NULL,
    "CurrentReadingLevel" integer NOT NULL,
    "AverageComprehensionScore" numeric NOT NULL,
    "AverageReadingSpeed" numeric NOT NULL,
    "TotalTextsRead" integer NOT NULL,
    "TotalReadingTimeSeconds" integer NOT NULL,
    "PreferredCategories" text[] NOT NULL,
    "DifficultCategories" text[] NOT NULL,
    "LastCalculatedAt" timestamptz NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAt" timestamptz NULL,
    "DeletedBy" uuid NULL
);

CREATE TABLE IF NOT EXISTS "TextRecommendationHistories" (
    "Id" uuid PRIMARY KEY,
    "StudentId" uuid NOT NULL,
    "ReadingTextId" uuid NOT NULL,
    "RecommendedAt" timestamptz NOT NULL,
    "WasAccepted" boolean NOT NULL DEFAULT false,
    "ConfidenceScore" numeric NOT NULL DEFAULT 0,
    "ReasoningJson" text NOT NULL DEFAULT '{}',
    "StudentLevelAtTime" integer NOT NULL DEFAULT 1,
    "ResultScore" numeric NULL,
    "CompletedAt" timestamptz NULL,
    "CreatedAt" timestamptz NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAt" timestamptz NULL,
    "DeletedBy" uuid NULL
);

CREATE INDEX IF NOT EXISTS "IX_StudentReadingProfiles_StudentId"
    ON "StudentReadingProfiles" ("StudentId");
CREATE INDEX IF NOT EXISTS "IX_TextRecommendationHistories_StudentId"
    ON "TextRecommendationHistories" ("StudentId");
CREATE INDEX IF NOT EXISTS "IX_TextRecommendationHistories_ReadingTextId"
    ON "TextRecommendationHistories" ("ReadingTextId");
