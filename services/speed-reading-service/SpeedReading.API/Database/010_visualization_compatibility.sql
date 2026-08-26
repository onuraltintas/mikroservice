-- Visualization scenes and questions used by the exercise player and editor.
CREATE TABLE IF NOT EXISTS "VisualizationScenes" (
    "Id" uuid PRIMARY KEY,
    "ExerciseId" uuid NOT NULL,
    "Description" text NOT NULL,
    "ImageUrl" text NULL,
    "Duration" integer NOT NULL DEFAULT 10,
    "DisplayOrder" integer NOT NULL DEFAULT 0,
    "DifficultyLevel" integer NOT NULL DEFAULT 1,
    "TargetAgeGroupConfigurationId" uuid NULL,
    "CreatedAt" timestamptz NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAt" timestamptz NULL,
    "DeletedBy" uuid NULL
);

CREATE TABLE IF NOT EXISTS "VisualizationQuestions" (
    "Id" uuid PRIMARY KEY,
    "SceneId" uuid NOT NULL,
    "QuestionText" text NOT NULL,
    "OptionsJson" text NOT NULL,
    "CorrectAnswer" text NOT NULL,
    "QuestionType" text NOT NULL DEFAULT 'detail',
    "DisplayOrder" integer NOT NULL DEFAULT 0,
    "HintText" text NULL,
    "CreatedAt" timestamptz NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAt" timestamptz NULL,
    "DeletedBy" uuid NULL
);

CREATE INDEX IF NOT EXISTS "IX_VisualizationScenes_ExerciseId"
    ON "VisualizationScenes" ("ExerciseId");
CREATE INDEX IF NOT EXISTS "IX_VisualizationScenes_TargetAgeGroupConfigurationId"
    ON "VisualizationScenes" ("TargetAgeGroupConfigurationId");
CREATE INDEX IF NOT EXISTS "IX_VisualizationQuestions_SceneId"
    ON "VisualizationQuestions" ("SceneId");
