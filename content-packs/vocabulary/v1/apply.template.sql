BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='120s';
SELECT pg_advisory_xact_lock(hashtext('vocabulary-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types, speed_reading.vocabulary_items IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE vocabulary_settings ON COMMIT DROP AS
SELECT (s->>'id')::uuid id,(s->>'ageGroupId')::uuid age_id,(s->>'level')::int level,
  s->>'title' title,s->>'description' description,s->'previousConfiguration' old_config,s->'configuration' config
FROM jsonb_array_elements($settings${{SETTINGS}}$settings$::jsonb) s;
DO $$ BEGIN
  IF (SELECT count(*) FROM vocabulary_settings)<>29 OR (SELECT count(DISTINCT id) FROM vocabulary_settings)<>29
    OR (SELECT count(*) FROM vocabulary_settings WHERE old_config='null'::jsonb)<>1
    THEN RAISE EXCEPTION 'Vocabulary payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM vocabulary_settings s WHERE s.level NOT BETWEEN 1 AND 5
    OR s.config->>'engineType' IS DISTINCT FROM 'vocabulary_builder'
    OR s.config#>'{engineConfig,timeLimitPerWord}' IS DISTINCT FROM '0'::jsonb
    OR s.config#>'{engineConfig,vocabulary,requireFullPool}' IS DISTINCT FROM 'true'::jsonb
    OR s.config#>'{engineConfig,vocabulary,difficultyLevel}' IS DISTINCT FROM to_jsonb(s.level)
    OR s.config#>>'{engineConfig,mode}' NOT IN ('learning','quiz')
    OR s.config#>>'{engineConfig,mode}' IS NULL
    OR (s.config#>>'{engineConfig,mode}'='learning' AND s.config#>'{engineConfig,learnThenQuiz}' IS DISTINCT FROM 'true'::jsonb)
    OR s.config#>'{engineConfig,vocabulary,count}' IS DISTINCT FROM to_jsonb((CASE s.age_id::text
      WHEN '10000000-0000-0000-0000-000000000001' THEN ARRAY[8,10,12,14,16]
      WHEN '10000000-0000-0000-0000-000000000002' THEN ARRAY[10,12,14,16,18]
      WHEN '10000000-0000-0000-0000-000000000004' THEN ARRAY[12,14,16,18,20]
      WHEN '10000000-0000-0000-0000-000000000003' THEN ARRAY[12,15,18,20,24]
      ELSE ARRAY[]::int[] END)[s.level]))
    THEN RAISE EXCEPTION 'Vocabulary payload differs from approved parameters'; END IF;
  IF (SELECT count(*) FROM speed_reading.exercise_types WHERE name='Vocabulary' AND engine_type='vocabulary_builder' AND is_active AND NOT is_deleted)<>1
    THEN RAISE EXCEPTION 'Vocabulary exercise type missing or ambiguous'; END IF;
  IF EXISTS(SELECT 1 FROM vocabulary_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE (s.old_config<>'null'::jsonb AND e.id IS NULL)
      OR (e.id IS NOT NULL AND (t.name IS DISTINCT FROM 'Vocabulary' OR t.engine_type IS DISTINCT FROM 'vocabulary_builder'
        OR NOT e.is_active OR e.is_deleted OR NOT t.is_active OR t.is_deleted
        OR e.target_age_group_id IS DISTINCT FROM s.age_id OR e.difficulty_level IS DISTINCT FROM s.level
        OR (e.configuration_json IS DISTINCT FROM s.old_config AND e.configuration_json IS DISTINCT FROM s.config))))
    THEN RAISE EXCEPTION 'Vocabulary baseline changed; review before applying'; END IF;
  IF EXISTS(SELECT 1 FROM vocabulary_settings s WHERE NOT EXISTS(
    SELECT 1 FROM (
      SELECT word,definition,count(*) OVER(PARTITION BY lower(trim(word))) word_uses,
        count(*) OVER(PARTITION BY lower(trim(definition))) definition_uses
      FROM speed_reading.vocabulary_items v WHERE NOT v.is_deleted
        AND (v.target_age_group_id IS NULL OR v.target_age_group_id=s.age_id) AND v.difficulty_level=s.level
    ) pool WHERE word_uses=1 AND definition_uses=1 AND trim(word)<>'' AND trim(definition)<>''
    HAVING count(*) >= (s.config#>>'{engineConfig,vocabulary,count}')::int))
    THEN RAISE EXCEPTION 'Vocabulary content pool insufficient'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.config,updated_at=now(),updated_by='vocabulary-v1',version=e.version+1
FROM vocabulary_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.config;
INSERT INTO speed_reading.exercises(id,exercise_type_id,title,description,type_code,difficulty_level,configuration_json,
  target_age_group_id,creator_id,is_active,created_at,created_by,version,is_deleted)
SELECT s.id,t.id,s.title,s.description,'Vocabulary',s.level,s.config,s.age_id,
  'b993e2f8-3948-49bd-9cbd-463e8771c20d'::uuid,true,now(),'vocabulary-v1',1,false
FROM vocabulary_settings s JOIN speed_reading.exercise_types t ON t.name='Vocabulary' AND t.is_active AND NOT t.is_deleted
WHERE s.old_config='null'::jsonb ON CONFLICT(id) DO NOTHING;
DO $$ BEGIN
  IF (SELECT count(*) FROM vocabulary_settings s JOIN speed_reading.exercises e ON e.id=s.id WHERE e.configuration_json=s.config)<>29
    THEN RAISE EXCEPTION 'Vocabulary verification failed'; END IF;
END $$;
COMMIT;
