set -euo pipefail
cd /var/lib/eduivme/releases/coaching-teacher-wizard-20261018
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL'
SELECT json_build_object('id',r."Id",'status',r."Status",'active',r."IsActive",'version',r."Version",'tasks',count(t."Id"),'minutes',sum(t."PlannedMinutes"),'firstDate',min(t."PlannedDate"),'lastDate',max(t."PlannedDate"),'linkedTasks',(SELECT count(DISTINCT o."TaskId") FROM coaching.study_plan_task_outcomes o JOIN coaching.study_plan_tasks t2 ON t2."Id"=o."TaskId" WHERE t2."RevisionId"=r."Id")) FROM coaching.study_plan_revisions r JOIN coaching.study_plan_tasks t ON t."RevisionId"=r."Id" WHERE r."Id"='84e6448f-069c-4ec3-ba9b-daa07e3edbb9' GROUP BY r."Id";
SELECT json_build_object('plans',count(*),'active',count(*) FILTER(WHERE "IsActive")) FROM coaching.study_plan_revisions;
SELECT json_build_object('completed',count(*),'actualMinutes',sum("ActualMinutes")) FROM coaching.study_plan_tasks WHERE "IsCompleted";
SELECT json_build_object('orphanLinks',count(*)) FROM coaching.study_plan_task_outcomes o LEFT JOIN coaching.study_plan_tasks t ON t."Id"=o."TaskId" WHERE t."Id" IS NULL;
SQL
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db <<'SQL' > verification/identity.after-workflow.txt
SELECT 'identity.users',count(*),md5(coalesce(string_agg(md5((to_jsonb(u)-'{LastLoginAt,LastAcceptedMfaTimeStep,MfaFailedAttempts,MfaLockedUntil,UpdatedAt,Version}'::text[])::text),'' ORDER BY "Id"),'')) FROM identity.users u;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_schema||'.'||table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE' AND table_name IN ('UserRoles','UserProductAccesses','Roles','RolePermissions') ORDER BY table_name
\gexec
SQL
cmp identity.before.txt verification/identity.after-workflow.txt
echo 'Accounts and role/product assignments preserved.'
for service in coaching-service staff-portal; do
  name="eduivme-production-$service-1"
  docker inspect "$name" --format '{{.Name}} running={{.State.Running}} restarts={{.RestartCount}}'
  docker logs --since "$(cat deployed.txt)" "$name" > "verification/$service.log" 2>&1
  count=$(grep -Eic 'Unhandled exception|\[ERR\]|\[FTL\]|^fail:| 500 ' "verification/$service.log" || true)
  echo "$service error-markers=$count"
done
