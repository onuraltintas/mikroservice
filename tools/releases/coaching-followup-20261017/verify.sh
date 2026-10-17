set -euo pipefail
umask 077
folder=/var/lib/eduivme/releases/coaching-followup-20261017/live-verification
db_user=$(docker exec postgres printenv POSTGRES_USER)
fingerprint() {
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' | LC_ALL=C sort
SELECT format('SELECT %L,md5(to_jsonb(t)::text) FROM %I.%I t;',table_name,table_schema,table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory','study_plan_revisions','study_plan_tasks') ORDER BY table_name
\gexec
SELECT 'study_plan_revisions',"Id",md5((to_jsonb(t)-CASE WHEN "Id"='2f7e2d07-e044-471d-aedc-dfe004df7e2e' THEN '{Version,UpdatedAt,UpdatedBy}'::text[] ELSE '{}'::text[] END)::text) FROM coaching.study_plan_revisions t;
SELECT 'study_plan_tasks',"Id",md5((to_jsonb(t)-CASE WHEN "Id"='84d75f90-06e8-424b-a772-3b519b2c3aac' THEN '{Version,UpdatedAt,UpdatedBy}'::text[] ELSE '{}'::text[] END)::text) FROM coaching.study_plan_tasks t;
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
notifications() { docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d notification_db -c 'SELECT "Id",md5(to_jsonb(t)::text) FROM public."Notifications" t ORDER BY "Id";' | LC_ALL=C sort; }
case "$1" in
 baseline)
  test ! -e "$folder"; mkdir -p "$folder";cd "$folder"
  fingerprint > protected.before.txt;accounts > accounts.before.txt;notifications > notifications.before.txt
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' > before.json
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_revisions WHERE "Id"='2f7e2d07-e044-471d-aedc-dfe004df7e2e' AND "Status"=1 AND "IsActive") THEN RAISE EXCEPTION 'Annual plan must remain active'; END IF;
 IF NOT EXISTS(SELECT 1 FROM coaching.study_plan_tasks WHERE "Id"='84d75f90-06e8-424b-a772-3b519b2c3aac' AND NOT "IsCompleted") THEN RAISE EXCEPTION 'Expected pending task missing'; END IF;
END $$;
SELECT json_build_object('plans',(SELECT count(*) FROM coaching.study_plan_revisions),'tasks',(SELECT count(*) FROM coaching.study_plan_tasks),'completed',(SELECT count(*) FROM coaching.study_plan_tasks WHERE "IsCompleted"),'actualMinutes',(SELECT sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted"),'annualVersion',(SELECT "Version" FROM coaching.study_plan_revisions WHERE "Id"='2f7e2d07-e044-471d-aedc-dfe004df7e2e'),'taskTitle',(SELECT "Title" FROM coaching.study_plan_tasks WHERE "Id"='84d75f90-06e8-424b-a772-3b519b2c3aac'));
SQL
  date -u +%FT%TZ > baseline-verified.txt;cat before.json
  ;;
 verify)
  cd "$folder"; test -s baseline-verified.txt
  fingerprint > protected.after.txt;accounts > accounts.after.txt;notifications > notifications.after.txt
  cmp protected.before.txt protected.after.txt;cmp accounts.before.txt accounts.after.txt
  [[ $(comm -23 notifications.before.txt notifications.after.txt | wc -l) == 0 ]]
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d coaching_db <<'SQL' > after.json
SELECT json_build_object('plans',(SELECT count(*) FROM coaching.study_plan_revisions),'tasks',(SELECT count(*) FROM coaching.study_plan_tasks),'completed',(SELECT count(*) FROM coaching.study_plan_tasks WHERE "IsCompleted"),'actualMinutes',(SELECT sum("ActualMinutes") FROM coaching.study_plan_tasks WHERE "IsCompleted"),'annualVersion',(SELECT "Version" FROM coaching.study_plan_revisions WHERE "Id"='2f7e2d07-e044-471d-aedc-dfe004df7e2e'),'allBusinessRowsPreserved',true,'allExistingNotificationsPreserved',true,'accountsPreserved',true);
SQL
  jq -e --slurpfile before before.json '.plans==$before[0].plans and .tasks==$before[0].tasks and .completed==$before[0].completed and .actualMinutes==$before[0].actualMinutes' after.json >/dev/null
  date -u +%FT%TZ > verified.txt;cat after.json
  ;;
 *) exit 2;;
esac
