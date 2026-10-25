set -euo pipefail
for service in coaching-service notification-service admin-panel staff-portal; do
 docker inspect "eduivme-production-$service-1" --format '{{.Name}} image={{.Image}} status={{.State.Status}} restarts={{.RestartCount}}'
done
for module in Coaching.API Coaching.Application Coaching.Infrastructure Coaching.Domain EduPlatform.Shared.Contracts; do
 docker exec eduivme-production-coaching-service-1 sha256sum "/app/$module.dll" "/app/$module.pdb"
done
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT "MigrationId" FROM coaching.__ef_migrations_history ORDER BY "MigrationId" DESC LIMIT 3;
SELECT 'plans',count(*) FROM coaching.study_plan_revisions;
SELECT 'decisions',count(*) FROM coaching.weekly_decisions;
COMMIT;
SQL
