set -euo pipefail
docker exec eduivme-production-coaching-service-1 sh -c 'sha256sum /app/Coaching.API.dll /app/Coaching.Application.dll /app/Coaching.Domain.dll /app/Coaching.Infrastructure.dll'
docker inspect eduivme-production-coaching-service-1 eduivme-production-admin-panel-1 eduivme-production-staff-portal-1 --format '{{.Name}}|{{.Id}}|{{.Image}}'
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT 'plans',count(*) FROM coaching.study_plan_revisions;
SELECT 'work',count(*),sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted";
SELECT 'contexts',count(*) FROM coaching.student_learning_contexts;
SELECT 'latest-migration',max("MigrationId") FROM coaching.__ef_migrations_history;
SELECT 'outcomes-table',to_regclass('coaching.study_catalog_outcomes') IS NOT NULL;
COMMIT;
SQL
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db <<'SQL'
BEGIN READ ONLY;
SELECT 'accounts',count(*) FROM identity.users;
SELECT 'identity-table',table_name FROM information_schema.tables WHERE table_schema='identity' AND (table_name ILIKE '%role%' OR table_name ILIKE '%product%') ORDER BY table_name;
COMMIT;
SQL
