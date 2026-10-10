BEGIN READ ONLY;
DO $$ DECLARE
  old_ids uuid[] := ARRAY['76bc9498-558d-55c9-8319-91a12cb13bdd','02c04d07-0489-5458-a504-ac4e3e747395',
    '1f884bac-73e4-5aa8-b34a-6c431c47a707','d1b72e96-f8d6-5bdb-a61d-d30f9a0edf99',
    '00f77937-72fe-520b-b509-88f1e4dfd5cc']::uuid[];
  sessions uuid[] := ARRAY['5607298b-63f4-487d-8a3c-c48986635c0f','b323708d-bc7c-4c06-ac50-37023a0282ba',
    'fc2fc9fd-90b6-4774-9f2c-e7dce2fb9901']::uuid[];
BEGIN
  IF NOT EXISTS (SELECT 1 FROM speed_reading.__ef_migrations_history
    WHERE "MigrationId"='20261010064029_AddDailyTaskSlotOrder') THEN
    RAISE EXCEPTION 'Task slot migration is required';
  END IF;
  IF (SELECT count(*) FROM speed_reading.program_templates p
    WHERE p.id=ANY(old_ids) AND p."IsActive" AND NOT p."IsDeleted" AND NOT p."IsAssessment"
      AND p."TargetAgeGroupConfigurationId"='10000000-0000-0000-0000-000000000004'
      AND p."TotalDays"=28)<>5 THEN RAISE EXCEPTION 'Expected five old young adult templates'; END IF;
  IF (SELECT count(*) FROM speed_reading.student_program_progress p WHERE p."ProgramTemplateId"=ANY(old_ids))<>1
    OR NOT EXISTS (SELECT 1 FROM speed_reading.student_program_progress WHERE id='8c7166e8-cc77-40b7-a96f-e8e5d891e4e4'
      AND "UserId"='08ea667c-2c01-4679-87bb-dbd966f57f95'
      AND "ProgramTemplateId"='76bc9498-558d-55c9-8319-91a12cb13bdd'
      AND "DaysCompleted"=1 AND "ExercisesCompleted"=3) THEN
    RAISE EXCEPTION 'Approved test progress changed; review before deletion';
  END IF;
  IF (SELECT count(*) FROM speed_reading.daily_exercise_logs WHERE "StudentProgramProgressId"='8c7166e8-cc77-40b7-a96f-e8e5d891e4e4')<>3
    OR (SELECT count(*) FROM speed_reading.daily_exercise_logs l
      WHERE l.session_id=ANY(sessions) AND l."StudentProgramProgressId"='8c7166e8-cc77-40b7-a96f-e8e5d891e4e4')<>3 THEN
    RAISE EXCEPTION 'Approved three logged sessions changed';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.review_items r WHERE r.program_template_id=ANY(old_ids))
    OR EXISTS (SELECT 1 FROM speed_reading.assessment_attempts WHERE program_progress_id='8c7166e8-cc77-40b7-a96f-e8e5d891e4e4') THEN
    RAISE EXCEPTION 'Unexpected review or assessment reference';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.program_templates WHERE created_by='system:young-adult-programs-v2'
    OR id IN('e2c01004-8d9a-4e6c-a428-000000000001','e2c01004-8d9a-4e6c-a428-000000000002',
      'e2c01004-8d9a-4e6c-a428-000000000003','e2c01004-8d9a-4e6c-a428-000000000004','e2c01004-8d9a-4e6c-a428-000000000005')) THEN
    RAISE EXCEPTION 'Initial publication identifiers already exist';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM speed_reading.program_templates WHERE id='654e55eb-de3a-4d82-933b-c1e522dba1b1'
    AND "IsAssessment" AND "IsActive" AND NOT "IsDeleted") THEN RAISE EXCEPTION 'Expected preserved assessment'; END IF;
END $$;
COMMIT;
