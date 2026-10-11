-- Restore only these 20 catalog configurations, never delete sessions/results/programs.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('eye-tracking-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE tracking_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->'previousConfiguration' old_config, item->'configuration' config
FROM jsonb_array_elements($settings${{SETTINGS}}$settings$::jsonb) item;
DO $$ BEGIN
  IF (SELECT count(*) FROM tracking_settings)<>20 OR (SELECT count(DISTINCT id) FROM tracking_settings)<>20
    THEN RAISE EXCEPTION 'EyeTracking rollback payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM tracking_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE e.id IS NULL OR t.name IS DISTINCT FROM 'EyeTracking' OR t.engine_type IS DISTINCT FROM 'motion_path'
      OR NOT t.is_active OR t.is_deleted OR NOT e.is_active OR e.is_deleted
      OR e.target_age_group_id IS DISTINCT FROM s.age_id OR e.difficulty_level IS DISTINCT FROM s.level
      OR (e.configuration_json IS DISTINCT FROM s.config AND e.configuration_json IS DISTINCT FROM s.old_config))
    THEN RAISE EXCEPTION 'EyeTracking rollback baseline changed; review before applying'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.old_config,updated_at=now(),updated_by='eye-tracking-v1-rollback',version=e.version+1
FROM tracking_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.old_config;
COMMIT;
