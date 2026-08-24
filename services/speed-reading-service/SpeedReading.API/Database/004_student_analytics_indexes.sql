-- Student-scoped analytics indexes for the existing speed-reading schema.
-- The migration-only startup executes this script idempotently; no legacy
-- rows are changed or deleted.
CREATE INDEX IF NOT EXISTS "IX_ReadingSessions_IsDeleted_UserId_CompletedAt"
    ON "ReadingSessions" ("IsDeleted", "UserId", "CompletedAt");

CREATE INDEX IF NOT EXISTS "IX_DailyExerciseLogs_IsDeleted_UserId_CompletedDate"
    ON "DailyExerciseLogs" ("IsDeleted", "UserId", "CompletedDate");

CREATE INDEX IF NOT EXISTS "IX_StudentProgramProgresses_IsDeleted_UserId_AssignedDate"
    ON "StudentProgramProgresses" ("IsDeleted", "UserId", "AssignedDate");
