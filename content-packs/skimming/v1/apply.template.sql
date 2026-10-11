-- Speed Reading DB only. Back up and quiesce the API before a controlled release.
-- Preserve existing content, programs, results and open-session snapshots.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('skimming-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types, speed_reading.reading_texts,
  speed_reading.reading_questions IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE skimming_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->'previousConfiguration' old_config, item->'configuration' config
FROM jsonb_array_elements($settings${{SETTINGS}}$settings$::jsonb) item;
CREATE TEMP TABLE skimming_content ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'exerciseId')::uuid exercise_id,
  (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->>'title' title, item->>'content' content, (item->>'wordCount')::int word_count, item->'questions' questions
FROM jsonb_array_elements($content${{CONTENT}}$content$::jsonb) item;
CREATE TEMP TABLE skimming_questions ON COMMIT DROP AS
SELECT (q->>'id')::uuid id, c.id text_id, q->>'questionText' question_text, q->>'correctAnswer' answer,
  q->>'optionA' a, q->>'optionB' b, q->>'optionC' c, q->>'optionD' d,
  q->>'explanation' explanation, q->>'evidence' evidence, (q->>'type')::int type,
  (q->>'bloomLevel')::int bloom, (q->>'orderIndex')::int position, c.level
FROM skimming_content c CROSS JOIN LATERAL jsonb_array_elements(c.questions) q;
DO $$ BEGIN
  IF (SELECT count(*) FROM skimming_settings)<>12 OR (SELECT count(DISTINCT id) FROM skimming_settings)<>12
    OR (SELECT count(*) FROM skimming_content)<>36 OR (SELECT count(DISTINCT id) FROM skimming_content)<>36
    OR (SELECT count(*) FROM skimming_questions)<>144 OR (SELECT count(DISTINCT id) FROM skimming_questions)<>144
    THEN RAISE EXCEPTION 'Skimming payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE e.id IS NULL OR t.name IS DISTINCT FROM 'Skimming' OR t.engine_type IS DISTINCT FROM 'skimming'
      OR NOT t.is_active OR t.is_deleted OR NOT e.is_active OR e.is_deleted
      OR e.target_age_group_id IS DISTINCT FROM s.age_id OR e.difficulty_level IS DISTINCT FROM s.level
      OR (e.configuration_json IS DISTINCT FROM s.config AND e.configuration_json IS DISTINCT FROM s.old_config))
    THEN RAISE EXCEPTION 'Skimming catalog baseline changed'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_settings s WHERE s.age_id IS NULL OR s.level NOT BETWEEN 3 AND 5
    OR s.old_config IS NULL OR s.config->>'engineType' IS DISTINCT FROM 'skimming'
    OR (s.config->>'difficultyLevel')::int IS DISTINCT FROM s.level
    OR s.config->'engineConfig'->>'readingPurpose' IS DISTINCT FROM 'evaluation'
    OR s.config->'engineConfig'->'content'->>'source' IS DISTINCT FROM 'reading_text'
    OR (s.config->'engineConfig'->'content'->>'questionCount')::int IS DISTINCT FROM 4
    OR (s.config->'engineConfig'->'content'->>'minWordCount')::int IS NULL
    OR (s.config->'engineConfig'->'content'->>'maxWordCount')::int IS NULL
    OR (s.config->'engineConfig'->'content'->>'minWordCount')::int < 1
    OR (s.config->'engineConfig'->'content'->>'maxWordCount')::int < (s.config->'engineConfig'->'content'->>'minWordCount')::int
    OR (s.config->'engineConfig'->'timing'->>'minReadingTimeMs')::int IS NULL
    OR (s.config->'engineConfig'->'timing'->>'maxReadingTimeMs')::int IS NULL
    OR (s.config->'engineConfig'->'timing'->>'minReadingTimeMs')::int < 10000
    OR (s.config->'engineConfig'->'timing'->>'maxReadingTimeMs')::int <= (s.config->'engineConfig'->'timing'->>'minReadingTimeMs')::int)
    THEN RAISE EXCEPTION 'Skimming settings invalid'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_settings GROUP BY age_id,level HAVING count(*)<>1)
    THEN RAISE EXCEPTION 'Skimming duplicated age/level'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_content c LEFT JOIN skimming_settings s ON s.id=c.exercise_id
    WHERE s.id IS NULL OR c.age_id IS DISTINCT FROM s.age_id OR c.level IS DISTINCT FROM s.level
      OR c.content IS NULL OR btrim(c.content)='' OR c.title IS NULL OR btrim(c.title)=''
      OR cardinality(regexp_split_to_array(btrim(c.content), E'\\s+')) IS DISTINCT FROM c.word_count
      OR c.word_count NOT BETWEEN (s.config->'engineConfig'->'content'->>'minWordCount')::int
        AND (s.config->'engineConfig'->'content'->>'maxWordCount')::int
      OR jsonb_array_length(c.questions) IS DISTINCT FROM 4)
    THEN RAISE EXCEPTION 'Skimming content band/question mismatch'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_settings s LEFT JOIN skimming_content c ON c.exercise_id=s.id
    GROUP BY s.id HAVING count(c.id)<>3 OR count(DISTINCT c.content)<>3)
    THEN RAISE EXCEPTION 'Skimming needs three distinct selections per exercise'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_questions q JOIN skimming_content t ON t.id=q.text_id
    WHERE q.type IS DISTINCT FROM 1 OR q.bloom IS NULL OR q.bloom NOT BETWEEN 1 AND 6
      OR q.answer IS NULL OR q.answer NOT IN ('A','B','C','D') OR q.question_text IS NULL OR btrim(q.question_text)=''
      OR q.a IS NULL OR q.b IS NULL OR q.c IS NULL OR q.d IS NULL
      OR btrim(q.a)='' OR btrim(q.b)='' OR btrim(q.c)='' OR btrim(q.d)=''
      OR q.a=q.b OR q.a=q.c OR q.a=q.d OR q.b=q.c OR q.b=q.d OR q.c=q.d
      OR q.explanation IS NULL OR btrim(q.explanation)='' OR q.evidence IS NULL OR btrim(q.evidence)=''
      OR strpos(t.content,q.evidence)=0 OR q.position IS NULL OR q.position NOT BETWEEN 0 AND 3)
    THEN RAISE EXCEPTION 'Skimming question evidence/options invalid'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_questions GROUP BY text_id
    HAVING count(DISTINCT position)<>4 OR count(DISTINCT question_text)<>4)
    THEN RAISE EXCEPTION 'Skimming question order/uniqueness invalid'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_content c JOIN speed_reading.reading_texts t ON t.id=c.id
    WHERE t.title IS DISTINCT FROM c.title OR t.content IS DISTINCT FROM c.content OR t.word_count IS DISTINCT FROM c.word_count
      OR t.exercise_id IS DISTINCT FROM c.exercise_id OR t.target_age_group_id IS DISTINCT FROM c.age_id
      OR t.difficulty_level IS DISTINCT FROM c.level OR t.language IS DISTINCT FROM 'tr'
      OR t.category IS DISTINCT FROM 'skimming' OR NOT t.is_active OR t.is_deleted)
    THEN RAISE EXCEPTION 'Skimming text collision or later edit'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_questions q JOIN speed_reading.reading_questions r ON r.id=q.id
    WHERE r.reading_text_id IS DISTINCT FROM q.text_id OR r.question_text IS DISTINCT FROM q.question_text
      OR r.correct_answer IS DISTINCT FROM q.answer OR r.option_a IS DISTINCT FROM q.a OR r.option_b IS DISTINCT FROM q.b
      OR r.option_c IS DISTINCT FROM q.c OR r.option_d IS DISTINCT FROM q.d OR r.explanation IS DISTINCT FROM q.explanation
      OR r.type IS DISTINCT FROM q.type OR r.bloom_level IS DISTINCT FROM q.bloom
      OR r.difficulty_level IS DISTINCT FROM q.level OR r.order_index IS DISTINCT FROM q.position OR r.is_deleted)
    THEN RAISE EXCEPTION 'Skimming question collision or later edit'; END IF;
