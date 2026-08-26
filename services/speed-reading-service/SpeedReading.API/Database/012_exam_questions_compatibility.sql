-- Exam/question-bank content from the legacy content service.
CREATE TABLE IF NOT EXISTS "ExamQuestions" (
    "Id" uuid PRIMARY KEY,
    "Content" text NOT NULL,
    "Question" text NOT NULL,
    "OptionA" text NOT NULL,
    "OptionB" text NOT NULL,
    "OptionC" text NOT NULL,
    "OptionD" text NOT NULL,
    "OptionE" text NULL,
    "CorrectOption" text NOT NULL,
    "ExamType" integer NOT NULL DEFAULT 0,
    "Difficulty" integer NOT NULL DEFAULT 1,
    "WordCount" integer NOT NULL DEFAULT 0,
    "Topic" text NULL,
    "Category" integer NOT NULL DEFAULT 0,
    "TargetAgeGroupConfigurationId" uuid NULL,
    "CreatedAt" timestamptz NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAt" timestamptz NULL,
    "DeletedBy" uuid NULL
);

CREATE INDEX IF NOT EXISTS "IX_ExamQuestions_TargetAgeGroupConfigurationId"
    ON "ExamQuestions" ("TargetAgeGroupConfigurationId");
CREATE INDEX IF NOT EXISTS "IX_ExamQuestions_ExamType"
    ON "ExamQuestions" ("ExamType");
CREATE INDEX IF NOT EXISTS "IX_ExamQuestions_Category"
    ON "ExamQuestions" ("Category");
