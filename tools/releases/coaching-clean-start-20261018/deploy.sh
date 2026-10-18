set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-clean-start-20261018
drill=coaching_clean_start_drill_20261018
notifdrill=notification_clean_start_drill_20261018
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock;flock -n 9
db_user=$(docker exec postgres printenv POSTGRES_USER)
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
plan_ids=$(cat plan-ids.txt 2>/dev/null || true);student_ids=$(cat student-ids.txt 2>/dev/null || true)
protected() {
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' | LC_ALL=C sort
SELECT format('SELECT %L||md5((to_jsonb(t)%s)::text) FROM coaching.%I t %s;',table_name||'|',
 CASE WHEN table_name='study_catalog_lessons' THEN '-''GroupId''' ELSE '' END,table_name,
 CASE table_name
 WHEN 'study_catalog_lessons' THEN 'WHERE t."ExamCode" IS DISTINCT FROM ''LIVE261016'''
 WHEN 'study_catalog_exams' THEN 'WHERE t."Code"<>''LIVE261016'''
 WHEN 'study_catalog_topics' THEN 'WHERE t."SourceId"<>''lgs-2026:din:ornek-davranislar'''
 WHEN 'study_catalog_topic_outcomes' THEN 'WHERE t."IsApproved"'
 WHEN 'outcome_duration_estimates' THEN 'WHERE NOT EXISTS(SELECT 1 FROM coaching.outcome_duration_estimates n WHERE n."OutcomeId"=t."OutcomeId" AND n."IsPublished" AND n."Number">t."Number")'
 WHEN 'reminder_receipts' THEN 'WHERE t."Kind"<>''StudyDay'''
 ELSE '' END)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN('study_plan_revisions','study_plan_tasks','study_plan_task_outcomes','study_weekly_drafts','study_availability','study_catalog_lesson_groups','__ef_migrations_history','AdminAuditRecords','InboxState','OutboxMessage','OutboxState') ORDER BY table_name
\gexec
SQL
}
accounts() {
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db <<'SQL'
SELECT count(*),md5(coalesce(string_agg(md5((to_jsonb(u)-'{LastLoginAt,LastAcceptedMfaTimeStep,MfaFailedAttempts,MfaLockedUntil,UpdatedAt,Version}'::text[])::text),'' ORDER BY "Id"),'')) FROM identity.users u;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE' AND table_name IN ('UserRoles','UserProductAccesses','Roles','RolePermissions') ORDER BY table_name \gexec
SQL
}
notifications_protected() {
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -v plan_ids="$plan_ids" -U "$db_user" -d "${1:-notification_db}" <<'SQL'
SELECT count(*),md5(coalesce(string_agg(md5(to_jsonb(n)::text),'' ORDER BY "Id"),'')) FROM public."Notifications" n
 WHERE NOT("Type" IN('StudyPlanPublished','StudyPlanUpdated','StudyPlanReminder') AND coalesce("RelatedEntityId",'')=ANY(string_to_array(:'plan_ids',',')));
SQL
}
ready() {
 local ip
 for attempt in $(seq 1 30);do
  ip=$(docker inspect eduivme-production-coaching-service-1 --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}'|head -1)
  if [[ "$ip" =~ ^[0-9.]+$ ]]&&curl -fsS --max-time 4 "http://$ip:8080/health/ready" >/dev/null 2>&1;then return 0;fi
  sleep 2
 done;return 1
}
unchanged() {
 while read -r name id;do
  [[ "$name" == eduivme-production-coaching-service-1 ]]&&continue
  [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]
 done < containers.before.txt
}
cleanup() {
 [[ "$plan_ids" =~ ^[0-9a-f,-]+$ && "$student_ids" =~ ^[0-9a-f,-]+$ ]]
 docker exec -i postgres psql -X -q -v ON_ERROR_STOP=1 -v plan_ids="$plan_ids" -v student_ids="$student_ids" -U "$db_user" -d "$1" < cleanup.sql
}
verify_data() {
 export VERIFY_CONNECTION VERIFY_SCOPE VERIFY_ADMIN_ID
 VERIFY_SCOPE=$1;VERIFY_CONNECTION=$(jq -er '.ConnectionStrings__DefaultConnection' coaching.environment.json)
 if [[ "$1" == drill ]];then VERIFY_CONNECTION=$(jq -nr --arg c "$VERIFY_CONNECTION" --arg d "$drill" '$c|sub("Database=coaching_db;";"Database="+$d+";")');[[ "$VERIFY_CONNECTION" == *"Database=$drill;"* ]];fi
 VERIFY_ADMIN_ID=$(docker exec postgres psql -X -qAt -U "$db_user" -d identity_db -c "SELECT DISTINCT u.\"Id\" FROM identity.users u JOIN identity.\"UserRoles\" ur ON ur.\"UserId\"=u.\"Id\" JOIN identity.\"Roles\" r ON r.\"Id\"=ur.\"RoleId\" WHERE u.\"Email\"='admin@eduivme.com' AND u.\"IsActive\" AND r.\"Name\"='SystemAdmin'")
 [[ "$VERIFY_ADMIN_ID" =~ ^[0-9a-f-]{36}$ ]]
 docker run --rm --network "$(<network.txt)" --entrypoint sh -e VERIFY_CONNECTION -e VERIFY_SCOPE -e VERIFY_ADMIN_ID -v "$release/payload/verification:/verification:ro" "$(<built-image.txt)" -c 'mkdir /tmp/verify && cp /verification/Verification.* /tmp/verify/ && cp /app/*.dll /tmp/verify/ && dotnet /tmp/verify/Verification.dll'
}
sha256sum -c payload.sha256 > payload-verified.log
case "$1" in
 prepare)
  test ! -e prepared.txt
  docker inspect $(docker ps --format '{{.Names}}') --format '{{.Name}} {{.Id}}'|sed 's#^/##' > containers.before.txt
  docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}' > previous-image.txt
  docker exec eduivme-production-coaching-service-1 sha256sum /app/Coaching.Domain.dll /app/Coaching.Application.dll /app/Coaching.Infrastructure.dll /app/Coaching.API.dll|sed 's#/app/##' > live-base-dlls.sha256;cmp expected-base-dlls.sha256 live-base-dlls.sha256
  docker inspect eduivme-production-coaching-service-1|jq -er '.[0].NetworkSettings.Networks|keys[0]' > network.txt
  docker inspect eduivme-production-coaching-service-1|jq '.[0].Config.Env|map(capture("^(?<key>[^=]+)=(?<value>.*)$"))|from_entries' > coaching.environment.json
  "${compose[@]}" -f /var/lib/eduivme/releases/coaching-scope-shares-20261018/production.compose.json config --format json > base.compose.json
  jq --arg image "$(<previous-image.txt)" --slurpfile env coaching.environment.json '.services["coaching-service"].image=$image|.services["coaching-service"].environment=$env[0]|walk(if type=="string" then gsub("\\$";"$$") else . end)' base.compose.json > rollback.compose.json
  for database in coaching_db identity_db notification_db;do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before-drill.backup";done
  sha256sum *.before-drill.backup > backups.sha256
  docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c 'SELECT string_agg("Id"::text,'"'"','"'"' ORDER BY "Id") FROM coaching.study_plan_revisions' > plan-ids.txt
  docker exec postgres psql -X -qAt -U "$db_user" -d coaching_db -c 'SELECT string_agg(id::text,'"'"','"'"' ORDER BY id) FROM (SELECT DISTINCT "StudentId" AS id FROM coaching.study_plan_revisions) s' > student-ids.txt
  date -u +%FT%TZ > prepared.txt;echo 'Three database backups and exact test-plan snapshot prepared.'
  ;;
 build)
  test -s prepared.txt;test ! -e built.txt
  docker tag "$(<previous-image.txt)" eduivme/coaching-service:clean-start-base-20261018
  docker build --network none --pull=false --build-arg BASE_IMAGE=eduivme/coaching-service:clean-start-base-20261018 --build-arg SOURCE_REVISION=clean-start-20261018 -f coaching-service.Dockerfile -t eduivme/coaching-service:clean-start-20261018 . > build.log 2>&1
  docker image inspect eduivme/coaching-service:clean-start-20261018 --format '{{.Id}}' > built-image.txt
  jq --arg image "$(<built-image.txt)" '.services["coaching-service"].image=$image' rollback.compose.json > production.compose.json
  "${compose[@]}" -f production.compose.json config --quiet;date -u +%FT%TZ > built.txt
  ;;
 drill)
  test -s built.txt;test ! -e drill-verified.txt
  connection=$(jq -er '.ConnectionStrings__DefaultConnection' coaching.environment.json);pattern='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)';[[ "$connection" =~ $pattern ]];owner=${BASH_REMATCH[2]}
  if [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$drill'") == 0 ]];then
   docker exec postgres createdb -U "$db_user" -O "$owner" "$drill"
   docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$drill" --exit-on-error --no-owner --no-privileges < coaching_db.before-drill.backup > drill-restore.log 2>&1
   protected "$drill" > drill-protected.before.txt
  fi
  jq --arg d "$drill" '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection|=sub("Database=coaching_db;";"Database="+$d+";")' production.compose.json > drill.compose.json
  jq -e --arg d "$drill" '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection|contains("Database="+$d+";")' drill.compose.json >/dev/null
  "${compose[@]}" -f drill.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > drill-migrate.log 2>&1
  cleanup "$drill" > drill-cleanup.log;protected "$drill" > drill-protected.after.txt;cmp drill-protected.before.txt drill-protected.after.txt
  verify_data drill > drill-result.json 2> drill-error.log;jq -e '.passed' drill-result.json >/dev/null
  cleanup "$drill" > drill-replay-cleanup.log;protected "$drill" > drill-replay.after.txt;cmp drill-protected.after.txt drill-replay.after.txt
  if [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$notifdrill'") == 0 ]];then
   docker exec postgres createdb -U "$db_user" "$notifdrill"
   docker exec -i postgres pg_restore -U "$db_user" -d "$notifdrill" --exit-on-error --no-owner --no-privileges < notification_db.before-drill.backup > notifications-drill-restore.log 2>&1
  fi
  notifications_protected "$notifdrill" > notifications-drill.before.txt
  docker exec -i postgres psql -X -q -v ON_ERROR_STOP=1 -v plan_ids="$plan_ids" -U "$db_user" -d "$notifdrill" < notifications.sql > notifications-drill-cleanup.log
  notifications_protected "$notifdrill" > notifications-drill.after.txt;cmp notifications-drill.before.txt notifications-drill.after.txt
  unchanged;date -u +%FT%TZ > drill-verified.txt;cat drill-result.json
  ;;
 promote)
  test -s drill-verified.txt;test ! -e deployed.txt;unchanged
  [[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}') == "$(<previous-image.txt)" ]]
  for database in coaching_db identity_db notification_db;do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before-cleanup.backup";done
  sha256sum *.before-cleanup.backup > final-backups.sha256
  protected coaching_db > live-protected.before.txt;accounts > accounts.before.txt;notifications_protected > notifications.before.txt
  recover_service() {
   local status=$?;trap - EXIT
   if ((status!=0));then
    if [[ -e schema-applied.txt ]];then target=production.compose.json;else target=rollback.compose.json;fi
    "${compose[@]}" -f "$target" up -d --no-deps --pull never coaching-service > recovery.log 2>&1
    ready || true
   fi
   exit "$status"
  };trap recover_service EXIT
  "${compose[@]}" -f rollback.compose.json stop coaching-service > stop.log 2>&1
  "${compose[@]}" -f production.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > live-migrate.log 2>&1
  date -u +%FT%TZ > schema-applied.txt
  cleanup coaching_db > live-cleanup.log
  docker exec -i postgres psql -X -q -v ON_ERROR_STOP=1 -v plan_ids="$plan_ids" -U "$db_user" -d notification_db < notifications.sql > notifications-cleanup.log
  "${compose[@]}" -f production.compose.json up -d --no-deps --pull never coaching-service > promote.log 2>&1;ready
  protected coaching_db > live-protected.after.txt;cmp live-protected.before.txt live-protected.after.txt
  accounts > accounts.after.txt;cmp accounts.before.txt accounts.after.txt;notifications_protected > notifications.after.txt;cmp notifications.before.txt notifications.after.txt
  verify_data live > live-result.json 2> live-error.log;jq -e '.passed' live-result.json >/dev/null
  cleanup coaching_db > live-replay-cleanup.log;protected coaching_db > live-replay.after.txt;cmp live-protected.after.txt live-replay.after.txt
  ready;unchanged;date -u +%FT%TZ > deployed.txt;trap - EXIT;cat live-result.json
  ;;
 verify)
  test -s deployed.txt;ready;unchanged;verify_data live
  protected coaching_db > live-latest.txt;cmp live-protected.before.txt live-latest.txt
  accounts > accounts.latest.txt;cmp accounts.before.txt accounts.latest.txt
  notifications_protected > notifications.latest.txt;cmp notifications.before.txt notifications.latest.txt
  ;;
 *) exit 2;;
esac
