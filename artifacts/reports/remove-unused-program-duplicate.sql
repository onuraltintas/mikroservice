\set ON_ERROR_STOP on
BEGIN;
LOCK TABLE speed_reading.program_templates IN SHARE ROW EXCLUSIVE MODE;
DO $$
DECLARE
    duplicate_id uuid := 'f035811f-31ae-4665-bc53-bb7df2d197f4';
    retained_id uuid := 'eaf89bb3-7427-4287-a6de-d19f49b782b6';
    t record;
    references_found bigint;
    deleted_count integer;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM speed_reading.program_templates a
        JOIN speed_reading.program_templates b ON b.id = duplicate_id
        WHERE a.id = retained_id
          AND (to_jsonb(a) - ARRAY['id','created_at','updated_at','created_by','updated_by'])
            = (to_jsonb(b) - ARRAY['id','created_at','updated_at','created_by','updated_by'])
    ) THEN
        RAISE EXCEPTION 'Duplicate absent or no longer identical; refusing deletion';
    END IF;
    FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'speed_reading'
        AND tablename <> 'program_templates'
    LOOP
        EXECUTE format('SELECT count(*) FROM speed_reading.%I r WHERE to_jsonb(r)::text LIKE %L',
            t.tablename, '%' || duplicate_id::text || '%') INTO references_found;
        IF references_found > 0 THEN
            RAISE EXCEPTION 'Referenced in %; refusing deletion', t.tablename;
        END IF;
    END LOOP;
    DELETE FROM speed_reading.program_templates WHERE id = duplicate_id;
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    IF deleted_count <> 1 THEN
        RAISE EXCEPTION 'Expected exactly one deletion';
    END IF;
END $$;
COMMIT;
