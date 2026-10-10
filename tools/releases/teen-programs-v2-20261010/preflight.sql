BEGIN READ ONLY;
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM speed_reading.__ef_migrations_history
    WHERE "MigrationId"='20261010064029_AddDailyTaskSlotOrder') THEN
    RAISE EXCEPTION 'Expected deployed task-slot migration';
  END IF;
  IF (SELECT count(*) FROM speed_reading.program_templates WHERE id IN(
    '8d3f8827-c0ff-5b91-99cf-d01a192b9bbb','4aa056b5-111f-5832-ac16-0f12466d00e6',
    '79c34e28-4e0d-538e-8b8f-a29d79906af7','eef1ac8f-cd05-5e3e-b8f1-355b3da5d4a2',
    '7a3cf6e3-c0fb-5e07-b539-ac05c92e4f15') AND "IsActive" AND NOT "IsDeleted"
    AND NOT "IsAssessment" AND "TargetAgeGroupConfigurationId"='10000000-0000-0000-0000-000000000002')<>5 THEN
    RAISE EXCEPTION 'Expected five old active teen templates';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.student_program_progress WHERE "ProgramTemplateId" IN(
    '8d3f8827-c0ff-5b91-99cf-d01a192b9bbb','4aa056b5-111f-5832-ac16-0f12466d00e6',
    '79c34e28-4e0d-538e-8b8f-a29d79906af7','eef1ac8f-cd05-5e3e-b8f1-355b3da5d4a2',
    '7a3cf6e3-c0fb-5e07-b539-ac05c92e4f15')) THEN
    RAISE EXCEPTION 'Old teen templates have history: review before replacement';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.review_items WHERE program_template_id IN(
    '8d3f8827-c0ff-5b91-99cf-d01a192b9bbb','4aa056b5-111f-5832-ac16-0f12466d00e6',
    '79c34e28-4e0d-538e-8b8f-a29d79906af7','eef1ac8f-cd05-5e3e-b8f1-355b3da5d4a2',
    '7a3cf6e3-c0fb-5e07-b539-ac05c92e4f15')) THEN
    RAISE EXCEPTION 'Old teen templates have review items: review before replacement';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.program_templates WHERE created_by='system:teen-programs-v2'
    OR id IN('e2c01002-8d9a-4e6c-a428-000000000001','e2c01002-8d9a-4e6c-a428-000000000002',
      'e2c01002-8d9a-4e6c-a428-000000000003','e2c01002-8d9a-4e6c-a428-000000000004',
      'e2c01002-8d9a-4e6c-a428-000000000005')) THEN
    RAISE EXCEPTION 'Initial publication identifiers already exist';
  END IF;
END $$;
COMMIT;
