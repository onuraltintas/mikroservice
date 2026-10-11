-- Speed Reading DB only; take a backup and quiesce the API before a controlled release.
-- Only tracking timing/path changes. Existing sessions, programs and results are preserved.
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
    OR (SELECT count(DISTINCT age_id) FROM tracking_settings)<>4
    OR EXISTS(SELECT 1 FROM tracking_settings GROUP BY age_id HAVING count(DISTINCT level)<>5)
    THEN RAISE EXCEPTION 'EyeTracking payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM tracking_settings s WHERE s.age_id IS NULL OR s.level IS NULL OR s.level NOT BETWEEN 1 AND 5
    OR s.old_config IS NULL OR s.config->>'engineType' IS DISTINCT FROM 'motion_path'
    OR s.config#>>'{engineConfig,mode}' IS DISTINCT FROM 'tracking'
    OR (s.config->>'difficultyLevel')::int IS DISTINCT FROM s.level
    OR s.config#>>'{engineConfig,path,type}' IS DISTINCT FROM
      CASE WHEN s.level=1 THEN 'horizontal' WHEN s.level IN(2,3) THEN 'circle' ELSE 'infinity8' END
    OR NOT CASE WHEN jsonb_typeof(s.config#>'{engineConfig,timing,speedMs}')='number'
      AND s.config#>>'{engineConfig,timing,speedMs}' ~ '^[1-9][0-9]{0,4}$'
      THEN (s.config#>>'{engineConfig,timing,speedMs}')::int BETWEEN 50 AND 60000 ELSE false END
    OR NOT CASE WHEN jsonb_typeof(s.config#>'{engineConfig,timing,durationSeconds}')='number'
      AND s.config#>>'{engineConfig,timing,durationSeconds}' ~ '^[1-9][0-9]{0,4}$'
      THEN (s.config#>>'{engineConfig,timing,durationSeconds}')::int BETWEEN 5 AND 3600 ELSE false END
    OR (s.config #- '{engineConfig,timing,speedMs}' #- '{engineConfig,timing,durationSeconds}' #- '{engineConfig,path,type}')
      IS DISTINCT FROM (s.old_config #- '{engineConfig,timing,speedMs}' #- '{engineConfig,timing,durationSeconds}' #- '{engineConfig,path,type}'))
    THEN RAISE EXCEPTION 'EyeTracking payload changes unapproved fields'; END IF;
  IF EXISTS(SELECT 1 FROM tracking_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE e.id IS NULL OR t.name IS DISTINCT FROM 'EyeTracking' OR t.engine_type IS DISTINCT FROM 'motion_path'
      OR NOT t.is_active OR t.is_deleted OR NOT e.is_active OR e.is_deleted
      OR e.target_age_group_id IS DISTINCT FROM s.age_id OR e.difficulty_level IS DISTINCT FROM s.level
      OR (e.configuration_json IS DISTINCT FROM s.config AND e.configuration_json IS DISTINCT FROM s.old_config))
    THEN RAISE EXCEPTION 'EyeTracking baseline changed; review before applying'; END IF;
  IF EXISTS(SELECT 1 FROM tracking_settings s JOIN speed_reading.exercises e
    ON e.target_age_group_id=s.age_id AND e.difficulty_level=s.level AND e.id<>s.id
    JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE t.name='EyeTracking' AND e.is_active AND NOT e.is_deleted)
    THEN RAISE EXCEPTION 'EyeTracking duplicate training record'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.config,updated_at=now(),updated_by='eye-tracking-v1',version=e.version+1
FROM tracking_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.config;
DO $$ BEGIN
  IF (SELECT count(*) FROM tracking_settings s JOIN speed_reading.exercises e ON e.id=s.id WHERE e.configuration_json=s.config)<>20
    THEN RAISE EXCEPTION 'EyeTracking verification failed'; END IF;
END $$;
COMMIT;
