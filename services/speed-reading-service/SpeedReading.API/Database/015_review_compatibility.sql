-- ExerciseReviewItems is owned by the Content database. This additive script
-- lets the SpeedReading API read/write the same SM-2 review queue.
CREATE TABLE IF NOT EXISTS "ExerciseReviewItems" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ExerciseId" uuid NOT NULL,
    "ProgramTemplateId" uuid NULL,
    "NextReviewDate" timestamp with time zone NOT NULL,
    "ReviewCount" integer NOT NULL DEFAULT 0,
    "IntervalDays" integer NOT NULL DEFAULT 1,
    "EasinessFactor" double precision NOT NULL DEFAULT 2.5,
    "IsMastered" boolean NOT NULL DEFAULT false,
    "LastScore" double precision NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAt" timestamp with time zone NULL,
    "DeletedBy" uuid NULL,
    CONSTRAINT "PK_ExerciseReviewItems" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_ExerciseReviewItems_UserId"
    ON "ExerciseReviewItems" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_ExerciseReviewItems_ExerciseId"
    ON "ExerciseReviewItems" ("ExerciseId");
CREATE INDEX IF NOT EXISTS "IX_ExerciseReviewItems_ProgramTemplateId"
    ON "ExerciseReviewItems" ("ProgramTemplateId");
