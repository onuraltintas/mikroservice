-- Read-only analytics indexes for the existing speed-reading schema.
-- The migration-only startup executes this script idempotently; no legacy
-- rows are changed or deleted.
CREATE INDEX IF NOT EXISTS "IX_ReadingSessions_IsDeleted_CompletedAt_UserId"
    ON "ReadingSessions" ("IsDeleted", "CompletedAt", "UserId");

CREATE INDEX IF NOT EXISTS "IX_Users_IsDeleted_InstitutionId_Id"
    ON "Users" ("IsDeleted", "InstitutionId", "Id");
