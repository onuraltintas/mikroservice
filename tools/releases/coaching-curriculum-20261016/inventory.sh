set -euo pipefail
docker inspect eduivme-production-coaching-service-1 eduivme-production-admin-panel-1 eduivme-production-staff-portal-1 --format '{{.Name}}|{{.Id}}|{{.Image}}'
docker exec eduivme-production-coaching-service-1 sh -c 'sha256sum /app/Coaching.API.dll /app/Coaching.Application.dll /app/Coaching.Domain.dll /app/Coaching.Infrastructure.dll'
docker exec eduivme-production-admin-panel-1 sha256sum /app/dist/admin-panel/server/server.mjs
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT 'plans',count(*) FROM coaching.study_plan_revisions;
SELECT 'tasks',count(*),count(*) FILTER(WHERE "IsCompleted"),sum("ActualMinutes") FROM coaching.study_plan_tasks;
SELECT 'latest-migration',max("MigrationId") FROM coaching.__ef_migrations_history;
SELECT 'lessons',count(*) FROM coaching.study_catalog_lessons;
SELECT 'outcomes',count(*) FROM coaching.study_catalog_outcomes;
SELECT 'curriculum-exams-table',to_regclass('coaching.study_catalog_exams') IS NOT NULL;
COMMIT;
SQL
docker exec postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db -c 'SELECT count(*) FROM identity.users'
df -h /var/lib/eduivme /opt/eduivme
