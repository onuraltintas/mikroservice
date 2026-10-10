SELECT "MigrationId" FROM speed_reading.__ef_migrations_history ORDER BY "MigrationId" DESC LIMIT 3;
DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM speed_reading.program_templates WHERE created_by='system:child-programs-v2'
    OR id IN('e2c01001-8d9a-4e6c-a428-000000000001','e2c01001-8d9a-4e6c-a428-000000000002',
      'e2c01001-8d9a-4e6c-a428-000000000003','e2c01001-8d9a-4e6c-a428-000000000004',
      'e2c01001-8d9a-4e6c-a428-000000000005')) THEN
    RAISE EXCEPTION 'Replacement markers/identifiers already exist; initial rollout must stop';
  END IF;
  IF (SELECT count(*) FROM speed_reading.__ef_migrations_history)<>77
    OR (SELECT md5(string_agg("MigrationId",'|' ORDER BY "MigrationId")) FROM speed_reading.__ef_migrations_history)
      IS DISTINCT FROM '4d0e4003168d775e55105cd3771bba94' THEN
    RAISE EXCEPTION 'Unexpected migration baseline; only the reviewed slot migration is permitted';
  END IF;
  IF (SELECT count(*) FROM speed_reading.program_templates WHERE id IN(
    '8e6b102d-5c6d-5247-916d-d67ab3f7719e','aac00b94-c237-56d0-89fb-30548e953ae1',
    'bef6047b-2ac5-5162-8967-8aaed9c49893','b89ab8e0-fa03-5ab0-ba8a-7085aec613df',
    '5619aefe-ac50-5afc-9f55-a422899c6a80'))<>5 THEN
    RAISE EXCEPTION 'Expected five old child programs before initial rollout';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.student_program_progress WHERE "ProgramTemplateId" IN(
    '8e6b102d-5c6d-5247-916d-d67ab3f7719e','aac00b94-c237-56d0-89fb-30548e953ae1',
    'bef6047b-2ac5-5162-8967-8aaed9c49893','b89ab8e0-fa03-5ab0-ba8a-7085aec613df',
    '5619aefe-ac50-5afc-9f55-a422899c6a80')) THEN
    RAISE EXCEPTION 'Old child programs have progress; release must stop';
  END IF;
END $$;
