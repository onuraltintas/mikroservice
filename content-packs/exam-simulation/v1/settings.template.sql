-- Speed Reading DB only. Back up and quiesce the API before a controlled release.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='120s';
SELECT pg_advisory_xact_lock(hashtext('exam-simulation-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types, speed_reading.reading_texts, speed_reading.reading_questions IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE exam_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->'previousConfiguration' old_config,item->'configuration' config
FROM jsonb_array_elements($settings${{SETTINGS}}$settings$::jsonb) item;
DO $$ BEGIN
  IF (SELECT count(*) FROM exam_settings)<>3 OR (SELECT count(DISTINCT id) FROM exam_settings)<>3
    OR (SELECT count(DISTINCT age_id) FROM exam_settings)<>3
    OR EXISTS(SELECT 1 FROM exam_settings WHERE level IS DISTINCT FROM 5
      OR config->>'engineType' IS DISTINCT FROM 'exam_simulation'
      OR config#>>'{engineConfig,content,questionCount}' IS DISTINCT FROM '8')
    THEN RAISE EXCEPTION 'Exam payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM exam_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE e.id IS NULL OR t.name IS DISTINCT FROM 'ExamSimulation' OR t.engine_type IS DISTINCT FROM 'exam_simulation'
      OR NOT t.is_active OR t.is_deleted OR NOT e.is_active OR e.is_deleted
      OR e.target_age_group_id IS DISTINCT FROM s.age_id OR e.difficulty_level IS DISTINCT FROM s.level
      OR (e.configuration_json IS DISTINCT FROM s.config AND e.configuration_json IS DISTINCT FROM s.old_config))
    THEN RAISE EXCEPTION 'Exam baseline changed; review before applying'; END IF;
  IF EXISTS(SELECT 1 FROM exam_settings s WHERE (SELECT count(*) FROM speed_reading.reading_texts r
    WHERE r.is_active AND NOT r.is_deleted AND length(trim(r.content))>0
      AND r.difficulty_level=s.level AND (r.target_age_group_id IS NULL OR r.target_age_group_id=s.age_id)
      AND (r.exercise_id IS NULL OR r.exercise_id=s.id)
      AND r.word_count BETWEEN (s.{{TARGET}}#>>'{engineConfig,content,minWordCount}')::int
        AND (s.{{TARGET}}#>>'{engineConfig,content,maxWordCount}')::int
      AND (SELECT count(*) FROM speed_reading.reading_questions q WHERE q.reading_text_id=r.id AND NOT q.is_deleted
        AND upper(trim(q.correct_answer)) IN('A','B','C','D') AND q.type BETWEEN 1 AND 3 AND q.bloom_level BETWEEN 1 AND 6
        AND length(trim(q.option_a))>0 AND length(trim(q.option_b))>0 AND length(trim(q.option_c))>0 AND length(trim(q.option_d))>0)>=8)<3
      AND '{{TARGET}}'='config')
    THEN RAISE EXCEPTION 'Exam requires three eligible texts per age'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.{{TARGET}},updated_at=now(),updated_by='exam-simulation-v1',version=e.version+1
FROM exam_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.{{TARGET}};
COMMIT;
