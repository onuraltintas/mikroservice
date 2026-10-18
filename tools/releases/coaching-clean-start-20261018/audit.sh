set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT 'TABLE|'||table_name||'|'||string_agg(column_name,',' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_schema='coaching' GROUP BY table_name ORDER BY table_name;
SELECT 'FK|'||conrelid::regclass||'|'||confrelid::regclass||'|'||pg_get_constraintdef(oid) FROM pg_constraint WHERE contype='f' AND connamespace='coaching'::regnamespace ORDER BY conrelid::regclass::text;
SELECT format('SELECT %L,count(*) FROM coaching.%I;',table_name,table_name) FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE' ORDER BY table_name \gexec
SELECT 'SELECT ''GROUPLESSONS'',count(*) FROM coaching.study_catalog_lessons WHERE "GroupId" IS NOT NULL' WHERE EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='coaching' AND table_name='study_catalog_lessons' AND column_name='GroupId') \gexec
SELECT 'CATALOG',"ExamCode",count(*),count(*) FILTER(WHERE "IsActive") FROM coaching.study_catalog_lessons GROUP BY "ExamCode";
SELECT 'OLD_ESTIMATES',count(*) FROM coaching.outcome_duration_estimates e WHERE EXISTS(SELECT 1 FROM coaching.outcome_duration_estimates n WHERE n."OutcomeId"=e."OutcomeId" AND n."IsPublished" AND n."Number">e."Number");
SELECT 'EXTRA_TOPIC',"Id","SourceId","IsActive" FROM coaching.study_catalog_topics WHERE "SourceId"='lgs-2026:din:ornek-davranislar';
SQL
