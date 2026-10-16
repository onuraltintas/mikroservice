set -euo pipefail
cd /var/lib/eduivme/releases/coaching-outcomes-20261016
for file in deployed.txt monitor-verified.txt; do if test -s "$file"; then printf '%s ' "$file"; cat "$file"; fi; done
if test -s live-result.json; then cat live-result.json; fi
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
BEGIN READ ONLY;
SELECT 'outcomes',count(*) FROM coaching.study_catalog_outcomes;
SELECT 'mappings',count(*),count(*) FILTER(WHERE "IsApproved") FROM coaching.study_catalog_topic_outcomes;
SELECT 'durations',count(*),count(*) FILTER(WHERE "IsPublished"),count(*) FILTER(WHERE NOT "IsPublished") FROM coaching.outcome_duration_estimates;
SELECT 'plans',"Status",count(*) FROM coaching.study_plan_revisions GROUP BY "Status" ORDER BY "Status";
SELECT 'completed',count(*),sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted";
SELECT 'migration',max("MigrationId") FROM coaching.__ef_migrations_history;
COMMIT;
SQL
