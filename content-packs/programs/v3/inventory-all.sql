CREATE TEMP TABLE full_inventory(table_name text,records bigint,fingerprint text);
BEGIN READ ONLY;
DO $$ DECLARE r record; BEGIN
FOR r IN SELECT table_name FROM information_schema.tables WHERE table_schema='speed_reading' AND table_type='BASE TABLE' ORDER BY table_name LOOP
EXECUTE format('INSERT INTO full_inventory SELECT %L,count(*),md5(coalesce(string_agg(h,'''' ORDER BY h),'''')) FROM (SELECT md5(to_jsonb(t)::text) h FROM speed_reading.%I t) rows',r.table_name,r.table_name);
END LOOP;
END $$;
SELECT table_name||'|'||records||'|'||fingerprint FROM full_inventory ORDER BY table_name;
COMMIT;
