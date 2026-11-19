-- Vocabulary age fit v1 (live test 19.11.2026, A2, decision K4).
-- Levels 4-5 of the shared vocabulary pool hold academic and professional terms (research methods, law,
-- medicine, philosophy). They move to adults: the existing rows to Yetişkin, a copy to Genç yetişkin.
-- Çocuk and Genç get their own age-appropriate level 4-5 words. Run once, as a whole; any check failing rolls back.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('vocabulary-age-fit-v1'));
LOCK TABLE speed_reading.vocabulary_items IN SHARE ROW EXCLUSIVE MODE;

CREATE TEMP TABLE age_fit_words ON COMMIT DROP AS
SELECT (w->>'id')::uuid id, w->>'word' word, w->>'definition' definition, w->>'category' category,
       (w->>'level')::int level, (w->>'ageGroupId')::uuid age_id
FROM jsonb_array_elements($words${{WORDS}}$words$::jsonb) w;

DO $$ BEGIN
  IF (SELECT count(*) FROM age_fit_words) <> {{WORD_COUNT}} THEN RAISE EXCEPTION 'Word payload incomplete'; END IF;
  IF to_regclass('speed_reading.vocabulary_age_fit_v1_backup') IS NOT NULL
    OR EXISTS (SELECT 1 FROM speed_reading.vocabulary_items WHERE created_by = 'vocabulary-age-fit-v1')
    THEN RAISE EXCEPTION 'vocabulary-age-fit-v1 is already applied'; END IF;
  IF (SELECT count(*) FROM speed_reading.age_group_configurations WHERE id IN (
      '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002',
      '10000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000004')) <> 4
    THEN RAISE EXCEPTION 'Age groups missing'; END IF;
  IF (SELECT count(*) FROM speed_reading.vocabulary_items
      WHERE NOT is_deleted AND target_age_group_id IS NULL AND difficulty_level = 4) <> {{BASELINE_LEVEL_4}}
    OR (SELECT count(*) FROM speed_reading.vocabulary_items
      WHERE NOT is_deleted AND target_age_group_id IS NULL AND difficulty_level = 5) <> {{BASELINE_LEVEL_5}}
    THEN RAISE EXCEPTION 'Vocabulary baseline changed; review before applying'; END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.vocabulary_items v JOIN age_fit_words w ON w.id = v.id)
    THEN RAISE EXCEPTION 'Word id collision'; END IF;
END $$;

-- Rollback record: the rows that leave the shared pool, with their previous values.
CREATE TABLE speed_reading.vocabulary_age_fit_v1_backup AS
SELECT id, target_age_group_id, updated_at, updated_by
FROM speed_reading.vocabulary_items
WHERE NOT is_deleted AND target_age_group_id IS NULL AND difficulty_level IN (4, 5);

INSERT INTO speed_reading.vocabulary_items (id, word, definition, example_sentence, synonyms, antonyms,
  target_age_group_id, difficulty_level, category, is_deleted, created_at, created_by, version)
SELECT md5('vocabulary-age-fit-v1:' || v.id::text)::uuid, v.word, v.definition, v.example_sentence, v.synonyms, v.antonyms,
  '10000000-0000-0000-0000-000000000004'::uuid, v.difficulty_level, v.category, FALSE, NOW(), 'vocabulary-age-fit-v1', 0
FROM speed_reading.vocabulary_items v
JOIN speed_reading.vocabulary_age_fit_v1_backup b ON b.id = v.id;

UPDATE speed_reading.vocabulary_items v
SET target_age_group_id = '10000000-0000-0000-0000-000000000003'::uuid,
    updated_at = NOW(), updated_by = 'vocabulary-age-fit-v1', version = v.version + 1
FROM speed_reading.vocabulary_age_fit_v1_backup b
WHERE b.id = v.id;

INSERT INTO speed_reading.vocabulary_items (id, word, definition, target_age_group_id, difficulty_level, category,
  is_deleted, created_at, created_by, version)
SELECT id, word, definition, age_id, level, category, FALSE, NOW(), 'vocabulary-age-fit-v1', 0
FROM age_fit_words;

-- Every age keeps a full, unambiguous pool for its level 4-5 vocabulary exercises.
DO $$ DECLARE missing text; BEGIN
  SELECT string_agg(r.age_id::text || ' L' || r.level, ', ') INTO missing
  FROM (VALUES
    ('10000000-0000-0000-0000-000000000001'::uuid, 4, 14), ('10000000-0000-0000-0000-000000000001'::uuid, 5, 16),
    ('10000000-0000-0000-0000-000000000002'::uuid, 4, 16), ('10000000-0000-0000-0000-000000000002'::uuid, 5, 18),
    ('10000000-0000-0000-0000-000000000004'::uuid, 4, 18), ('10000000-0000-0000-0000-000000000004'::uuid, 5, 20),
    ('10000000-0000-0000-0000-000000000003'::uuid, 4, 20), ('10000000-0000-0000-0000-000000000003'::uuid, 5, 24)
  ) AS r(age_id, level, needed)
  WHERE (SELECT count(*) FROM (
      SELECT v.word, v.definition FROM speed_reading.vocabulary_items v
      WHERE NOT v.is_deleted AND v.difficulty_level = r.level
        AND (v.target_age_group_id IS NULL OR v.target_age_group_id = r.age_id)) visible
    WHERE (SELECT count(*) FROM speed_reading.vocabulary_items o
           WHERE NOT o.is_deleted AND o.difficulty_level = r.level
             AND (o.target_age_group_id IS NULL OR o.target_age_group_id = r.age_id)
             AND (lower(trim(o.word)) = lower(trim(visible.word)) OR lower(trim(o.definition)) = lower(trim(visible.definition)))) = 1
  ) < r.needed;
  IF missing IS NOT NULL THEN RAISE EXCEPTION 'Vocabulary pool too small after age fit: %', missing; END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.vocabulary_items
      WHERE NOT is_deleted AND target_age_group_id IS NULL AND difficulty_level IN (4, 5))
    THEN RAISE EXCEPTION 'Shared level 4-5 words remain'; END IF;
END $$;

SELECT target_age_group_id, difficulty_level, count(*) AS words
FROM speed_reading.vocabulary_items
WHERE NOT is_deleted AND difficulty_level IN (4, 5)
GROUP BY 1, 2 ORDER BY 1, 2;
COMMIT;
