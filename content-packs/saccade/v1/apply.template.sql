-- Only identified Saccade training records; no history, assessment or assignment deletion.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('saccade-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE saccade_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->'previousConfiguration' old_config, item->'configuration' config
FROM jsonb_array_elements($settings${{SETTINGS}}$settings$::jsonb) item;
DO $$ BEGIN
  IF (SELECT count(*) FROM saccade_settings)<>20 OR (SELECT count(DISTINCT id) FROM saccade_settings)<>20
    THEN RAISE EXCEPTION 'Saccade payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM saccade_settings s
    WHERE s.config->>'engineType' IS DISTINCT FROM 'motion_path'
      OR s.config#>>'{engineConfig,mode}' IS DISTINCT FROM 'saccade'
      OR NOT CASE WHEN jsonb_typeof(s.config#>'{engineConfig,timing,holdMs}')='number'
        AND s.config#>>'{engineConfig,timing,holdMs}' ~ '^[1-9][0-9]{0,4}$'
        THEN (s.config#>>'{engineConfig,timing,holdMs}')::int BETWEEN 50 AND 10000 ELSE false END
      OR NOT CASE WHEN jsonb_typeof(s.config#>'{engineConfig,timing,durationSeconds}')='number'
        AND s.config#>>'{engineConfig,timing,durationSeconds}' ~ '^[1-9][0-9]{0,4}$'
        THEN (s.config#>>'{engineConfig,timing,durationSeconds}')::int BETWEEN 5 AND 3600 ELSE false END
      OR (s.config #- '{engineConfig,timing,holdMs}' #- '{engineConfig,timing,durationSeconds}')
        IS DISTINCT FROM (s.old_config #- '{engineConfig,timing,holdMs}' #- '{engineConfig,timing,durationSeconds}'))
    THEN RAISE EXCEPTION 'Saccade payload changes unapproved fields'; END IF;
  IF EXISTS(SELECT 1 FROM saccade_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE e.id IS NULL OR t.name IS DISTINCT FROM 'Saccade' OR t.engine_type IS DISTINCT FROM 'motion_path'
      OR NOT t.is_active OR t.is_deleted OR NOT e.is_active OR e.is_deleted
      OR e.target_age_group_id IS DISTINCT FROM s.age_id OR e.difficulty_level IS DISTINCT FROM s.level
      OR (e.configuration_json IS DISTINCT FROM s.old_config AND e.configuration_json IS DISTINCT FROM s.config))
    THEN RAISE EXCEPTION 'Saccade baseline changed; review before applying'; END IF;
  IF EXISTS(SELECT 1 FROM saccade_settings s JOIN speed_reading.exercises e
      ON e.target_age_group_id=s.age_id AND e.difficulty_level=s.level AND e.id<>s.id
      JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
      WHERE t.name='Saccade' AND e.is_active AND NOT e.is_deleted)
    THEN RAISE EXCEPTION 'Saccade duplicate training record; review before applying'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.config,
  updated_at=now(),updated_by='saccade-v1',version=e.version+1
FROM saccade_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.config;
DO $$ BEGIN
  IF (SELECT count(*) FROM saccade_settings s JOIN speed_reading.exercises e ON e.id=s.id
    WHERE e.configuration_json=s.config)<>20 THEN RAISE EXCEPTION 'Saccade verification failed'; END IF;
END $$;
COMMIT;
