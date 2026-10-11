-- Only the 20 explicitly identified training exercises; assessments/history stay intact.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('visual-expansion-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE expansion_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->'previousConfiguration' old_config, item->'configuration' config
FROM jsonb_array_elements($settings${{SETTINGS}}$settings$::jsonb) item;
DO $$ BEGIN
  IF (SELECT count(*) FROM expansion_settings)<>20 OR (SELECT count(DISTINCT id) FROM expansion_settings)<>20
    THEN RAISE EXCEPTION 'Visual Expansion payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM expansion_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE e.id IS NULL OR t.name IS DISTINCT FROM 'VisualExpansion' OR NOT t.is_active OR t.is_deleted
      OR NOT e.is_active OR e.is_deleted OR e.target_age_group_id IS DISTINCT FROM s.age_id
      OR e.difficulty_level<>s.level
      OR (e.configuration_json IS DISTINCT FROM s.old_config AND e.configuration_json IS DISTINCT FROM s.config))
    THEN RAISE EXCEPTION 'Visual Expansion baseline changed; review before applying'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.config,
  updated_at=now(),updated_by='visual-expansion-v1',version=e.version+1
FROM expansion_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.config;
DO $$ BEGIN
  IF (SELECT count(*) FROM expansion_settings s JOIN speed_reading.exercises e ON e.id=s.id
    WHERE e.configuration_json=s.config)<>20 THEN RAISE EXCEPTION 'Visual Expansion verification failed'; END IF;
END $$;
COMMIT;
