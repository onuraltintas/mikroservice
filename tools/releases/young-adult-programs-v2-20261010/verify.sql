BEGIN READ ONLY;
DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM speed_reading.program_templates WHERE id IN(
    '76bc9498-558d-55c9-8319-91a12cb13bdd','02c04d07-0489-5458-a504-ac4e3e747395',
    '1f884bac-73e4-5aa8-b34a-6c431c47a707','d1b72e96-f8d6-5bdb-a61d-d30f9a0edf99','00f77937-72fe-520b-b509-88f1e4dfd5cc')) THEN
    RAISE EXCEPTION 'Old templates remain';
  END IF;
  IF (SELECT count(*) FROM speed_reading.program_templates WHERE created_by='system:young-adult-programs-v2'
    AND "IsActive" AND NOT "IsDeleted" AND NOT "IsAssessment" AND "TotalDays"=28 AND "TotalWeeks"=4
    AND "WeeksPerDifficultyIncrease"=0 AND "TargetAgeGroupConfigurationId"='10000000-0000-0000-0000-000000000004')<>5 THEN
    RAISE EXCEPTION 'Expected five new young adult plans';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.student_program_progress WHERE id='8c7166e8-cc77-40b7-a96f-e8e5d891e4e4')
    OR EXISTS (SELECT 1 FROM speed_reading.daily_exercise_logs WHERE "StudentProgramProgressId"='8c7166e8-cc77-40b7-a96f-e8e5d891e4e4')
    OR EXISTS (SELECT 1 FROM speed_reading.exercise_sessions WHERE id IN('5607298b-63f4-487d-8a3c-c48986635c0f','b323708d-bc7c-4c06-ac50-37023a0282ba','fc2fc9fd-90b6-4774-9f2c-e7dce2fb9901'))
    OR EXISTS (SELECT 1 FROM speed_reading.exercise_session_answers WHERE session_id IN('5607298b-63f4-487d-8a3c-c48986635c0f','b323708d-bc7c-4c06-ac50-37023a0282ba','fc2fc9fd-90b6-4774-9f2c-e7dce2fb9901'))
    OR EXISTS (SELECT 1 FROM speed_reading.exercise_session_results WHERE session_id IN('5607298b-63f4-487d-8a3c-c48986635c0f','b323708d-bc7c-4c06-ac50-37023a0282ba','fc2fc9fd-90b6-4774-9f2c-e7dce2fb9901')) THEN
    RAISE EXCEPTION 'Approved test records remain';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM speed_reading.program_templates WHERE id='654e55eb-de3a-4d82-933b-c1e522dba1b1'
    AND "IsAssessment" AND "IsActive" AND NOT "IsDeleted") THEN RAISE EXCEPTION 'Assessment not preserved'; END IF;
  IF EXISTS (SELECT p.id FROM speed_reading.program_templates p
    CROSS JOIN LATERAL jsonb_each(p."WeeklyPatternJson") w
    CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(w.value)='object' THEN w.value ELSE '{}'::jsonb END) d
    WHERE p.created_by='system:young-adult-programs-v2'
    GROUP BY p.id HAVING count(*)<>28 OR sum(jsonb_array_length(d.value))<>182
      OR min(jsonb_array_length(d.value))<>6 OR max(jsonb_array_length(d.value))<>7) THEN
    RAISE EXCEPTION 'Incorrect daily task counts';
  END IF;
END $$;
SELECT p.id,p."Name",count(*) AS days,sum(jsonb_array_length(d.value)) AS tasks
FROM speed_reading.program_templates p CROSS JOIN LATERAL jsonb_each(p."WeeklyPatternJson") w
CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(w.value)='object' THEN w.value ELSE '{}'::jsonb END) d
WHERE p.created_by='system:young-adult-programs-v2' GROUP BY p.id,p."Name" ORDER BY p.id;
COMMIT;
