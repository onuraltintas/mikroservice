-- Vocabulary items and spaced-repetition progress from the legacy content service.
CREATE TABLE IF NOT EXISTS "VocabularyItems" (
    "Id" uuid PRIMARY KEY,
    "Word" text NOT NULL,
    "Definition" text NOT NULL,
    "ExampleSentence" text NULL,
    "Synonyms" text NULL,
    "Antonyms" text NULL,
    "TargetAgeGroupConfigurationId" uuid NULL,
    "DifficultyLevel" integer NOT NULL DEFAULT 1,
    "Category" text NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAt" timestamptz NULL,
    "DeletedBy" uuid NULL
);

CREATE TABLE IF NOT EXISTS "UserVocabularyProgresses" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL,
    "VocabularyItemId" uuid NOT NULL,
    "Box" integer NOT NULL DEFAULT 1,
    "ConsecutiveCorrectCount" integer NOT NULL DEFAULT 0,
    "NextReviewDate" timestamptz NOT NULL,
    "LastReviewedAt" timestamptz NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAt" timestamptz NULL,
    "DeletedBy" uuid NULL
);

CREATE INDEX IF NOT EXISTS "IX_VocabularyItems_TargetAgeGroupConfigurationId"
    ON "VocabularyItems" ("TargetAgeGroupConfigurationId");
CREATE INDEX IF NOT EXISTS "IX_UserVocabularyProgresses_UserId"
    ON "UserVocabularyProgresses" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_UserVocabularyProgresses_VocabularyItemId"
    ON "UserVocabularyProgresses" ("VocabularyItemId");
