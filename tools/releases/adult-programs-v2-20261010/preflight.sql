BEGIN READ ONLY;
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM speed_reading.program_templates WHERE id='55ac59f6-e75b-42ee-822a-ed4acd9cdddb'
    AND "IsAssessment" AND "IsActive" AND NOT "IsDeleted"
    AND "TargetAgeGroupConfigurationId"='10000000-0000-0000-0000-000000000003') THEN
    RAISE EXCEPTION 'Expected preserved adult assessment';
  END IF;
  IF EXISTS (SELECT 1 FROM generate_series(1,5) level WHERE NOT EXISTS (
    SELECT 1 FROM speed_reading.reading_texts t
    WHERE t.target_age_group_id='10000000-0000-0000-0000-000000000003'
      AND t.difficulty_level=level AND t.is_active AND NOT t.is_deleted
      AND EXISTS(SELECT 1 FROM speed_reading.reading_questions q WHERE q.reading_text_id=t.id AND NOT q.is_deleted))) THEN
    RAISE EXCEPTION 'Missing adult reading text with questions';
  END IF;
  IF EXISTS (SELECT 1 FROM generate_series(1,5) level WHERE NOT EXISTS (
    SELECT 1 FROM speed_reading.vocabulary_items v WHERE NOT v.is_deleted AND v.difficulty_level=level
      AND (v.target_age_group_id IS NULL OR v.target_age_group_id='10000000-0000-0000-0000-000000000003'))) THEN
    RAISE EXCEPTION 'Missing adult or common vocabulary';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM speed_reading.__ef_migrations_history
    WHERE "MigrationId"='20261010064029_AddDailyTaskSlotOrder') THEN
    RAISE EXCEPTION 'Expected deployed task-slot migration';
  END IF;
  IF (SELECT count(*) FROM speed_reading.program_templates WHERE id IN(
    'c212d2c6-ea6b-5fce-ad7d-111c1ff6b7fe','6dd7f01d-127e-5340-baa8-a485307f4d15',
    '161b34c6-29e9-57db-8ad2-d3ded5b88a3c','dcf57722-3d60-56da-9a89-af8e11fbf4f6',
    '27a1c700-f15a-5a0b-9ec9-3d1d2444eb35') AND "IsActive" AND NOT "IsDeleted"
    AND NOT "IsAssessment" AND "TargetAgeGroupConfigurationId"='10000000-0000-0000-0000-000000000003')<>5 THEN
    RAISE EXCEPTION 'Expected five old active adult templates';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.student_program_progress WHERE "ProgramTemplateId" IN(
    'c212d2c6-ea6b-5fce-ad7d-111c1ff6b7fe','6dd7f01d-127e-5340-baa8-a485307f4d15',
    '161b34c6-29e9-57db-8ad2-d3ded5b88a3c','dcf57722-3d60-56da-9a89-af8e11fbf4f6',
    '27a1c700-f15a-5a0b-9ec9-3d1d2444eb35')) THEN
    RAISE EXCEPTION 'Old adult templates have history: review before replacement';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.review_items WHERE program_template_id IN(
    'c212d2c6-ea6b-5fce-ad7d-111c1ff6b7fe','6dd7f01d-127e-5340-baa8-a485307f4d15',
    '161b34c6-29e9-57db-8ad2-d3ded5b88a3c','dcf57722-3d60-56da-9a89-af8e11fbf4f6',
    '27a1c700-f15a-5a0b-9ec9-3d1d2444eb35')) THEN
    RAISE EXCEPTION 'Old adult templates have review items: review before replacement';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.program_templates WHERE created_by='system:adult-programs-v2'
    OR id IN('e2c01003-8d9a-4e6c-a428-000000000001','e2c01003-8d9a-4e6c-a428-000000000002',
      'e2c01003-8d9a-4e6c-a428-000000000003','e2c01003-8d9a-4e6c-a428-000000000004',
      'e2c01003-8d9a-4e6c-a428-000000000005')) THEN
    RAISE EXCEPTION 'Initial publication identifiers already exist';
  END IF;
END $$;
COMMIT;
