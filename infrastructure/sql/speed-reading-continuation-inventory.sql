CREATE TEMP TABLE protected_inventory(table_name text,records bigint,fingerprint text);
BEGIN READ ONLY;
DO $$ DECLARE r record; BEGIN
 FOR r IN SELECT table_name FROM information_schema.tables
 WHERE table_schema='speed_reading' AND table_type='BASE TABLE'
 AND table_name NOT IN ('program_continuations','__ef_migrations_history') ORDER BY table_name LOOP
  EXECUTE format('INSERT INTO protected_inventory SELECT %L,count(*),md5(coalesce(string_agg(h,'''' ORDER BY h),'''')) FROM (SELECT md5(to_jsonb(t)::text) h FROM speed_reading.%I t) rows',r.table_name,r.table_name);
 END LOOP;
END $$;
SELECT table_name||'|'||records||'|'||fingerprint FROM protected_inventory ORDER BY table_name;
COMMIT;
