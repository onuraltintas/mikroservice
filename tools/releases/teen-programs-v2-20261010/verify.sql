BEGIN READ ONLY;
DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM speed_reading.program_templates WHERE id IN(
    '8d3f8827-c0ff-5b91-99cf-d01a192b9bbb','4aa056b5-111f-5832-ac16-0f12466d00e6',
    '79c34e28-4e0d-538e-8b8f-a29d79906af7','eef1ac8f-cd05-5e3e-b8f1-355b3da5d4a2',
    '7a3cf6e3-c0fb-5e07-b539-ac05c92e4f15')) THEN RAISE EXCEPTION 'Old teen templates remain'; END IF;
  IF (SELECT count(*) FROM speed_reading.program_templates WHERE created_by='system:teen-programs-v2'
    AND "IsActive" AND NOT "IsDeleted" AND NOT "IsAssessment"
    AND "TargetAgeGroupConfigurationId"='10000000-0000-0000-0000-000000000002'
    AND "TotalDays"=28 AND "TotalWeeks"=4 AND "WeeksPerDifficultyIncrease"=0)<>5 THEN
    RAISE EXCEPTION 'Expected five active teen replacement templates';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM speed_reading.program_templates WHERE id='990738b8-d111-4f39-82ff-156c42dd7dae'
    AND "IsAssessment" AND "IsActive" AND NOT "IsDeleted") THEN
    RAISE EXCEPTION 'Teen assessment was not preserved';
  END IF;
  IF EXISTS (
    SELECT p.id FROM speed_reading.program_templates p
    CROSS JOIN LATERAL jsonb_each(p."WeeklyPatternJson") w
    CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(w.value)='object' THEN w.value ELSE '{}'::jsonb END) d
    WHERE p.created_by='system:teen-programs-v2'
    GROUP BY p.id HAVING count(*)<>28 OR sum(jsonb_array_length(d.value))<>182
      OR min(jsonb_array_length(d.value))<>6 OR max(jsonb_array_length(d.value))<>7
  ) THEN RAISE EXCEPTION 'Teen daily task counts are incorrect'; END IF;
END $$;
WITH tasks AS (
  SELECT p.id,p."Name",jsonb_array_length(d.value) AS task_count
  FROM speed_reading.program_templates p CROSS JOIN LATERAL jsonb_each(p."WeeklyPatternJson") w
  CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(w.value)='object' THEN w.value ELSE '{}'::jsonb END) d
  WHERE p.created_by='system:teen-programs-v2'
)
SELECT id,"Name",count(*) AS days,sum(task_count) AS tasks,min(task_count),max(task_count)
FROM tasks GROUP BY id,"Name" ORDER BY id;
COMMIT;
