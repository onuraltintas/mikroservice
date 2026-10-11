BEGIN READ ONLY;
SELECT format('SELECT %L || ''|'' || count(*) || ''|'' || md5(COALESCE(string_agg(row_to_json(t)::text, ''|'' ORDER BY row_to_json(t)::text), '''')) FROM %I.%I t;',
  tablename,schemaname,tablename)
FROM pg_tables WHERE schemaname='speed_reading'
  AND tablename NOT IN ('exercises','exercise_types','reading_texts','reading_questions',
    'program_templates','visualization_scenes','visualization_questions') ORDER BY tablename
\gexec
COMMIT;
