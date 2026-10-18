set -euo pipefail
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT 'REMINDER',"Kind",count(*) FROM coaching.reminder_receipts GROUP BY "Kind";
SELECT 'DRAFT',count(*),sum(jsonb_array_length("SlotsJson")) FROM coaching.study_weekly_drafts;
SELECT 'LGS_MAPPING',m."IsApproved",count(*) FROM coaching.study_catalog_topic_outcomes m JOIN coaching.study_catalog_outcomes o ON o."Id"=m."OutcomeId" JOIN coaching.official_curriculum_editions e ON e."Id"=o."EditionId" WHERE e."ExamCode"='LGS' GROUP BY m."IsApproved";
SELECT 'INACTIVE_TEST_LESSON',l."Id",l."Name",l."ExamCode",count(DISTINCT u."Id"),count(DISTINCT t."Id") FROM coaching.study_catalog_lessons l LEFT JOIN coaching.study_catalog_units u ON u."LessonId"=l."Id" LEFT JOIN coaching.study_catalog_topics t ON t."LessonId"=l."Id" WHERE NOT l."IsActive" GROUP BY l."Id";
SELECT 'TEST_EXAM',"Code","Name","IsActive" FROM coaching.study_catalog_exams WHERE "Code"='LIVE261016';
SELECT 'PGDATABASE',datname FROM pg_database WHERE datname LIKE '%notif%';
SQL
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d notification_db <<'SQL'
SELECT table_name,string_agg(column_name,',' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_schema NOT IN('pg_catalog','information_schema') GROUP BY table_name ORDER BY table_name;
SQL
