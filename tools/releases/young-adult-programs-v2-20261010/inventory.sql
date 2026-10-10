-- Run with psql -qAt, while the Speed Reading API is stopped.
-- Compare every permanent table, excluding only the explicitly approved replacement scope.
BEGIN READ ONLY;
SELECT format('SELECT %L || ''|'' || count(*) || ''|'' || md5(COALESCE(string_agg(row_to_json(t)::text, ''|'' ORDER BY row_to_json(t)::text), '''')) FROM %I.%I t %s;',
  tablename,schemaname,tablename,
  CASE tablename
    WHEN 'program_templates' THEN 'WHERE id NOT IN(''76bc9498-558d-55c9-8319-91a12cb13bdd'',''02c04d07-0489-5458-a504-ac4e3e747395'',''1f884bac-73e4-5aa8-b34a-6c431c47a707'',''d1b72e96-f8d6-5bdb-a61d-d30f9a0edf99'',''00f77937-72fe-520b-b509-88f1e4dfd5cc'',''e2c01004-8d9a-4e6c-a428-000000000001'',''e2c01004-8d9a-4e6c-a428-000000000002'',''e2c01004-8d9a-4e6c-a428-000000000003'',''e2c01004-8d9a-4e6c-a428-000000000004'',''e2c01004-8d9a-4e6c-a428-000000000005'')'
    WHEN 'student_program_progress' THEN 'WHERE id<>''8c7166e8-cc77-40b7-a96f-e8e5d891e4e4'''
    WHEN 'daily_exercise_logs' THEN 'WHERE "StudentProgramProgressId"<>''8c7166e8-cc77-40b7-a96f-e8e5d891e4e4'''
    WHEN 'exercise_sessions' THEN 'WHERE id NOT IN(''5607298b-63f4-487d-8a3c-c48986635c0f'',''b323708d-bc7c-4c06-ac50-37023a0282ba'',''fc2fc9fd-90b6-4774-9f2c-e7dce2fb9901'')'
    WHEN 'exercise_session_answers' THEN 'WHERE session_id NOT IN(''5607298b-63f4-487d-8a3c-c48986635c0f'',''b323708d-bc7c-4c06-ac50-37023a0282ba'',''fc2fc9fd-90b6-4774-9f2c-e7dce2fb9901'')'
    WHEN 'exercise_session_results' THEN 'WHERE session_id NOT IN(''5607298b-63f4-487d-8a3c-c48986635c0f'',''b323708d-bc7c-4c06-ac50-37023a0282ba'',''fc2fc9fd-90b6-4774-9f2c-e7dce2fb9901'')'
    ELSE '' END)
FROM pg_tables WHERE schemaname='speed_reading' ORDER BY tablename
\gexec
COMMIT;
