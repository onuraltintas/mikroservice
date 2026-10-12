CREATE TEMP TABLE protected_inventory(table_name text,records bigint,fingerprint text);
BEGIN READ ONLY;
DO $$ DECLARE r record; expression text; filter text; BEGIN
FOR r IN SELECT table_name FROM information_schema.tables WHERE table_schema='speed_reading' AND table_type='BASE TABLE' ORDER BY table_name LOOP
 expression := 'to_jsonb(t)'; filter := 'true';
 IF r.table_name='exercises' THEN
   expression := '(to_jsonb(t)-''is_assessment_only'')';
   expression := '('||expression||' - CASE WHEN id IN(''d8f10000-0000-5000-8000-000000000011'',''d8f10000-0000-5000-8000-000000000012'',''d8f10000-0000-5000-8000-000000000013'',''b59e48d3-da80-5ce2-90fa-f784b78b9d9d'',''41bbc0ee-6d42-53dc-bde6-c7f65c615d60'') THEN ARRAY[''configuration_json'',''updated_at'',''updated_by'',''version''] ELSE ARRAY[]::text[] END)';
 ELSIF r.table_name='program_templates' THEN
   expression := '(to_jsonb(t) - CASE WHEN id::text LIKE ''e2c0100_-8d9a-4e6c-a428-00000000000_'' AND NOT "IsAssessment" THEN ARRAY[''WeeklyPatternJson'',''updated_at'',''updated_by'',''version''] ELSE ARRAY[]::text[] END)';
 ELSIF r.table_name='student_program_progress' THEN
   expression := '(to_jsonb(t) - CASE WHEN "ProgramTemplateId"=''e2c01002-8d9a-4e6c-a428-000000000001'' THEN ARRAY[''ScheduleJson'',''updated_at'',''updated_by'',''version''] ELSE ARRAY[]::text[] END)';
 ELSIF r.table_name='reading_texts' THEN filter := 'id::text NOT LIKE ''fa031200-0000-5000-8000-%''';
 ELSIF r.table_name='reading_questions' THEN filter := 'id::text NOT LIKE ''fa031201-0000-5000-8000-%''';
 ELSIF r.table_name='__ef_migrations_history' THEN filter := '"MigrationId"<>''20261012020000_SeparateAssessmentExerciseScope''';
 END IF;
 EXECUTE format('INSERT INTO protected_inventory SELECT %L,count(*),md5(coalesce(string_agg(h,'''' ORDER BY h),'''')) FROM (SELECT md5((%s)::text) h FROM speed_reading.%I t WHERE %s) rows',r.table_name,expression,r.table_name,filter);
END LOOP;
END $$;
SELECT table_name||'|'||records||'|'||fingerprint FROM protected_inventory ORDER BY table_name;
COMMIT;
