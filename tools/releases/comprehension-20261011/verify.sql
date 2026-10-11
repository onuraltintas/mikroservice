BEGIN READ ONLY;
DO $$ BEGIN
  IF (SELECT count(*) FROM speed_reading.reading_texts WHERE created_by='comprehension-v1' AND is_active AND NOT is_deleted)<>80
    OR (SELECT count(*) FROM speed_reading.reading_questions WHERE created_by='comprehension-v1' AND NOT is_deleted)<>588
    OR (SELECT count(*) FROM speed_reading.exercises WHERE updated_by='comprehension-v1' AND is_active AND NOT is_deleted)<>40
    THEN RAISE EXCEPTION 'Comprehension live counts mismatch'; END IF;
  IF (SELECT count(*) FROM (
    SELECT e.id FROM speed_reading.exercises e
    JOIN speed_reading.reading_texts t ON t.exercise_id=e.id AND t.created_by='comprehension-v1'
    WHERE t.is_active AND NOT t.is_deleted
      AND t.target_age_group_id=e.target_age_group_id AND t.difficulty_level=e.difficulty_level
      AND t.word_count BETWEEN (e.configuration_json->'engineConfig'->'content'->>'minWordCount')::int
        AND (e.configuration_json->'engineConfig'->'content'->>'maxWordCount')::int
      AND (SELECT count(*) FROM speed_reading.reading_questions q WHERE q.reading_text_id=t.id AND NOT q.is_deleted)
        = (e.configuration_json->'engineConfig'->'content'->>'questionCount')::int
    GROUP BY e.id HAVING count(*)=2
  ) ready)<>40 THEN RAISE EXCEPTION 'Comprehension live pools incomplete'; END IF;
END $$;
SELECT count(*) AS normal_modes FROM speed_reading.exercises WHERE updated_by='comprehension-v1';
SELECT count(*) AS new_text_records, count(DISTINCT content) AS distinct_bodies
FROM speed_reading.reading_texts WHERE created_by='comprehension-v1';
SELECT count(*) AS new_question_records FROM speed_reading.reading_questions WHERE created_by='comprehension-v1';
SELECT target_age_group_id,difficulty_level,configuration_json->'engineConfig'->>'comprehensionMode' AS mode,
  configuration_json->'engineConfig'->'content' AS content_limits
FROM speed_reading.exercises WHERE updated_by='comprehension-v1' ORDER BY target_age_group_id,difficulty_level,mode;
COMMIT;
