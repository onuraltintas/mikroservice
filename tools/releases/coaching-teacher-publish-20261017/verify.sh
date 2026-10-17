set -euo pipefail
umask 077
folder=/var/lib/eduivme/releases/coaching-teacher-publish-20261017
db_user=$(docker exec postgres printenv POSTGRES_USER)
fingerprint() {
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | LC_ALL=C sort
SELECT format('SELECT %L,md5(to_jsonb(t)::text) FROM %I.%I t;',table_name,table_schema,table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory','study_plan_revisions','study_plan_tasks') ORDER BY table_name
\gexec
SELECT 'study_plan_revisions',"Id",md5((to_jsonb(t)-CASE WHEN "Id" IN ('2f7e2d07-e044-471d-aedc-dfe004df7e2e','b5536157-dd00-4f63-b522-9f250c77b33b') THEN '{Status,IsActive,UpdatedAt,UpdatedBy,Version}'::text[] ELSE '{}'::text[] END)::text) FROM coaching.study_plan_revisions t;
SELECT 'study_plan_tasks',"Id",md5((to_jsonb(t)-CASE WHEN "Id"='afbc9fea-ae32-4bb5-bc65-00395d3755f7' THEN '{IsCompleted,ActualMinutes,ActualQuestions,CompletedAt,UpdatedAt,UpdatedBy,Version}'::text[] ELSE '{}'::text[] END)::text) FROM coaching.study_plan_tasks t;
SQL
}
accounts() {
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db <<'SQL'
SELECT 'identity.users',count(*),md5(coalesce(string_agg(md5((to_jsonb(u)-'{LastLoginAt,LastAcceptedMfaTimeStep,MfaFailedAttempts,MfaLockedUntil,UpdatedAt,Version}'::text[])::text),'' ORDER BY "Id"),'')) FROM identity.users u;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_schema||'.'||table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE' AND table_name IN ('UserRoles','UserProductAccesses','Roles','RolePermissions') ORDER BY table_name
\gexec
SQL
}
case "$1" in
 baseline)
  test ! -e "$folder"; mkdir -p "$folder"; cd "$folder"
  for database in identity_db coaching_db; do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before.backup"; done
  sha256sum *.backup > backups.sha256
  fingerprint > protected.before.txt; accounts > accounts.before.txt
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' > before.json
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_revisions WHERE "Id"='b5536157-dd00-4f63-b522-9f250c77b33b' AND "Status"=0 AND NOT "IsActive") THEN RAISE EXCEPTION 'Teacher plan must be a draft'; END IF;
 IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_revisions WHERE "Id"='2f7e2d07-e044-471d-aedc-dfe004df7e2e' AND "Status"=1 AND "IsActive" AND "Version"=10) THEN RAISE EXCEPTION 'Annual plan baseline changed'; END IF;
 IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_tasks WHERE "Id"='afbc9fea-ae32-4bb5-bc65-00395d3755f7' AND NOT "IsCompleted" AND "PlannedDate"='2026-10-18' AND "PlannedStartMinute"=1020 AND "PlannedMinutes"=30) THEN RAISE EXCEPTION 'Teacher task baseline changed'; END IF;
END $$;
SELECT json_build_object('plans',(SELECT count(*) FROM coaching.study_plan_revisions),'tasks',(SELECT count(*) FROM coaching.study_plan_tasks),'completed',(SELECT count(*) FROM coaching.study_plan_tasks WHERE "IsCompleted"),'actualMinutes',(SELECT sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted"));
SQL
  date -u +%FT%TZ > baseline-verified.txt; cat before.json
  ;;
 verify)
  cd "$folder"; test -s baseline-verified.txt
  fingerprint > protected.after.txt; accounts > accounts.after.txt
  cmp protected.before.txt protected.after.txt; cmp accounts.before.txt accounts.after.txt
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' > after.json
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_revisions WHERE "Id"='b5536157-dd00-4f63-b522-9f250c77b33b' AND "Status"=1 AND "IsActive") THEN RAISE EXCEPTION 'Teacher plan is not active'; END IF;
 IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_revisions WHERE "Id"='2f7e2d07-e044-471d-aedc-dfe004df7e2e' AND "Status"=2 AND NOT "IsActive" AND "Version"=11) THEN RAISE EXCEPTION 'Annual plan must be preserved in archive'; END IF;
 IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_tasks WHERE "Id"='afbc9fea-ae32-4bb5-bc65-00395d3755f7' AND "IsCompleted" AND "ActualMinutes"=12 AND "ActualQuestions"=0 AND "CompletedAt" IS NOT NULL AND "PlannedDate"='2026-10-18' AND "PlannedStartMinute"=1020 AND "PlannedMinutes"=30) THEN RAISE EXCEPTION 'Expected exact completion missing'; END IF;
END $$;
SELECT json_build_object('passed',true,'plans',(SELECT count(*) FROM coaching.study_plan_revisions),'tasks',(SELECT count(*) FROM coaching.study_plan_tasks),'completed',(SELECT count(*) FROM coaching.study_plan_tasks WHERE "IsCompleted"),'actualMinutes',(SELECT sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted"),'accountsPreserved',true,'allOtherWorkPreserved',true,'annualPlanArchived',true);
SQL
  jq -e --slurpfile before before.json '.plans==$before[0].plans and .tasks==$before[0].tasks and .completed==$before[0].completed+1 and .actualMinutes==$before[0].actualMinutes+12' after.json >/dev/null
  date -u +%FT%TZ > verified.txt; cat after.json
  ;;
 *) exit 2;;
esac
