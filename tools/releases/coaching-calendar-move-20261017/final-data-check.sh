set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-calendar-move-20261017
db_user=$(docker exec postgres printenv POSTGRES_USER)
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db <<'SQL' > accounts.after-browser.txt
SELECT 'identity.users',count(*),md5(coalesce(string_agg(md5((to_jsonb(u)-'{LastLoginAt,LastAcceptedMfaTimeStep,MfaFailedAttempts,MfaLockedUntil,UpdatedAt,Version}'::text[])::text),'' ORDER BY "Id"),'')) FROM identity.users u;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_schema||'.'||table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE' AND table_name IN ('UserRoles','UserProductAccesses','Roles','RolePermissions') ORDER BY table_name
\gexec
SQL
cmp accounts.live.before.txt accounts.after-browser.txt
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' > browser-db-result.json
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM coaching.study_plan_revisions p LEFT JOIN coaching.student_profiles s ON s."UserId"=p."StudentId" WHERE s."Id" IS NULL) THEN RAISE EXCEPTION 'Plan owner profile missing'; END IF;
 IF EXISTS(SELECT 1 FROM coaching.teacher_student_assignments a LEFT JOIN coaching.student_profiles s ON s."UserId"=a."StudentUserId" LEFT JOIN coaching.teacher_profiles t ON t."UserId"=a."TeacherUserId" WHERE s."Id" IS NULL OR t."Id" IS NULL) THEN RAISE EXCEPTION 'Assignment UserId relationship broken'; END IF;
 IF EXISTS(SELECT 1 FROM coaching.study_plan_task_outcomes o LEFT JOIN coaching.study_plan_tasks t ON t."Id"=o."TaskId" WHERE t."Id" IS NULL) THEN RAISE EXCEPTION 'Task outcome relationship broken'; END IF;
 IF (SELECT count(*) FROM coaching.study_plan_revisions)<>7 OR (SELECT count(*) FROM coaching.study_plan_tasks)<>339 OR (SELECT count(*) FROM coaching.study_plan_tasks WHERE "IsCompleted")<>5 OR (SELECT sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted")<>103 THEN RAISE EXCEPTION 'Unexpected test record totals'; END IF;
 IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_revisions p JOIN coaching.study_plan_tasks t ON t."RevisionId"=p."Id" WHERE p."Id"='b5536157-dd00-4f63-b522-9f250c77b33b' AND p."Status"=0 AND NOT p."IsActive" AND p."StudentId"='473bd2d6-e3db-47c4-be56-eeedec0022ed' AND t."Id"='afbc9fea-ae32-4bb5-bc65-00395d3755f7' AND t."PlannedDate"='2026-10-18' AND t."PlannedStartMinute"=1020 AND t."PlannedMinutes"=30 AND NOT t."IsCompleted") THEN RAISE EXCEPTION 'Exact new teacher draft missing or altered'; END IF;
 IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_revisions WHERE "Id"='2f7e2d07-e044-471d-aedc-dfe004df7e2e' AND "Status"=1 AND "IsActive" AND "Version"=10) THEN RAISE EXCEPTION 'Existing annual plan must stay active'; END IF;
END $$;
SELECT json_build_object('passed',true,'plans',7,'tasks',339,'originalPlans',6,'originalTasks',338,'newTeacherDrafts',1,'newTeacherTasks',1,'completed',5,'actualMinutes',103,'accountsUnchanged',true,'userIdRelationshipsIntact',true,'outcomeLinksIntact',true,'annualPlanActive',true,'annualPlanVersion',10);
SQL
date -u +%FT%TZ > browser-db-verified.txt
cat browser-db-result.json
