-- psql -qAt; Speed Reading API must remain stopped between both snapshots.
BEGIN READ ONLY;
SELECT format('SELECT %L || ''|'' || count(*) || ''|'' || md5(COALESCE(string_agg(row_to_json(t)::text, ''|'' ORDER BY row_to_json(t)::text), '''')) FROM %I.%I t %s;',
  tablename,schemaname,tablename,
  CASE WHEN tablename='program_templates' THEN
    'WHERE id NOT IN(''c212d2c6-ea6b-5fce-ad7d-111c1ff6b7fe'',''6dd7f01d-127e-5340-baa8-a485307f4d15'',''161b34c6-29e9-57db-8ad2-d3ded5b88a3c'',''dcf57722-3d60-56da-9a89-af8e11fbf4f6'',''27a1c700-f15a-5a0b-9ec9-3d1d2444eb35'',''e2c01003-8d9a-4e6c-a428-000000000001'',''e2c01003-8d9a-4e6c-a428-000000000002'',''e2c01003-8d9a-4e6c-a428-000000000003'',''e2c01003-8d9a-4e6c-a428-000000000004'',''e2c01003-8d9a-4e6c-a428-000000000005'')'
  ELSE '' END)
FROM pg_tables WHERE schemaname='speed_reading' ORDER BY tablename
\gexec
COMMIT;
