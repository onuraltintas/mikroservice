set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-scope-shares-20261018
drill=coaching_scope_shares_drill_20261018
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock;flock -n 9
db_user=$(docker exec postgres printenv POSTGRES_USER)
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
rows() {
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' | LC_ALL=C sort
SELECT format('SELECT %L||%L||md5((to_jsonb(t)%s)::text) FROM %I.%I t;',table_name,'|',
 CASE WHEN table_name='study_catalog_topics' THEN
 '-CASE WHEN t."LessonId" IN (SELECT "Id" FROM coaching.study_catalog_lessons WHERE "ExamCode"=''LGS'') THEN ARRAY[''EstimatedMinutes'']::text[] ELSE ARRAY[]::text[] END - CASE WHEN t."SourceId"=''lgs-2026:din:ornek-davranislar'' THEN ARRAY[''IsActive'']::text[] ELSE ARRAY[]::text[] END'
 WHEN table_name='study_catalog_topic_outcomes' THEN '-ARRAY[''IsApproved'',''Evidence'',''ReviewedBy'',''ReviewedAt'']::text[]' ELSE '' END,table_schema,table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState') ORDER BY table_name
\gexec
SQL
}
accounts() {
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db <<'SQL'
SELECT count(*),md5(coalesce(string_agg(md5((to_jsonb(u)-'{LastLoginAt,LastAcceptedMfaTimeStep,MfaFailedAttempts,MfaLockedUntil,UpdatedAt,Version}'::text[])::text),'' ORDER BY "Id"),'')) FROM identity.users u;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE' AND table_name IN ('UserRoles','UserProductAccesses','Roles','RolePermissions') ORDER BY table_name
\gexec
SQL
}
preserved() {
 rows "$1" > "$2.after.txt"
 LC_ALL=C comm -23 "$2.before.txt" "$2.after.txt" > "$2.missing.txt"
 test ! -s "$2.missing.txt"
}
unchanged() {
 while read -r name id;do
  [[ "$name" == eduivme-production-coaching-service-1 ]]&&continue
  [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]
 done < containers.before.txt
}
ready() {
 local ip
 for attempt in $(seq 1 30);do
  ip=$(docker inspect eduivme-production-coaching-service-1 --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}'|head -1)
  if [[ "$ip" =~ ^[0-9.]+$ ]]&&curl -fsS --max-time 4 "http://$ip:8080/health/ready" >/dev/null 2>&1;then return 0;fi
  sleep 2
 done
 return 1
}
run() {
 local mode=$1
 export VERIFY_CONNECTION VERIFY_SCOPE VERIFY_ADMIN_ID VERIFY_APPROVAL
 VERIFY_SCOPE=$mode;VERIFY_APPROVAL=user-approved-scope-shares-20261018
 VERIFY_CONNECTION=$(jq -er '.ConnectionStrings__DefaultConnection' coaching.environment.json)
 if [[ "$mode" == drill ]];then
  VERIFY_CONNECTION=$(jq -nr --arg c "$VERIFY_CONNECTION" --arg d "$drill" '$c|sub("Database=coaching_db;";"Database="+$d+";")')
  [[ "$VERIFY_CONNECTION" == *"Database=$drill;"* ]]
 fi
 VERIFY_ADMIN_ID=$(docker exec postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db -c "SELECT DISTINCT u.\"Id\" FROM identity.users u JOIN identity.\"UserRoles\" ur ON ur.\"UserId\"=u.\"Id\" JOIN identity.\"Roles\" r ON r.\"Id\"=ur.\"RoleId\" WHERE u.\"Email\"='admin@eduivme.com' AND u.\"IsActive\" AND r.\"Name\"='SystemAdmin'")
 [[ "$VERIFY_ADMIN_ID" =~ ^[0-9a-f-]{36}$ ]]
 docker run --rm --network "$(<network.txt)" --entrypoint sh -e VERIFY_CONNECTION -e VERIFY_SCOPE -e VERIFY_ADMIN_ID -e VERIFY_APPROVAL \
  -v "$release/payload/verification:/verification:ro" "$(<built-image.txt)" \
  -c 'mkdir /tmp/scope-verification && cp /verification/Verification.* /tmp/scope-verification/ && cp /app/*.dll /tmp/scope-verification/ && dotnet /tmp/scope-verification/Verification.dll'
}
sha256sum -c payload.sha256 > payload-verified.log
case "$1" in
 prepare)
  test ! -e prepared.txt
  docker inspect $(docker ps --format '{{.Names}}') --format '{{.Name}} {{.Id}}' |sed 's#^/##' > containers.before.txt
  docker inspect eduivme-production-coaching-service-1 --format '{{.Id}}' > previous-container.txt
  docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}' > previous-image.txt
  docker inspect eduivme-production-coaching-service-1 |jq -er '.[0].NetworkSettings.Networks|keys[0]' > network.txt
  docker exec eduivme-production-coaching-service-1 sha256sum /app/Coaching.Domain.dll /app/Coaching.Application.dll /app/Coaching.Infrastructure.dll /app/Coaching.API.dll |sed 's#/app/##' > live-base-dlls.sha256
  cmp expected-base-dlls.sha256 live-base-dlls.sha256
  "${compose[@]}" -f /var/lib/eduivme/releases/coaching-teacher-wizard-20261018/production.compose.json config --format json > base.compose.json
  docker inspect eduivme-production-coaching-service-1 |jq '.[0].Config.Env|map(capture("^(?<key>[^=]+)=(?<value>.*)$"))|from_entries' > coaching.environment.json
  jq --arg image "$(<previous-image.txt)" --slurpfile env coaching.environment.json '.services["coaching-service"].image=$image|.services["coaching-service"].environment=$env[0]|walk(if type=="string" then gsub("\\$";"$$") else . end)' base.compose.json > rollback.compose.json
  for database in coaching_db identity_db;do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before-drill.backup";done
  sha256sum *.before-drill.backup > initial-backups.sha256
  date -u +%FT%TZ > prepared.txt;echo 'Backups and exact live-base verification ready.'
  ;;
 build)
  test -s prepared.txt;test ! -e built.txt
  [[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Id}}') == "$(<previous-container.txt)" ]]
  docker tag "$(<previous-image.txt)" eduivme/coaching-service:scope-shares-base-20261018
  docker build --network none --pull=false --build-arg BASE_IMAGE=eduivme/coaching-service:scope-shares-base-20261018 \
   --build-arg SOURCE_REVISION=scope-shares-20261018 -f coaching-service.Dockerfile -t eduivme/coaching-service:scope-shares-20261018 . > build.log 2>&1
  docker image inspect eduivme/coaching-service:scope-shares-20261018 --format '{{.Id}}' > built-image.txt
  jq --arg image "$(<built-image.txt)" '.services["coaching-service"].image=$image' rollback.compose.json > production.compose.json
  "${compose[@]}" -f production.compose.json config --quiet
  date -u +%FT%TZ > built.txt;echo 'Built one service from the exact live base.'
  ;;
 drill)
  test -s built.txt;test ! -e drill-verified.txt
  if [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$drill'") == 0 ]];then
   connection=$(jq -er '.ConnectionStrings__DefaultConnection' coaching.environment.json)
   pattern='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)';[[ "$connection" =~ $pattern ]];owner=${BASH_REMATCH[2]}
   docker exec postgres createdb -U "$db_user" -O "$owner" "$drill"
   docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$drill" --exit-on-error --no-owner --no-privileges < coaching_db.before-drill.backup > drill-restore.log 2>&1
   rows "$drill" > drill.before.txt
  else
   test -s drill.before.txt;preserved "$drill" drill
  fi
  jq --arg d "$drill" '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection|=sub("Database=coaching_db;";"Database="+$d+";")' production.compose.json > drill.compose.json
  jq -e --arg d "$drill" '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection|contains("Database="+$d+";")' drill.compose.json >/dev/null
  "${compose[@]}" -f drill.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > drill-migrate.log 2>&1
  "${compose[@]}" -f drill.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > drill-migrate-replay.log 2>&1
  run drill > drill-result.json 2> drill-error.log
  jq -e '.passed and .newDurationVersions==269 and .topicShares==319 and .annualPreviewTopics==144' drill-result.json >/dev/null
  preserved "$drill" drill
  rows "$drill" > drill-replay.before.txt
  run drill > drill-replay-result.json 2> drill-replay-error.log
  jq -e '.passed and .newDurationVersions==0 and .metadataChanges==0' drill-replay-result.json >/dev/null
  rows "$drill" > drill-replay.after.txt;cmp drill-replay.before.txt drill-replay.after.txt
  unchanged;date -u +%FT%TZ > drill-verified.txt;cat drill-result.json
  ;;
 promote)
  test -s drill-verified.txt;test ! -e deployed.txt;unchanged
  [[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Id}}') == "$(<previous-container.txt)" ]]
  for database in coaching_db identity_db;do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before-promotion.backup";done
  sha256sum *.before-promotion.backup > live-backups.sha256
  rows coaching_db > live.before.txt;accounts > accounts.before.txt
  rollback() {
   local status=$?;trap - EXIT
   if ((status!=0))&&[[ ! -e data-applied.txt ]];then
    "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never coaching-service > rollback.log 2>&1;ready
    echo 'Previous image restored; additive schema and original data retained.' >&2
   fi
   exit "$status"
  };trap rollback EXIT
  "${compose[@]}" -f production.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > live-migrate.log 2>&1
  "${compose[@]}" -f production.compose.json up -d --no-deps --pull never coaching-service > promote.log 2>&1;ready
  run live > live-result.json 2> live-error.log
  date -u +%FT%TZ > data-applied.txt
  jq -e '.passed and .newDurationVersions==269 and .topicShares==319 and .annualPreviewTopics==144' live-result.json >/dev/null
  preserved coaching_db live;accounts > accounts.after.txt;cmp accounts.before.txt accounts.after.txt
  rows coaching_db > live-replay.before.txt;run live > live-replay-result.json 2> live-replay-error.log
  jq -e '.passed and .newDurationVersions==0 and .metadataChanges==0' live-replay-result.json >/dev/null
  rows coaching_db > live-replay.after.txt;cmp live-replay.before.txt live-replay.after.txt
  ready;unchanged;date -u +%FT%TZ > deployed.txt;trap - EXIT;cat live-result.json
  ;;
 verify)
  test -s deployed.txt;ready;unchanged;preserved coaching_db live
  accounts > accounts.latest.txt;cmp accounts.before.txt accounts.latest.txt
  [[ $(docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}') == "$(<built-image.txt)" ]]
  cat deployed.txt;cat live-result.json;cat live-replay-result.json
  ;;
 *) exit 2;;
esac
