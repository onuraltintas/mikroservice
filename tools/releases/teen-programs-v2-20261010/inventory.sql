BEGIN READ ONLY;
SELECT 'exercise-catalog|'||count(*)||'|'||md5(string_agg(row_to_json(e)::text,'|' ORDER BY e.id)) FROM speed_reading.exercises e;
SELECT 'exercise-types|'||count(*)||'|'||md5(string_agg(row_to_json(e)::text,'|' ORDER BY e.id)) FROM speed_reading.exercise_types e;
SELECT 'preserved-programs|'||count(*)||'|'||md5(string_agg(row_to_json(p)::text,'|' ORDER BY p.id))
FROM speed_reading.program_templates p WHERE p.id NOT IN(
  '8d3f8827-c0ff-5b91-99cf-d01a192b9bbb','4aa056b5-111f-5832-ac16-0f12466d00e6',
  '79c34e28-4e0d-538e-8b8f-a29d79906af7','eef1ac8f-cd05-5e3e-b8f1-355b3da5d4a2',
  '7a3cf6e3-c0fb-5e07-b539-ac05c92e4f15',
  'e2c01002-8d9a-4e6c-a428-000000000001','e2c01002-8d9a-4e6c-a428-000000000002',
  'e2c01002-8d9a-4e6c-a428-000000000003','e2c01002-8d9a-4e6c-a428-000000000004',
  'e2c01002-8d9a-4e6c-a428-000000000005');
SELECT 'migrations|'||count(*)||'|'||md5(string_agg("MigrationId",'|' ORDER BY "MigrationId")) FROM speed_reading.__ef_migrations_history;
COMMIT;
