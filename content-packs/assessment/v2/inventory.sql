CREATE TEMP TABLE protected_inventory(table_name text,records bigint,fingerprint text);
BEGIN READ ONLY;
DO $$ DECLARE r record; BEGIN
 FOR r IN SELECT table_name FROM information_schema.tables
 WHERE table_schema='speed_reading' AND table_type='BASE TABLE' AND table_name<>'exercises' ORDER BY table_name LOOP
  EXECUTE format('INSERT INTO protected_inventory SELECT %L,count(*),md5(coalesce(string_agg(h,'''' ORDER BY h),'''')) FROM (SELECT md5(to_jsonb(t)::text) h FROM speed_reading.%I t) rows',r.table_name,r.table_name);
 END LOOP;
END $$;
INSERT INTO protected_inventory
SELECT 'exercises-other',count(*),md5(coalesce(string_agg(h,'' ORDER BY h),''))
FROM (SELECT md5(to_jsonb(t)::text) h FROM speed_reading.exercises t WHERE id NOT IN ('99b0a038-90b9-5156-b803-f96d30753499'::uuid,'82087c90-eb56-587a-b064-cdc27e15f14b'::uuid,'dc182755-52fe-5b86-be96-ba98fbcd04b9'::uuid,'4d7c5eb7-2a1e-5900-b4ac-bdf3add28872'::uuid,'291705e1-254f-5379-aecd-c033901c276d'::uuid,'0f67593d-efd9-5f0d-814b-1f7d2692a48f'::uuid,'bd64c53c-c379-5a42-bcfc-a0f076189520'::uuid,'4dd52a35-bbee-512a-914a-8f471438c528'::uuid,'39b33543-b617-5e3e-a6fe-06d82b58137b'::uuid,'705e471b-ec39-508d-aa84-dd5bee4b6bcf'::uuid,'9ca42784-5cdb-5aaf-be5b-d0f3924ecdeb'::uuid,'c5d9326d-2097-51f7-974f-5b660847c831'::uuid)) rows;
SELECT table_name||'|'||records||'|'||fingerprint FROM protected_inventory ORDER BY table_name;
COMMIT;
