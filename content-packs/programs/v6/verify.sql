CREATE TEMP TABLE program_day_check AS
SELECT p.id,p."TargetAgeGroupConfigurationId" age_id,
 (substr(w.key,5)::int-1)*7+substr(d.key,4)::int day_number,
 s.ordinality position,s.value->>'type' type,(s.value->>'count')::int tasks,s.value->>'comprehensionMode' mode,
 CASE WHEN s.value->>'type' IN('EyeTracking','Saccade','Fixation','SchulteTable','TargetHunt') THEN 0
 WHEN s.value->>'type' IN('Tachistoscope','VisualExpansion','Chunking') THEN 1
 WHEN s.value->>'type' IN('SpeedReading','FreeReading','Scanning','Skimming','RSVP','TextFading','RegressionReduction','SubvocalizationReduction','Visualization','ErrorAnalysis') THEN 2
 WHEN s.value->>'type'='Comprehension' THEN 3 WHEN s.value->>'type'='Vocabulary' THEN 4 ELSE -1 END block
FROM speed_reading.program_templates p
CROSS JOIN LATERAL jsonb_each(p."WeeklyPatternJson") w
CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(w.value)='object' THEN w.value ELSE '{}'::jsonb END) d
CROSS JOIN LATERAL jsonb_array_elements(d.value) WITH ORDINALITY s(value,ordinality)
WHERE p."IsActive" AND NOT p."IsDeleted" AND NOT p."IsAssessment" AND w.key LIKE 'week%';
BEGIN READ ONLY;
DO $$ BEGIN
 IF (SELECT count(DISTINCT id) FROM program_day_check)<>20 OR (SELECT sum(tasks) FROM program_day_check)<>3570
 OR (SELECT count(*) FROM (SELECT DISTINCT id,day_number FROM program_day_check) days)<>560
 THEN RAISE EXCEPTION 'Unexpected program/day/task counts'; END IF;
 IF EXISTS(SELECT 1 FROM program_day_check WHERE block<0 OR (position=1 AND block<>0) OR tasks<>1 OR day_number NOT BETWEEN 1 AND 28)
 OR EXISTS(SELECT 1 FROM (SELECT block,lag(block) OVER(PARTITION BY id,day_number ORDER BY position) previous FROM program_day_check) ordered WHERE block<previous)
 OR EXISTS(SELECT 1 FROM program_day_check GROUP BY id,day_number HAVING count(*) FILTER(WHERE block=0)<>1 OR count(*) FILTER(WHERE type='Comprehension')<>1)
 OR EXISTS(SELECT 1 FROM program_day_check WHERE type='Comprehension' AND mode IS DISTINCT FROM CASE WHEN day_number%7=0 THEN 'evaluation' ELSE 'learning' END)
 OR EXISTS(SELECT 1 FROM program_day_check c GROUP BY id,age_id,day_number HAVING count(*) FILTER(WHERE type='Vocabulary')<>
   CASE WHEN age_id IN('10000000-0000-0000-0000-000000000001'::uuid,'10000000-0000-0000-0000-000000000002'::uuid) THEN 1 ELSE 0 END)
 OR EXISTS(SELECT 1 FROM program_day_check GROUP BY id,age_id,day_number HAVING sum(tasks)<>
   CASE WHEN age_id='10000000-0000-0000-0000-000000000001'::uuid OR day_number<=14 THEN 6 ELSE 7 END)
 OR EXISTS(SELECT 1 FROM program_day_check WHERE type='Focus')
 THEN RAISE EXCEPTION 'Program blocks, vocabulary, comprehension or day count invalid'; END IF;
END $$;
SELECT 'PASS: 20 programs / 560 days / 3570 tasks; warm-up → perception/chunking → reading → comprehension → vocabulary; vocabulary only Child/Teen; Child 6 a day';
COMMIT;
