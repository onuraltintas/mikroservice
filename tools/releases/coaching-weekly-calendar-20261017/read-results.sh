set -euo pipefail
cd /var/lib/eduivme/releases/coaching-weekly-calendar-20261017
for file in deployed.txt monitor-verified.txt; do if test -s "$file"; then printf '%s ' "$file"; cat "$file"; fi; done
test ! -s live-result.json || cat live-result.json
test ! -s browser-checks.json || cat browser-checks.json
test ! -s monitor.log || { printf 'monitor-samples '; grep -Ec '^20[0-9]{2}-' monitor.log; tail -6 monitor.log; }
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT 'plans',count(*) FROM coaching.study_plan_revisions;
SELECT 'tasks',count(*),count(*) FILTER(WHERE "IsCompleted"),sum("ActualMinutes") FROM coaching.study_plan_tasks;
SELECT 'outcomes',count(*) FROM coaching.study_catalog_outcomes;
SELECT 'latest-migration',max("MigrationId") FROM coaching.__ef_migrations_history;
SQL
