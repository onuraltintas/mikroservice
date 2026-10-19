CREATE TEMP TABLE lgs_preservation (table_name text, row_count bigint, fingerprint text);
DO $$
DECLARE r record;
BEGIN
  FOR r IN SELECT tablename FROM pg_tables WHERE schemaname='coaching'
    AND tablename NOT IN ('__ef_migrations_history','target_schools','target_admission_batches','target_admission_records')
  LOOP
    EXECUTE format('INSERT INTO lgs_preservation SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),'''' ORDER BY md5(to_jsonb(t)::text)),'''')) FROM coaching.%I t',r.tablename,r.tablename);
  END LOOP;
END $$;
SELECT json_agg(p ORDER BY table_name) FROM lgs_preservation p;
