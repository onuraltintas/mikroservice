DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM speed_reading.__ef_migrations_history WHERE "MigrationId"='20261010064029_AddDailyTaskSlotOrder') THEN
    RAISE EXCEPTION 'Task migration was not applied';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.program_templates WHERE id IN(
    '8e6b102d-5c6d-5247-916d-d67ab3f7719e','aac00b94-c237-56d0-89fb-30548e953ae1',
    'bef6047b-2ac5-5162-8967-8aaed9c49893','b89ab8e0-fa03-5ab0-ba8a-7085aec613df',
    '5619aefe-ac50-5afc-9f55-a422899c6a80')) THEN RAISE EXCEPTION 'Old child templates remain'; END IF;
  IF (SELECT count(*) FROM speed_reading.program_templates WHERE created_by='system:child-programs-v2' AND "IsActive" AND NOT "IsDeleted")<>5 THEN
    RAISE EXCEPTION 'Expected five active replacement programs';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM speed_reading.program_templates WHERE id='1d65530e-13c2-4f09-a6cf-b717009fd959' AND "IsAssessment" AND "IsActive") THEN
    RAISE EXCEPTION 'Child assessment was not preserved';
  END IF;
END $$;
WITH tasks AS (
  SELECT p.id,p."Name",w.key AS week,d.key AS day,jsonb_array_length(d.value) AS task_count
  FROM speed_reading.program_templates p
  CROSS JOIN LATERAL jsonb_each(p."WeeklyPatternJson") w
  CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(w.value)='object' THEN w.value ELSE '{}'::jsonb END) d
  WHERE p.created_by='system:child-programs-v2'
)
SELECT id,"Name",count(*) AS days,sum(task_count) AS tasks,min(task_count),max(task_count)
FROM tasks GROUP BY id,"Name" ORDER BY id;
