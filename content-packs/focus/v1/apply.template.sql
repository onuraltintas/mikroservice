-- Only the twenty identified normal Focus records; assessments and history stay unchanged.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('focus-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE focus_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->'previousConfiguration' old_config, item->'configuration' config
FROM jsonb_array_elements($settings${{SETTINGS}}$settings$::jsonb) item;
DO $$ BEGIN
  IF (SELECT count(*) FROM focus_settings)<>20 OR (SELECT count(DISTINCT id) FROM focus_settings)<>20
    OR (SELECT count(DISTINCT (age_id,level)) FROM focus_settings)<>20
    THEN RAISE EXCEPTION 'Focus payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM focus_settings s
    WHERE s.config->>'engineType' IS DISTINCT FROM 'focus'
      OR s.config#>>'{engineConfig,mode}' IS DISTINCT FROM 'position'
      OR s.config#>'{engineConfig,gridSize}' IS DISTINCT FROM '3'::jsonb
      OR NOT CASE WHEN jsonb_typeof(s.config#>'{engineConfig,nLevel}')='number'
        THEN s.config#>>'{engineConfig,nLevel}' IN ('1','2','3') ELSE false END
      OR NOT CASE WHEN jsonb_typeof(s.config#>'{engineConfig,speedMs}')='number'
        AND s.config#>>'{engineConfig,speedMs}' ~ '^[1-9][0-9]{0,4}$'
        THEN (s.config#>>'{engineConfig,speedMs}')::int BETWEEN 2500 AND 3100 ELSE false END
      OR NOT CASE WHEN jsonb_typeof(s.config#>'{engineConfig,totalSteps}')='number'
        THEN s.config#>>'{engineConfig,totalSteps}' IN ('32','40','48','56','64') ELSE false END
      OR (s.config #- '{engineConfig,nLevel}' #- '{engineConfig,speedMs}' #- '{engineConfig,totalSteps}')
        IS DISTINCT FROM (s.old_config #- '{engineConfig,nLevel}' #- '{engineConfig,speedMs}' #- '{engineConfig,totalSteps}'))
    THEN RAISE EXCEPTION 'Focus payload changes unapproved fields'; END IF;
  IF EXISTS(SELECT 1 FROM focus_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE e.id IS NULL OR t.name IS DISTINCT FROM 'Focus' OR t.engine_type IS DISTINCT FROM 'focus'
      OR NOT t.is_active OR t.is_deleted OR NOT e.is_active OR e.is_deleted
      OR e.target_age_group_id IS DISTINCT FROM s.age_id OR e.difficulty_level IS DISTINCT FROM s.level
      OR (e.configuration_json IS DISTINCT FROM s.old_config AND e.configuration_json IS DISTINCT FROM s.config))
    THEN RAISE EXCEPTION 'Focus baseline changed; review before applying'; END IF;
  IF EXISTS(SELECT 1 FROM focus_settings s JOIN speed_reading.exercises e
      ON e.target_age_group_id=s.age_id AND e.difficulty_level=s.level AND e.id<>s.id
      JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
      WHERE t.name='Focus' AND e.is_active AND NOT e.is_deleted)
    THEN RAISE EXCEPTION 'Focus duplicate training record; review before applying'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.config,
  updated_at=now(),updated_by='focus-v1',version=e.version+1
FROM focus_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.config;
DO $$ BEGIN
  IF (SELECT count(*) FROM focus_settings s JOIN speed_reading.exercises e ON e.id=s.id
    WHERE e.configuration_json=s.config)<>20 THEN RAISE EXCEPTION 'Focus verification failed'; END IF;
END $$;
COMMIT;
