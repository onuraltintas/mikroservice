-- Speed Reading DB only. Verified backup and quiesced API required before production use.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='120s';
SELECT pg_advisory_xact_lock(hashtext('error-analysis-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types, speed_reading.reading_texts,
  speed_reading.reading_questions IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE proofreading_settings ON COMMIT DROP AS
SELECT (s->>'id')::uuid id,(s->>'ageGroupId')::uuid age_id,(s->>'level')::int level,
  s->'previousConfiguration' old_config,s->'configuration' config
FROM jsonb_array_elements($settings${{SETTINGS}}$settings$::jsonb) s;
CREATE TEMP TABLE proofreading_content ON COMMIT DROP AS
SELECT (s->>'id')::uuid id,(s->>'exerciseId')::uuid exercise_id,(s->>'ageGroupId')::uuid age_id,
  (s->>'level')::int level,s->>'title' title,s->>'content' content,(s->>'wordCount')::int word_count,s->'errors' errors
FROM jsonb_array_elements($content${{CONTENT}}$content$::jsonb) s;
DO $$ BEGIN
  IF (SELECT count(*) FROM proofreading_settings)<>8 OR (SELECT count(DISTINCT id) FROM proofreading_settings)<>8
    OR (SELECT count(*) FROM proofreading_content)<>40 OR (SELECT count(DISTINCT id) FROM proofreading_content)<>40
    THEN RAISE EXCEPTION 'Proofreading payload incomplete'; END IF;
  IF (SELECT count(*) FROM speed_reading.exercise_types WHERE name='ErrorAnalysis' AND engine_type='error_analysis'
    AND is_active AND NOT is_deleted)<>1 THEN RAISE EXCEPTION 'Proofreading type missing or ambiguous'; END IF;
  IF EXISTS(SELECT 1 FROM proofreading_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE e.id IS NULL OR t.name IS DISTINCT FROM 'ErrorAnalysis' OR t.engine_type IS DISTINCT FROM 'error_analysis'
      OR NOT e.is_active OR e.is_deleted OR e.target_age_group_id IS DISTINCT FROM s.age_id
      OR e.difficulty_level IS DISTINCT FROM s.level
      OR (e.configuration_json IS DISTINCT FROM s.old_config AND e.configuration_json IS DISTINCT FROM s.config))
    THEN RAISE EXCEPTION 'Proofreading baseline changed'; END IF;
  IF EXISTS(SELECT 1 FROM proofreading_settings GROUP BY age_id HAVING count(*)<>2 OR min(level)<>4 OR max(level)<>5)
    OR (SELECT count(DISTINCT age_id) FROM proofreading_settings)<>4
    OR EXISTS(SELECT 1 FROM proofreading_settings WHERE config->>'engineType' IS DISTINCT FROM 'error_analysis'
      OR config#>>'{engineConfig,content,source}' IS DISTINCT FROM 'reading_text'
      OR config#>'{engineConfig,errorCount}' IS DISTINCT FROM old_config#>'{engineConfig,errorCount}'
      OR config#>'{engineConfig,timeLimit}' IS DISTINCT FROM old_config#>'{engineConfig,timeLimit}')
    THEN RAISE EXCEPTION 'Proofreading cells or preserved limits invalid'; END IF;
  IF EXISTS(SELECT 1 FROM proofreading_content c LEFT JOIN proofreading_settings s ON s.id=c.exercise_id
    WHERE s.id IS NULL OR c.age_id IS DISTINCT FROM s.age_id OR c.level IS DISTINCT FROM s.level
      OR c.content IS NULL OR btrim(c.content)='' OR c.title IS NULL OR btrim(c.title)=''
      OR cardinality(regexp_split_to_array(btrim(c.content), E'\\s+')) IS DISTINCT FROM c.word_count
      OR c.word_count NOT BETWEEN (s.config#>>'{engineConfig,content,minWordCount}')::int
        AND (s.config#>>'{engineConfig,content,maxWordCount}')::int
      OR c.errors IS DISTINCT FROM s.config->'engineConfig'->'errorsByTextId'->c.id::text
      OR jsonb_array_length(c.errors)<>(s.config#>>'{engineConfig,errorCount}')::int)
    THEN RAISE EXCEPTION 'Proofreading content or target count mismatch'; END IF;
  IF EXISTS(SELECT 1 FROM proofreading_settings s LEFT JOIN proofreading_content c ON c.exercise_id=s.id
    GROUP BY s.id,s.config HAVING count(c.id)<>5 OR count(DISTINCT c.content)<>5
      OR (SELECT count(*) FROM jsonb_object_keys(s.config#>'{engineConfig,errorsByTextId}'))<>5)
    THEN RAISE EXCEPTION 'Proofreading needs five distinct texts per exercise'; END IF;
  IF EXISTS(SELECT 1 FROM proofreading_content c CROSS JOIN LATERAL jsonb_array_elements(c.errors) error
    WHERE (error->>'wordIndex')::int NOT BETWEEN 0 AND c.word_count-1
      OR (regexp_split_to_array(btrim(c.content), E'\\s+'))[(error->>'wordIndex')::int+1] IS DISTINCT FROM error->>'originalWord'
      OR btrim(coalesce(error->>'explanation',''))='' OR btrim(coalesce(error->>'errorWord',''))=''
      OR error->>'errorWord'=error->>'originalWord' OR error->>'errorWord' ~ '\s'
      OR error->>'errorType' NOT IN ('transposition','missing_letter','extra_letter'))
    OR EXISTS(SELECT 1 FROM proofreading_content c CROSS JOIN LATERAL jsonb_array_elements(c.errors) error
      GROUP BY c.id HAVING count(DISTINCT error->>'wordIndex')<>count(*))
    THEN RAISE EXCEPTION 'Proofreading annotations invalid'; END IF;
  IF EXISTS(SELECT 1 FROM proofreading_content c JOIN speed_reading.reading_texts t ON t.id=c.id
    WHERE t.content IS DISTINCT FROM c.content OR t.title IS DISTINCT FROM c.title OR t.word_count IS DISTINCT FROM c.word_count
      OR t.exercise_id IS DISTINCT FROM c.exercise_id OR t.target_age_group_id IS DISTINCT FROM c.age_id
      OR t.difficulty_level IS DISTINCT FROM c.level OR t.category IS DISTINCT FROM 'error-analysis'
      OR t.language IS DISTINCT FROM 'tr' OR NOT t.is_active OR t.is_deleted)
    OR EXISTS(SELECT 1 FROM proofreading_content c JOIN speed_reading.reading_questions q ON q.reading_text_id=c.id WHERE NOT q.is_deleted)
    THEN RAISE EXCEPTION 'Proofreading text collision or later edit'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.config,updated_at=now(),updated_by='error-analysis-v1',version=e.version+1
FROM proofreading_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.config;
INSERT INTO speed_reading.reading_texts(id,title,content,word_count,category,difficulty_level,target_age_group_id,
  language,is_active,tags,recommended_min_level,recommended_max_level,average_comprehension_score,times_read,
  average_reading_time_seconds,exercise_id,created_at,created_by,version,is_deleted)
SELECT id,title,content,word_count,'error-analysis',level,age_id,'tr',true,'error-analysis-v1;prepared-targets',level,level,
  0,0,0,exercise_id,now(),'error-analysis-v1',1,false FROM proofreading_content ON CONFLICT(id) DO NOTHING;
COMMIT;
