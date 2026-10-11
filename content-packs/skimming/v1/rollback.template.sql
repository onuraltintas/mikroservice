-- Restore settings and archive ONLY this package's texts. Never delete results or questions.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='120s';
SELECT pg_advisory_xact_lock(hashtext('skimming-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.reading_texts, speed_reading.reading_questions IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE skimming_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->'previousConfiguration' old_config, item->'configuration' config
FROM jsonb_array_elements($settings${{SETTINGS}}$settings$::jsonb) item;
CREATE TEMP TABLE skimming_content ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'exerciseId')::uuid exercise_id, (item->>'ageGroupId')::uuid age_id,
  (item->>'level')::int level, item->>'title' title, item->>'content' content,
  (item->>'wordCount')::int word_count, item->'questions' questions
FROM jsonb_array_elements($content${{CONTENT}}$content$::jsonb) item;
DO $$ BEGIN
  IF (SELECT count(*) FROM skimming_settings)<>12 OR (SELECT count(*) FROM skimming_content)<>36
    THEN RAISE EXCEPTION 'Skimming rollback payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    WHERE e.id IS NULL OR e.target_age_group_id IS DISTINCT FROM s.age_id OR e.difficulty_level IS DISTINCT FROM s.level
      OR (e.configuration_json IS DISTINCT FROM s.config AND e.configuration_json IS DISTINCT FROM s.old_config))
    THEN RAISE EXCEPTION 'Skimming rollback catalog changed'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_content c LEFT JOIN speed_reading.reading_texts t ON t.id=c.id
    WHERE t.id IS NULL OR t.title IS DISTINCT FROM c.title OR t.content IS DISTINCT FROM c.content
      OR t.word_count IS DISTINCT FROM c.word_count OR t.target_age_group_id IS DISTINCT FROM c.age_id
      OR t.exercise_id IS DISTINCT FROM c.exercise_id OR t.difficulty_level IS DISTINCT FROM c.level
      OR t.category IS DISTINCT FROM 'skimming' OR t.language IS DISTINCT FROM 'tr' OR t.is_deleted)
    THEN RAISE EXCEPTION 'Skimming rollback content changed'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_content c CROSS JOIN LATERAL jsonb_array_elements(c.questions) q
    LEFT JOIN speed_reading.reading_questions r ON r.id=(q->>'id')::uuid
    WHERE r.id IS NULL OR r.reading_text_id IS DISTINCT FROM c.id OR r.question_text IS DISTINCT FROM q->>'questionText'
      OR r.correct_answer IS DISTINCT FROM q->>'correctAnswer' OR r.option_a IS DISTINCT FROM q->>'optionA'
      OR r.option_b IS DISTINCT FROM q->>'optionB' OR r.option_c IS DISTINCT FROM q->>'optionC'
      OR r.option_d IS DISTINCT FROM q->>'optionD' OR r.explanation IS DISTINCT FROM q->>'explanation'
      OR r.type IS DISTINCT FROM (q->>'type')::int OR r.bloom_level IS DISTINCT FROM (q->>'bloomLevel')::int
      OR r.order_index IS DISTINCT FROM (q->>'orderIndex')::int OR r.difficulty_level IS DISTINCT FROM c.level OR r.is_deleted)
    THEN RAISE EXCEPTION 'Skimming rollback questions changed'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.old_config,updated_at=now(),updated_by='skimming-v1-rollback',version=e.version+1
FROM skimming_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.old_config;
UPDATE speed_reading.reading_texts t SET is_active=false,updated_at=now(),updated_by='skimming-v1-rollback',version=t.version+1
FROM skimming_content c WHERE t.id=c.id AND t.is_active;
COMMIT;
