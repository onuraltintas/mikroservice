-- Generated with generate.mjs. Only Speed Reading DB, after its existing v2 program packs.
-- Take and verify a backup before publishing; no student schedules/history are changed.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('grouping-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.reading_texts, speed_reading.exercise_types,
  speed_reading.program_templates IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE grouping_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->>'previousTitle' old_title, item->'previousConfiguration' old_config,
  item->>'title' title, item->'configuration' config
FROM jsonb_array_elements($settings${{SETTINGS}}$settings$::jsonb) item;
CREATE TEMP TABLE grouping_content ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'exerciseId')::uuid exercise_id,
  (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->>'title' title, item->>'content' content, (item->>'wordCount')::int word_count
FROM jsonb_array_elements($content${{CONTENT}}$content$::jsonb) item;
CREATE TEMP TABLE grouping_programs ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id,
  item->'previousPattern' old_pattern, item->'pattern' pattern
FROM jsonb_array_elements($programs${{PROGRAMS}}$programs$::jsonb) item;
DO $$ BEGIN
  IF (SELECT count(*) FROM grouping_settings)<>20 OR (SELECT count(DISTINCT id) FROM grouping_settings)<>20
    OR (SELECT count(*) FROM grouping_content)<>60 OR (SELECT count(DISTINCT id) FROM grouping_content)<>60
    OR (SELECT count(*) FROM grouping_programs)<>4 THEN RAISE EXCEPTION 'Grouping payload incomplete'; END IF;
  IF (SELECT count(*) FROM speed_reading.exercise_types WHERE name='Chunking' AND is_active AND NOT is_deleted)<>1
    THEN RAISE EXCEPTION 'Active Chunking type missing or ambiguous'; END IF;
  IF EXISTS(SELECT 1 FROM grouping_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE (s.level<5 AND e.id IS NULL) OR (e.id IS NOT NULL AND
      (t.name IS DISTINCT FROM 'Chunking' OR e.target_age_group_id IS DISTINCT FROM s.age_id
       OR e.difficulty_level<>s.level OR NOT e.is_active OR e.is_deleted
       OR (e.configuration_json IS DISTINCT FROM s.config AND e.configuration_json IS DISTINCT FROM s.old_config)
       OR (e.title IS DISTINCT FROM s.title AND e.title IS DISTINCT FROM s.old_title))))
    THEN RAISE EXCEPTION 'Grouping catalog changed; review before applying'; END IF;
  IF EXISTS(SELECT 1 FROM speed_reading.exercises e JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE t.name='Chunking' AND e.is_active AND NOT e.is_deleted
      AND e.target_age_group_id IN (SELECT age_id FROM grouping_settings)
      AND NOT EXISTS(SELECT 1 FROM grouping_settings s WHERE s.id=e.id))
    THEN RAISE EXCEPTION 'Unexpected age-specific Chunking entry'; END IF;
  IF EXISTS(SELECT 1 FROM grouping_content c LEFT JOIN grouping_settings s ON s.id=c.exercise_id
    WHERE s.id IS NULL OR s.age_id<>c.age_id OR s.level<>c.level
      OR cardinality(regexp_split_to_array(btrim(c.content), E'\\s+'))<>c.word_count
      OR c.word_count<(s.config->'engineConfig'->'content'->>'minWordCount')::int
      OR c.word_count>(s.config->'engineConfig'->'content'->>'maxWordCount')::int)
    THEN RAISE EXCEPTION 'Grouping content band mismatch'; END IF;
  IF EXISTS(SELECT 1 FROM grouping_settings s LEFT JOIN grouping_content c ON c.exercise_id=s.id
    GROUP BY s.id HAVING count(c.id)<>3 OR count(DISTINCT c.content)<>3)
    THEN RAISE EXCEPTION 'Grouping needs three distinct texts per age/level'; END IF;
  IF EXISTS(SELECT 1 FROM grouping_content c JOIN speed_reading.reading_texts t ON t.id=c.id
    WHERE t.title<>c.title OR t.content<>c.content OR t.word_count<>c.word_count
      OR t.exercise_id IS DISTINCT FROM c.exercise_id OR t.target_age_group_id IS DISTINCT FROM c.age_id
      OR t.difficulty_level<>c.level OR NOT t.is_active OR t.is_deleted)
    THEN RAISE EXCEPTION 'Grouping text collision or later edit'; END IF;
  IF EXISTS(SELECT 1 FROM grouping_programs g LEFT JOIN speed_reading.program_templates p ON p.id=g.id
    WHERE p.id IS NULL OR NOT p."IsActive" OR p."IsDeleted" OR p."IsAssessment"
      OR p."InitialDifficultyLevel"<>5 OR p."TotalDays"<>28 OR p."TotalWeeks"<>4
      OR p."TargetAgeGroupConfigurationId" IS DISTINCT FROM g.age_id
      OR (p."WeeklyPatternJson"::jsonb IS DISTINCT FROM g.old_pattern AND p."WeeklyPatternJson"::jsonb IS DISTINCT FROM g.pattern))
    THEN RAISE EXCEPTION 'Grouping program baseline changed'; END IF;
END $$;
INSERT INTO speed_reading.exercises(id,exercise_type_id,title,description,type_code,difficulty_level,
  configuration_json,target_age_group_id,creator_id,is_active,created_at,created_by,version,is_deleted)
SELECT s.id,t.id,s.title,'Sabit tempoyla kelime gruplarını izleme alıştırması; anlama ölçümü değildir.',
  'reading',s.level,s.config,s.age_id,e.creator_id,true,now(),'grouping-v1',1,false
FROM grouping_settings s CROSS JOIN speed_reading.exercise_types t
JOIN speed_reading.exercises e ON e.exercise_type_id=t.id AND e.difficulty_level=1
WHERE t.name='Chunking' AND e.target_age_group_id=s.age_id AND s.level=5
ON CONFLICT(id) DO NOTHING;
INSERT INTO speed_reading.reading_texts(id,title,content,word_count,category,difficulty_level,target_age_group_id,
  language,is_active,tags,recommended_min_level,recommended_max_level,average_comprehension_score,times_read,
  average_reading_time_seconds,exercise_id,created_at,created_by,version,is_deleted)
SELECT id,title,content,word_count,'grouping',level,age_id,'tr',true,'grouping-v1;original-selection',level,level,
  0,0,0,exercise_id,now(),'grouping-v1',1,false FROM grouping_content ON CONFLICT(id) DO NOTHING;
UPDATE speed_reading.exercises e SET configuration_json=s.config,title=s.title,updated_at=now(),
  updated_by='grouping-v1',version=e.version+1 FROM grouping_settings s
WHERE e.id=s.id AND (e.configuration_json IS DISTINCT FROM s.config OR e.title IS DISTINCT FROM s.title);
UPDATE speed_reading.exercise_types SET display_name='Kelime Gruplarıyla Okuma',updated_at=now(),
  updated_by='grouping-v1',version=version+1 WHERE name='Chunking' AND display_name<>'Kelime Gruplarıyla Okuma';
UPDATE speed_reading.program_templates p SET "WeeklyPatternJson"=g.pattern,
  updated_at=now(),updated_by='grouping-v1',version=p.version+1 FROM grouping_programs g
WHERE p.id=g.id AND p."WeeklyPatternJson"::jsonb IS DISTINCT FROM g.pattern;
COMMIT;
