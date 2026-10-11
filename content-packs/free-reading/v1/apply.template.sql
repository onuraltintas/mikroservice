-- Speed Reading DB only. Verified backup and quiesced API required before production use.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='120s';
SELECT pg_advisory_xact_lock(hashtext('free-reading-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types, speed_reading.reading_texts,
  speed_reading.reading_questions IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE free_reading_settings ON COMMIT DROP AS
SELECT (s->>'id')::uuid id,(s->>'ageGroupId')::uuid age_id,(s->>'level')::int level,
  s->>'title' title,s->>'description' description,s->'previousConfiguration' old_config,s->'configuration' config
FROM jsonb_array_elements($settings${{SETTINGS}}$settings$::jsonb) s;
CREATE TEMP TABLE free_reading_content ON COMMIT DROP AS
SELECT (s->>'id')::uuid id,(s->>'exerciseId')::uuid exercise_id,(s->>'ageGroupId')::uuid age_id,
  (s->>'level')::int level,s->>'title' title,s->>'content' content,(s->>'wordCount')::int word_count
FROM jsonb_array_elements($content${{CONTENT}}$content$::jsonb) s;
DO $$ BEGIN
  IF (SELECT count(*) FROM free_reading_settings)<>20 OR (SELECT count(DISTINCT id) FROM free_reading_settings)<>20
    OR (SELECT count(*) FROM free_reading_settings WHERE old_config='null'::jsonb)<>7
    OR (SELECT count(*) FROM free_reading_content)<>100 OR (SELECT count(DISTINCT id) FROM free_reading_content)<>100
    THEN RAISE EXCEPTION 'Free Reading payload incomplete'; END IF;
  IF (SELECT count(*) FROM speed_reading.exercise_types WHERE name='FreeReading' AND engine_type='free_reading'
    AND is_active AND NOT is_deleted)<>1 THEN RAISE EXCEPTION 'Free Reading type missing or ambiguous'; END IF;
  IF EXISTS(SELECT 1 FROM free_reading_settings s WHERE s.level NOT BETWEEN 1 AND 5
    OR s.config->>'engineType' IS DISTINCT FROM 'free_reading'
    OR s.config#>>'{engineConfig,readingPurpose}' IS DISTINCT FROM 'practice'
    OR s.config#>>'{engineConfig,content,source}' IS DISTINCT FROM 'reading_text'
    OR s.config#>'{engineConfig,timing,minReadingTimeMs}' IS DISTINCT FROM '10000'::jsonb
    OR s.config#>'{engineConfig,timing,maxReadingTimeMs}' IS DISTINCT FROM '0'::jsonb)
    THEN RAISE EXCEPTION 'Free Reading settings invalid'; END IF;
  IF EXISTS(SELECT 1 FROM free_reading_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE (s.old_config<>'null'::jsonb AND e.id IS NULL)
      OR (e.id IS NOT NULL AND (t.name IS DISTINCT FROM 'FreeReading' OR t.engine_type IS DISTINCT FROM 'free_reading'
        OR NOT e.is_active OR e.is_deleted OR e.target_age_group_id IS DISTINCT FROM s.age_id
        OR e.difficulty_level IS DISTINCT FROM s.level
        OR (e.configuration_json IS DISTINCT FROM s.old_config AND e.configuration_json IS DISTINCT FROM s.config))))
    THEN RAISE EXCEPTION 'Free Reading baseline changed'; END IF;
  IF EXISTS(SELECT 1 FROM free_reading_settings GROUP BY age_id HAVING count(*)<>5 OR count(DISTINCT level)<>5)
    OR (SELECT count(DISTINCT age_id) FROM free_reading_settings)<>4
    THEN RAISE EXCEPTION 'Free Reading needs all twenty age/level cells'; END IF;
  IF EXISTS(SELECT 1 FROM free_reading_content c LEFT JOIN free_reading_settings s ON s.id=c.exercise_id
    WHERE s.id IS NULL OR c.age_id IS DISTINCT FROM s.age_id OR c.level IS DISTINCT FROM s.level
      OR c.content IS NULL OR btrim(c.content)='' OR c.title IS NULL OR btrim(c.title)=''
      OR cardinality(regexp_split_to_array(btrim(c.content), E'\\s+')) IS DISTINCT FROM c.word_count
      OR c.word_count NOT BETWEEN (s.config#>>'{engineConfig,content,minWordCount}')::int
        AND (s.config#>>'{engineConfig,content,maxWordCount}')::int)
    THEN RAISE EXCEPTION 'Free Reading content band mismatch'; END IF;
  IF EXISTS(SELECT 1 FROM free_reading_settings s LEFT JOIN free_reading_content c ON c.exercise_id=s.id
    GROUP BY s.id HAVING count(c.id)<>5 OR count(DISTINCT c.content)<>5)
    THEN RAISE EXCEPTION 'Free Reading needs five distinct texts per exercise'; END IF;
  IF EXISTS(SELECT 1 FROM free_reading_content c JOIN speed_reading.reading_texts t ON t.id=c.id
    WHERE t.content IS DISTINCT FROM c.content OR t.title IS DISTINCT FROM c.title OR t.word_count IS DISTINCT FROM c.word_count
      OR t.exercise_id IS DISTINCT FROM c.exercise_id OR t.target_age_group_id IS DISTINCT FROM c.age_id
      OR t.difficulty_level IS DISTINCT FROM c.level OR t.category IS DISTINCT FROM 'free-reading'
      OR t.language IS DISTINCT FROM 'tr' OR NOT t.is_active OR t.is_deleted)
    OR EXISTS(SELECT 1 FROM free_reading_content c JOIN speed_reading.reading_questions q ON q.reading_text_id=c.id WHERE NOT q.is_deleted)
    THEN RAISE EXCEPTION 'Free Reading text collision or later edit'; END IF;
END $$;
INSERT INTO speed_reading.exercises(id,exercise_type_id,title,description,type_code,difficulty_level,configuration_json,
  target_age_group_id,creator_id,is_active,created_at,created_by,version,is_deleted)
SELECT s.id,t.id,s.title,s.description,'FreeReading',s.level,s.config,s.age_id,
  'b993e2f8-3948-49bd-9cbd-463e8771c20d'::uuid,true,now(),'free-reading-v1',1,false
FROM free_reading_settings s JOIN speed_reading.exercise_types t ON t.name='FreeReading' AND t.is_active AND NOT t.is_deleted
WHERE s.old_config='null'::jsonb ON CONFLICT(id) DO NOTHING;
UPDATE speed_reading.exercises e SET configuration_json=s.config,updated_at=now(),updated_by='free-reading-v1',version=e.version+1
FROM free_reading_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.config;
INSERT INTO speed_reading.reading_texts(id,title,content,word_count,category,difficulty_level,target_age_group_id,
  language,is_active,tags,recommended_min_level,recommended_max_level,average_comprehension_score,times_read,
  average_reading_time_seconds,exercise_id,created_at,created_by,version,is_deleted)
SELECT id,title,content,word_count,'free-reading',level,age_id,'tr',true,'free-reading-v1;original-selection',level,level,
  0,0,0,exercise_id,now(),'free-reading-v1',1,false FROM free_reading_content ON CONFLICT(id) DO NOTHING;
DO $$ BEGIN
  IF (SELECT count(*) FROM free_reading_settings s JOIN speed_reading.exercises e ON e.id=s.id WHERE e.configuration_json=s.config)<>20
    OR (SELECT count(*) FROM free_reading_content c JOIN speed_reading.reading_texts t ON t.id=c.id)<>100
    THEN RAISE EXCEPTION 'Free Reading final verification failed'; END IF;
END $$;
COMMIT;