END $$;
INSERT INTO speed_reading.reading_texts(id,title,content,word_count,category,difficulty_level,target_age_group_id,
  language,is_active,tags,recommended_min_level,recommended_max_level,average_comprehension_score,times_read,
  average_reading_time_seconds,exercise_id,created_at,created_by,version,is_deleted)
SELECT id,title,content,word_count,'skimming',level,age_id,'tr',true,'skimming-v1;original-selection',level,level,
  0,0,0,exercise_id,now(),'skimming-v1',1,false FROM skimming_content ON CONFLICT(id) DO NOTHING;
INSERT INTO speed_reading.reading_questions(id,reading_text_id,question_text,type,bloom_level,difficulty_level,
  explanation,option_a,option_b,option_c,option_d,correct_answer,order_index,created_at,created_by,is_deleted)
SELECT id,text_id,question_text,type,bloom,level,explanation,a,b,c,d,answer,position,now(),'skimming-v1',false
FROM skimming_questions ON CONFLICT(id) DO NOTHING;
UPDATE speed_reading.exercises e SET configuration_json=s.config,updated_at=now(),updated_by='skimming-v1',version=e.version+1
FROM skimming_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.config;
DO $$ BEGIN
  IF (SELECT count(*) FROM skimming_settings s JOIN speed_reading.exercises e ON e.id=s.id
    WHERE e.configuration_json=s.config AND e.is_active AND NOT e.is_deleted)<>12
    OR (SELECT count(*) FROM skimming_content c JOIN speed_reading.reading_texts t ON t.id=c.id)<>36
    OR (SELECT count(*) FROM skimming_questions q JOIN speed_reading.reading_questions r ON r.id=q.id)<>144
    THEN RAISE EXCEPTION 'Skimming final verification failed'; END IF;
END $$;
COMMIT;
