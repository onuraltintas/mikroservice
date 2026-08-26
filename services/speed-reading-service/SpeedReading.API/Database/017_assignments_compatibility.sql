-- Compatibility schema for the legacy teacher/student assignment workflow.
-- Existing assignment rows are preserved; this script is safe to run repeatedly.

CREATE TABLE IF NOT EXISTS "Assignments" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "TeacherId" uuid NOT NULL,
    "ExerciseId" uuid NOT NULL,
    "ReadingTextId" uuid NULL,
    "Title" text NOT NULL,
    "Description" text NOT NULL,
    "DueDate" timestamp with time zone NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL,
    "DeletedAt" timestamp with time zone NULL,
    "DeletedBy" uuid NULL
);

CREATE TABLE IF NOT EXISTS "StudentAssignments" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "AssignmentId" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "IsCompleted" boolean NOT NULL,
    "CompletionDate" timestamp with time zone NULL,
    "ResultId" uuid NULL,
    "Score" numeric(18,2) NULL,
    "KeyPerformanceMetric" numeric(18,2) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL,
    "DeletedAt" timestamp with time zone NULL,
    "DeletedBy" uuid NULL
);

CREATE INDEX IF NOT EXISTS "IX_Assignments_TeacherId" ON "Assignments" ("TeacherId");
CREATE INDEX IF NOT EXISTS "IX_Assignments_ExerciseId" ON "Assignments" ("ExerciseId");
CREATE INDEX IF NOT EXISTS "IX_Assignments_ReadingTextId" ON "Assignments" ("ReadingTextId");
CREATE INDEX IF NOT EXISTS "IX_Assignments_IsDeleted_TeacherId_CreatedAt"
    ON "Assignments" ("IsDeleted", "TeacherId", "CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_StudentAssignments_AssignmentId" ON "StudentAssignments" ("AssignmentId");
CREATE INDEX IF NOT EXISTS "IX_StudentAssignments_StudentId" ON "StudentAssignments" ("StudentId");
CREATE INDEX IF NOT EXISTS "IX_StudentAssignments_ResultId" ON "StudentAssignments" ("ResultId");
CREATE INDEX IF NOT EXISTS "IX_StudentAssignments_IsDeleted_StudentId_CreatedAt"
    ON "StudentAssignments" ("IsDeleted", "StudentId", "CreatedAt");
