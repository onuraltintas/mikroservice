SELECT 'exercise-catalog|'||count(*)||'|'||md5(string_agg(row_to_json(e)::text,'|' ORDER BY e.id)) FROM speed_reading.exercises e;
SELECT 'exercise-types|'||count(*)||'|'||md5(string_agg(row_to_json(e)::text,'|' ORDER BY e.id)) FROM speed_reading.exercise_types e;
SELECT 'preserved-programs|'||count(*)||'|'||md5(string_agg(row_to_json(p)::text,'|' ORDER BY p.id))
FROM speed_reading.program_templates p WHERE p.id NOT IN(
  '8e6b102d-5c6d-5247-916d-d67ab3f7719e','aac00b94-c237-56d0-89fb-30548e953ae1',
  'bef6047b-2ac5-5162-8967-8aaed9c49893','b89ab8e0-fa03-5ab0-ba8a-7085aec613df',
  '5619aefe-ac50-5afc-9f55-a422899c6a80',
  'e2c01001-8d9a-4e6c-a428-000000000001','e2c01001-8d9a-4e6c-a428-000000000002',
  'e2c01001-8d9a-4e6c-a428-000000000003','e2c01001-8d9a-4e6c-a428-000000000004',
  'e2c01001-8d9a-4e6c-a428-000000000005');
