BEGIN READ ONLY;
DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM speed_reading.program_templates WHERE id IN(
    'c212d2c6-ea6b-5fce-ad7d-111c1ff6b7fe','6dd7f01d-127e-5340-baa8-a485307f4d15',
    '161b34c6-29e9-57db-8ad2-d3ded5b88a3c','dcf57722-3d60-56da-9a89-af8e11fbf4f6',
    '27a1c700-f15a-5a0b-9ec9-3d1d2444eb35')) THEN RAISE EXCEPTION 'Old adult templates remain'; END IF;
  IF (SELECT count(*) FROM speed_reading.program_templates WHERE created_by='system:adult-programs-v2'
    AND "IsActive" AND NOT "IsDeleted" AND NOT "IsAssessment"
    AND "TargetAgeGroupConfigurationId"='10000000-0000-0000-0000-000000000003'
    AND "TotalDays"=28 AND "TotalWeeks"=4 AND "WeeksPerDifficultyIncrease"=0)<>5 THEN
    RAISE EXCEPTION 'Expected five active adult replacement templates';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM speed_reading.program_templates WHERE id='55ac59f6-e75b-42ee-822a-ed4acd9cdddb'
    AND "IsAssessment" AND "IsActive" AND NOT "IsDeleted") THEN
    RAISE EXCEPTION 'Adult assessment was not preserved';
  END IF;
  IF EXISTS (
    SELECT p.id FROM speed_reading.program_templates p
    CROSS JOIN LATERAL jsonb_each(p."WeeklyPatternJson") w
    CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(w.value)='object' THEN w.value ELSE '{}'::jsonb END) d
    WHERE p.created_by='system:adult-programs-v2'
    GROUP BY p.id HAVING count(*)<>28 OR sum(jsonb_array_length(d.value))<>182
      OR min(jsonb_array_length(d.value))<>6 OR max(jsonb_array_length(d.value))<>7
  ) THEN RAISE EXCEPTION 'Adult daily task counts are incorrect'; END IF;
END $$;
WITH tasks AS (
  SELECT p.id,p."Name",jsonb_array_length(d.value) AS task_count
  FROM speed_reading.program_templates p CROSS JOIN LATERAL jsonb_each(p."WeeklyPatternJson") w
  CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(w.value)='object' THEN w.value ELSE '{}'::jsonb END) d
  WHERE p.created_by='system:adult-programs-v2'
)
SELECT id,"Name",count(*) AS days,sum(task_count) AS tasks,min(task_count),max(task_count)
FROM tasks GROUP BY id,"Name" ORDER BY id;
COMMIT;
