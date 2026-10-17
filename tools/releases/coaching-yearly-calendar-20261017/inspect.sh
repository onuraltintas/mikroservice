set -euo pipefail
docker inspect eduivme-production-admin-panel-1 eduivme-production-coaching-service-1 --format '{{.Name}} image={{.Image}} status={{.State.Status}} restarts={{.RestartCount}}'
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT json_build_object('plans',(SELECT count(*) FROM coaching.study_plan_revisions),'tasks',(SELECT count(*) FROM coaching.study_plan_tasks),'completed',(SELECT count(*) FROM coaching.study_plan_tasks WHERE "IsCompleted"),'actualMinutes',(SELECT sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted"),'migration',(SELECT max("MigrationId") FROM coaching.__ef_migrations_history));
SELECT "Id","Title","Status","Version" FROM coaching.study_plan_revisions ORDER BY "CreatedAt";
SELECT "Id","Title","IsCompleted","ActualMinutes","ActualQuestions" FROM coaching.study_plan_tasks WHERE "RevisionId"='2f7e2d07-e044-471d-aedc-dfe004df7e2e' ORDER BY "PlannedDate","PlannedStartMinute","Id" LIMIT 2;
SQL
