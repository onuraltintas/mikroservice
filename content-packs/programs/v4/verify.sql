CREATE TEMP TABLE program_day_check AS
SELECT p.id,p."TargetAgeGroupConfigurationId" age_id,w.key week_label,d.key day_label,
 (substr(w.key,5)::int-1)*7+substr(d.key,4)::int day_number,
 s.ordinality position,s.value->>'type' type,(s.value->>'count')::int tasks,
 CASE WHEN s.value->>'type' IN('EyeTracking','Saccade','Fixation','VisualExpansion','Tachistoscope','SchulteTable') THEN 0
 WHEN s.value->>'type' IN('Chunking','SpeedReading','Scanning','Comprehension','FreeReading','TextFading','RSVP','Skimming','RegressionReduction','SubvocalizationReduction','Visualization','ErrorAnalysis','Vocabulary') THEN 1
 WHEN s.value->>'type'='Focus' THEN 2 ELSE -1 END stage
FROM speed_reading.program_templates p
CROSS JOIN LATERAL jsonb_each(p."WeeklyPatternJson") w
CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(w.value)='object' THEN w.value ELSE '{}'::jsonb END) d
CROSS JOIN LATERAL jsonb_array_elements(d.value) WITH ORDINALITY s(value,ordinality)
WHERE p."IsActive" AND NOT p."IsDeleted" AND NOT p."IsAssessment" AND w.key LIKE 'week%';
BEGIN READ ONLY;
DO $$ BEGIN
 IF (SELECT count(DISTINCT id) FROM program_day_check)<>20 OR (SELECT sum(tasks) FROM program_day_check)<>3640
 OR (SELECT count(*) FROM (SELECT DISTINCT id,day_number FROM program_day_check) days)<>560
 THEN RAISE EXCEPTION 'Unexpected program/day/task counts'; END IF;
 IF EXISTS(SELECT 1 FROM program_day_check WHERE stage<0 OR (position=1 AND stage<>0) OR tasks<1 OR day_number NOT BETWEEN 1 AND 28)
 OR EXISTS(SELECT 1 FROM (SELECT stage,lag(stage) OVER(PARTITION BY id,day_number ORDER BY position) previous FROM program_day_check) ordered WHERE stage<previous)
 OR EXISTS(SELECT 1 FROM program_day_check WHERE type='Vocabulary' AND age_id NOT IN('10000000-0000-0000-0000-000000000001'::uuid,'10000000-0000-0000-0000-000000000002'::uuid))
 OR EXISTS(SELECT 1 FROM program_day_check GROUP BY id,day_number HAVING sum(tasks)<>CASE WHEN day_number<=14 THEN 6 ELSE 7 END)
 THEN RAISE EXCEPTION 'Program ordering, vocabulary or day count invalid'; END IF;
END $$;
SELECT 'PASS: 20 programs / 560 days / 3640 tasks; visual first, reading then attention; vocabulary only Child/Teen';
COMMIT;
