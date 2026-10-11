-- Read-only readiness report. Never changes the live database.
BEGIN READ ONLY;
WITH bands(age, age_id, level, low, high) AS (VALUES
 ('child',1,2,126,154),('child',1,3,168,206),('child',1,4,216,264),('child',1,5,270,330),
 ('teen',2,1,144,176),('teen',2,2,213,262),('teen',2,3,297,363),('teen',2,4,409,501),('teen',2,5,522,638),
 ('young-adult',4,1,162,198),('young-adult',4,2,236,289),('young-adult',4,3,337,413),('young-adult',4,4,456,559),('young-adult',4,5,576,704),
 ('adult',3,1,162,198),('adult',3,2,247,303),('adult',3,3,364,446),('adult',3,4,488,597),('adult',3,5,612,748))
SELECT jsonb_agg(report ORDER BY report->>'age', (report->>'level')::int) FROM (
 SELECT jsonb_build_object('age',b.age,'level',b.level,'minimum',b.low,'maximum',b.high,
 'eligible',count(t.id),'titles',coalesce(jsonb_agg(t.title) FILTER (WHERE t.id IS NOT NULL),'[]'::jsonb)) report
 FROM bands b LEFT JOIN speed_reading.reading_texts t ON t.is_active AND NOT t.is_deleted
 AND t.exercise_id IS NULL AND t.difficulty_level=b.level
 AND (t.target_age_group_id IS NULL OR t.target_age_group_id=('10000000-0000-0000-0000-00000000000'||b.age_id)::uuid)
 AND cardinality(regexp_split_to_array(btrim(t.content), E'\\s+')) BETWEEN b.low AND b.high
 AND (SELECT count(*) FROM speed_reading.reading_questions q WHERE q.reading_text_id=t.id AND NOT q.is_deleted
   AND q.type BETWEEN 1 AND 3 AND q.bloom_level BETWEEN 1 AND 6
   AND upper(btrim(q.correct_answer)) IN ('A','B','C','D')
   AND btrim(q.option_a)<>'' AND btrim(q.option_b)<>'' AND btrim(q.option_c)<>'' AND btrim(q.option_d)<>'')>=5
 GROUP BY b.age,b.level,b.low,b.high) rows;
ROLLBACK;
