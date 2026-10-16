set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-curriculum-20261016
test -s deployed.txt
db_user=$(docker exec postgres printenv POSTGRES_USER)
lesson_id=$(docker exec postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db -c "SELECT \"Id\" FROM coaching.study_catalog_lessons WHERE \"Name\"='Canlı doğrulama dersi · 16.10.2026' AND \"ExamCode\"='LIVE261016' AND NOT \"IsActive\" AND \"Source\"='admin-manual' AND \"CurriculumVersionId\" IS NULL")
[[ "$lesson_id" =~ ^[0-9a-f-]{36}$ ]]
printf '%s\n' "$lesson_id" > browser-test-lesson-id.txt
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -v lesson="$lesson_id" -U "$db_user" -d coaching_db <<'SQL' > coaching.after-browser.excluding-new-test.txt
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5((to_jsonb(t)-%L::text[])::text),%L ORDER BY md5((to_jsonb(t)-%L::text[])::text)),%L)) FROM %I.%I t %s;',
table_schema||'.'||table_name,
CASE table_name WHEN 'study_catalog_lessons' THEN '{GroupId}' WHEN 'study_catalog_outcomes' THEN '{IsArchived}' WHEN 'study_plan_task_outcomes' THEN '{CodeSnapshot,NameSnapshot}' ELSE '{}' END,
'',CASE table_name WHEN 'study_catalog_lessons' THEN '{GroupId}' WHEN 'study_catalog_outcomes' THEN '{IsArchived}' WHEN 'study_plan_task_outcomes' THEN '{CodeSnapshot,NameSnapshot}' ELSE '{}' END,
'',table_schema,table_name,CASE WHEN table_name='study_catalog_lessons' THEN format('WHERE "Id" <> %L::uuid',:'lesson') ELSE '' END)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory','study_catalog_lesson_groups','study_catalog_exams')
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
SELECT json_build_object('passed',true,
'testExamId',(SELECT "Id" FROM coaching.study_catalog_exams WHERE "Code"='LIVE261016'),
'testExamPassive',(SELECT NOT "IsActive" FROM coaching.study_catalog_exams WHERE "Code"='LIVE261016'),
'testLessonPassive',(SELECT NOT "IsActive" FROM coaching.study_catalog_lessons WHERE "ExamCode"='LIVE261016' AND "Name"='Canlı doğrulama dersi · 16.10.2026'),
'oldPlans',(SELECT count(*) FROM coaching.study_plan_revisions),'oldTasks',(SELECT count(*) FROM coaching.study_plan_tasks),
'completed',(SELECT count(*) FROM coaching.study_plan_tasks WHERE "IsCompleted"),'actualMinutes',(SELECT sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted"),
'publishedDurations',(SELECT count(*) FROM coaching.outcome_duration_estimates WHERE "IsPublished"),
'draftDurations',(SELECT count(*) FROM coaching.outcome_duration_estimates WHERE NOT "IsPublished"),
'snapshotLinks',(SELECT count(*) FROM coaching.study_plan_task_outcomes),
'snapshotTextMatches',(SELECT bool_and(t."NameSnapshot"=o."Name" AND t."CodeSnapshot"=o."Code") FROM coaching.study_plan_task_outcomes t JOIN coaching.study_catalog_outcomes o ON o."Id"=t."OutcomeId"),
'migration',(SELECT max("MigrationId") FROM coaching.__ef_migrations_history));
SQL
jq -e '.passed and .testExamPassive and .testLessonPassive and .oldPlans==4 and .oldTasks==5 and .completed==4 and .actualMinutes==86 and .publishedDurations==52 and .draftDurations==0 and .snapshotLinks==1 and .snapshotTextMatches' browser-checks.json >/dev/null
date -u +%FT%TZ > browser-data-verified.txt
echo 'Original Coaching business rows and Identity account/role/permission/product-access fingerprints preserved; only the named new passive test lesson excluded.'
cat browser-checks.json
