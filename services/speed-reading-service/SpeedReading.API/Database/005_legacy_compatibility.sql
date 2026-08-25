-- Compatibility additions required by the independent Speed Reading service.
-- The legacy dump predates EngineType; existing content is preserved and the
-- stable legacy Name is used as the initial engine key for existing rows.
ALTER TABLE IF EXISTS "ExerciseTypes"
    ADD COLUMN IF NOT EXISTS "EngineType" character varying(100) NOT NULL DEFAULT '';

UPDATE "ExerciseTypes"
SET "EngineType" = "Name"
WHERE "EngineType" = ''
  AND "Name" <> '';
