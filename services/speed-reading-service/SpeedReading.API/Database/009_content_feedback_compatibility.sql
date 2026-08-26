-- Content interaction and feedback table from the legacy content service.
CREATE TABLE IF NOT EXISTS "UserContentFeedbacks" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL,
    "ContentId" uuid NOT NULL,
    "ContentType" varchar(50) NOT NULL,
    "Rating" integer NULL,
    "IsLiked" boolean NOT NULL DEFAULT false,
    "IsBookmarked" boolean NOT NULL DEFAULT false,
    "SkipReason" text NULL,
    "CompletionRate" numeric NOT NULL DEFAULT 0,
    "TimeSpentSeconds" integer NOT NULL DEFAULT 0,
    "ExpectedTimeSeconds" integer NOT NULL DEFAULT 0,
    "ComprehensionScore" numeric NULL,
    "ExerciseScore" numeric NULL,
    "RetryCount" integer NOT NULL DEFAULT 0,
    "InteractionCount" integer NOT NULL DEFAULT 0,
    "PauseCount" integer NOT NULL DEFAULT 0,
    "AbandonedAtPercentage" numeric NULL,
    "SessionDate" timestamptz NOT NULL,
    "TimeOfDay" integer NOT NULL DEFAULT 0,
    "DeviceType" varchar(50) NOT NULL,
    "ContentCategory" text NULL,
    "ContentDifficultyLevel" integer NULL,
    "CreatedAt" timestamptz NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAt" timestamptz NULL,
    "DeletedBy" uuid NULL
);

CREATE INDEX IF NOT EXISTS "IX_UserContentFeedbacks_UserId_SessionDate"
    ON "UserContentFeedbacks" ("UserId", "SessionDate");
CREATE INDEX IF NOT EXISTS "IX_UserContentFeedbacks_UserId_ContentType_ContentId"
    ON "UserContentFeedbacks" ("UserId", "ContentType", "ContentId");
