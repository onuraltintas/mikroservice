set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-outcomes-20261016
test -s deployed.txt
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' > coaching.after-browser.excluding-new-test.txt
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t %s;',table_schema||'.'||table_name,'','',table_schema,table_name,
CASE WHEN table_name='study_plan_revisions' THEN 'WHERE "Id" <> ''8c4647f1-7f4b-4634-9f47-a0b7f687f3dd''::uuid'
WHEN table_name='study_plan_tasks' THEN 'WHERE "RevisionId" <> ''8c4647f1-7f4b-4634-9f47-a0b7f687f3dd''::uuid' ELSE '' END)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory',
'official_curriculum_editions','official_curriculum_topics','study_catalog_outcomes','study_catalog_outcome_sources','study_catalog_topic_outcomes','outcome_duration_estimates','study_plan_task_outcomes')
ORDER BY table_name
\gexec
SQL
cmp coaching.live.before.txt coaching.after-browser.excluding-new-test.txt
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db <<'SQL' > accounts.after-browser.txt
SELECT 'identity.users',count(*),md5(coalesce(string_agg(md5((to_jsonb(u)-'{LastLoginAt,LastAcceptedMfaTimeStep,MfaFailedAttempts,MfaLockedUntil,UpdatedAt,Version}'::text[])::text),'' ORDER BY "Id"),'')) FROM identity.users u;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_schema||'.'||table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE' AND table_name IN ('UserRoles','UserProductAccesses','Roles','RolePermissions') ORDER BY table_name
\gexec
SQL
cmp accounts.live.before.txt accounts.after-browser.txt
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' > browser-checks.json
SELECT json_build_object(
  'passed', (SELECT count(*)=1 FROM coaching.study_plan_revisions WHERE "Id"='8c4647f1-7f4b-4634-9f47-a0b7f687f3dd' AND "Status"=2 AND "Title"='Canlı doğrulama · LGS kazanım · 16.10.2026'),
  'savedOutcomeLinks',(SELECT count(*) FROM coaching.study_plan_task_outcomes link JOIN coaching.study_plan_tasks task ON task."Id"=link."TaskId" WHERE task."RevisionId"='8c4647f1-7f4b-4634-9f47-a0b7f687f3dd' AND link."AllocatedMinutes"=30 AND link."Origin"='StudentOverride'),
  'completedRecords',(SELECT count(*) FROM coaching.study_plan_tasks WHERE "IsCompleted"),
  'completedMinutes',(SELECT sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted"),
  'allMappingsApproved',(SELECT bool_and("IsApproved") FROM coaching.study_catalog_topic_outcomes),
  'allDurationsPublished',(SELECT count(*)=52 AND bool_and("IsPublished") FROM coaching.outcome_duration_estimates),
  'globalFirstEstimateMinutes',(SELECT d."LearningMinutes"+d."PracticeMinutes"+d."CheckMinutes" FROM coaching.outcome_duration_estimates d JOIN coaching.study_catalog_outcomes o ON o."Id"=d."OutcomeId" WHERE o."Code"='M.8.1.1.1' AND d."IsPublished")
);
SQL
jq -e '.passed and .savedOutcomeLinks==1 and .completedRecords==4 and .completedMinutes==86 and .allMappingsApproved and .allDurationsPublished and .globalFirstEstimateMinutes==135' browser-checks.json >/dev/null
date -u +%FT%TZ > browser-data-verified.txt
echo 'All original Coaching business rows and Identity accounts, roles, permissions and product access match their pre-deployment fingerprints.'
cat browser-checks.json
