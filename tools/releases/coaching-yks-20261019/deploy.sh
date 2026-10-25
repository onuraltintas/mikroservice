set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-yks-20261019-r3
drill=coaching_yks_drill_20261019_r3
tag=coaching-yks-20261019-r3
cd "$release"
exec 9>/var/lock/eduivme-coaching-deploy.lock;flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock;flock -n 8
services=(coaching-service admin-panel staff-portal)
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
payload(){ sha256sum -c payload.sha256 > payload-verified.log; }
baseline_targets(){
 for service in "${services[@]}";do [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]];done
}
unchanged(){
 while read -r name id;do
  case "$name" in eduivme-production-coaching-service-1|eduivme-production-admin-panel-1|eduivme-production-staff-portal-1)continue;;esac
  [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]]
 done < containers.before.txt
}
ready(){
 local service=$1 ip port=8080 path=/health/ready
 if [[ "$service" == admin-panel ]];then port=4000;path=/;fi
 if [[ "$service" == staff-portal ]];then port=80;path=/staff/;fi
 for attempt in $(seq 1 30);do
  ip=$(docker inspect "eduivme-production-$service-1" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}'|head -1)
  if [[ "$ip" =~ ^[0-9.]+$ ]]&&curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null 2>&1;then return 0;fi
  sleep 2
 done;return 1
}
protected(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL' | LC_ALL=C sort
BEGIN READ ONLY;
SELECT format('SELECT %L||md5(to_jsonb(t)::text) FROM coaching.%I t;',table_name||'|',table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT LIKE 'study_catalog_%' AND table_name NOT LIKE 'official_curriculum_%'
AND table_name NOT IN ('outcome_duration_estimates','AdminAuditRecords','InboxState','OutboxState','OutboxMessage','__ef_migrations_history','__EFMigrationsHistory') ORDER BY table_name \gexec
SELECT 'lgs.lesson|'||md5(to_jsonb(l)::text) FROM coaching.study_catalog_lessons l WHERE "ExamCode"='LGS';
SELECT 'lgs.unit|'||md5(to_jsonb(u)::text) FROM coaching.study_catalog_units u JOIN coaching.study_catalog_lessons l ON l."Id"=u."LessonId" WHERE l."ExamCode"='LGS';
SELECT 'lgs.topic|'||md5(to_jsonb(t)::text) FROM coaching.study_catalog_topics t JOIN coaching.study_catalog_lessons l ON l."Id"=t."LessonId" WHERE l."ExamCode"='LGS';
SELECT 'lgs.edition|'||md5(to_jsonb(e)::text) FROM coaching.official_curriculum_editions e WHERE "ExamCode"='LGS';
SELECT 'lgs.official-topic|'||md5(to_jsonb(t)::text) FROM coaching.official_curriculum_topics t JOIN coaching.official_curriculum_editions e ON e."Id"=t."EditionId" WHERE e."ExamCode"='LGS';
SELECT 'lgs.outcome|'||md5((to_jsonb(o)-'{SourceKind,SourceUrl}'::text[])::text) FROM coaching.study_catalog_outcomes o JOIN coaching.official_curriculum_editions e ON e."Id"=o."EditionId" WHERE e."ExamCode"='LGS';
SELECT 'lgs.estimate|'||md5(to_jsonb(d)::text) FROM coaching.outcome_duration_estimates d JOIN coaching.study_catalog_outcomes o ON o."Id"=d."OutcomeId" JOIN coaching.official_curriculum_editions e ON e."Id"=o."EditionId" WHERE e."ExamCode"='LGS';
SELECT 'lgs.mapping|'||md5(to_jsonb(m)::text) FROM coaching.study_catalog_topic_outcomes m JOIN coaching.study_catalog_outcomes o ON o."Id"=m."OutcomeId" JOIN coaching.official_curriculum_editions e ON e."Id"=o."EditionId" WHERE e."ExamCode"='LGS';
SELECT 'lgs.share|'||md5(to_jsonb(s)::text) FROM coaching.study_catalog_topic_outcome_shares s JOIN coaching.study_catalog_outcomes o ON o."Id"=s."OutcomeId" JOIN coaching.official_curriculum_editions e ON e."Id"=o."EditionId" WHERE e."ExamCode"='LGS';
COMMIT;
SQL
}
accounts(){
 docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d identity_db <<'SQL'
SELECT 'users',count(*),md5(coalesce(string_agg(md5((to_jsonb(u)-'{LastLoginAt,LastAcceptedMfaTimeStep,MfaFailedAttempts,MfaLockedUntil,UpdatedAt,Version}'::text[])::text),'' ORDER BY "Id"),'')) FROM identity.users u;
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE' AND table_name IN ('UserRoles','UserProductAccesses','Roles','RolePermissions') ORDER BY table_name \gexec
SQL
}
notifications(){ docker exec postgres psql -X -qAt -U "$db_user" -d notification_db -c 'SELECT count(*),md5(coalesce(string_agg(md5(to_jsonb(n)::text),'"'"''"'"' ORDER BY "Id"),'"'"''"'"')) FROM public."Notifications" n'; }
verify_data(){
 export VERIFY_CONNECTION VERIFY_SCOPE VERIFY_ADMIN_ID VERIFY_OPERATION
 VERIFY_SCOPE=$1;VERIFY_OPERATION=$2;VERIFY_CONNECTION=$(jq -er '.ConnectionStrings__DefaultConnection' coaching-service.environment.json)
 if [[ "$1" == drill ]];then VERIFY_CONNECTION=$(jq -nr --arg c "$VERIFY_CONNECTION" --arg d "$drill" '$c|sub("Database=coaching_db;";"Database="+$d+";")');[[ "$VERIFY_CONNECTION" == *"Database=$drill;"* ]];fi
 VERIFY_ADMIN_ID=$(docker exec postgres psql -X -qAt -U "$db_user" -d identity_db -c "SELECT DISTINCT u.\"Id\" FROM identity.users u JOIN identity.\"UserRoles\" ur ON ur.\"UserId\"=u.\"Id\" JOIN identity.\"Roles\" r ON r.\"Id\"=ur.\"RoleId\" WHERE u.\"Email\"='admin@eduivme.com' AND u.\"IsActive\" AND r.\"Name\"='SystemAdmin'")
 [[ "$VERIFY_ADMIN_ID" =~ ^[0-9a-f-]{36}$ ]]
 docker run --rm --network "$(<network.txt)" --entrypoint sh -e VERIFY_CONNECTION -e VERIFY_SCOPE -e VERIFY_ADMIN_ID -e VERIFY_OPERATION -v "$release/payload/verification:/verification:ro" "$(<built-coaching-service-image.txt)" -c 'mkdir /tmp/verify && cp /verification/Verification.* /tmp/verify/ && cp /verification/yks-2026-basis-v1.json /tmp/verify/ && cp /app/*.dll /tmp/verify/ && dotnet /tmp/verify/Verification.dll'
}
case "${1:-}" in
 prepare)
  test ! -e prepared.txt;payload
  docker inspect eduivme-production-coaching-service-1 --format '{{.Image}}'|grep -Fx 'sha256:3552ca5123a6e267d368598579e16426ee20f9053ad99398dd6a3f2bfc1ae450' >/dev/null
  docker inspect eduivme-production-admin-panel-1 --format '{{.Image}}'|grep -Fx 'sha256:25f7028593d1ddb7158d96e41a9fc593f8e08d21938a5db886a404459496b833' >/dev/null
  docker inspect eduivme-production-staff-portal-1 --format '{{.Image}}'|grep -Fx 'sha256:ebc286d719eae166cd72bb34bdc6df7be0dbc9ac6dcf46af29e2f67d486403a5' >/dev/null
  docker exec eduivme-production-coaching-service-1 sha256sum /app/Coaching.Domain.dll /app/Coaching.Application.dll /app/Coaching.Infrastructure.dll /app/Coaching.API.dll|sed 's#/app/##' > live-base-dlls.sha256;cmp expected-base-dlls.sha256 live-base-dlls.sha256
  docker inspect $(docker ps -q) --format '{{.Name}} {{.Id}}'|sed 's#^/##' > containers.before.txt
  docker inspect eduivme-production-coaching-service-1 --format '{{range $name,$config:=.NetworkSettings.Networks}}{{println $name}}{{end}}'|head -1 > network.txt
  "${compose[@]}" -f /var/lib/eduivme/releases/coaching-clean-start-20261018/production.compose.json config --format json > base.compose.json
  docker inspect $(cut -d' ' -f1 containers.before.txt)|jq '{services:(map(select(.Config.Labels["com.docker.compose.project"]=="eduivme-production" and .Config.Labels["com.docker.compose.service"]!=null)|{key:.Config.Labels["com.docker.compose.service"],value:{image:.Image,pull_policy:"never"}})|from_entries)}' > images.json
  jq --slurpfile images images.json '. * $images[0]' base.compose.json > frozen.compose.json
  for service in "${services[@]}";do
   docker inspect "eduivme-production-$service-1" --format '{{.Id}}' > "previous-$service-container.txt"
   docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
   docker inspect "eduivme-production-$service-1"|jq '.[0].Config.Env|map(capture("^(?<key>[^=]+)=(?<value>.*)$"))|from_entries' > "$service.environment.json"
   jq --arg service "$service" --slurpfile env "$service.environment.json" '.services[$service].environment=$env[0]' frozen.compose.json > next.compose.json;mv next.compose.json frozen.compose.json
  done
  jq 'walk(if type=="string" then gsub("\\$";"$$") else . end)' frozen.compose.json > rollback.compose.json
  jq --arg tag "$tag" '.services["coaching-service"].image=("eduivme/coaching-service:"+$tag)|.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)|.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)' rollback.compose.json > production.compose.json
  "${compose[@]}" -f production.compose.json config --quiet
  for database in coaching_db identity_db notification_db;do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before-drill.backup";done
  sha256sum *.before-drill.backup > backups.sha256
  baseline_targets;unchanged;date -u +%FT%TZ > prepared.txt;echo 'Exact live baselines and three database backups prepared.'
  ;;
 build)
  test -s prepared.txt;test ! -e built.txt;payload;baseline_targets
  for service in "${services[@]}";do
   docker tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
   docker build --network none --pull=false --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg SOURCE_REVISION=yks-20261019-r3 -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
   docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
  done
  unchanged;date -u +%FT%TZ > built.txt;echo 'Coaching and both portal images built from exact live bases.'
  ;;
 drill)
  test -s built.txt;test ! -e drill-verified.txt;baseline_targets
  connection=$(jq -er '.ConnectionStrings__DefaultConnection' coaching-service.environment.json);pattern='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)';[[ "$connection" =~ $pattern ]];owner=${BASH_REMATCH[2]}
  if [[ $(docker exec postgres psql -X -qAt -U "$db_user" -d postgres -c "SELECT count(*) FROM pg_database WHERE datname='$drill'") == 0 ]];then
   docker exec postgres createdb -U "$db_user" -O "$owner" "$drill"
   docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$drill" --exit-on-error --no-owner --no-privileges < coaching_db.before-drill.backup > drill-restore.log 2>&1
   protected "$drill" > drill-protected.before.txt
  fi
  test -s drill-protected.before.txt
  jq --arg d "$drill" '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection|=sub("Database=coaching_db;";"Database="+$d+";")' production.compose.json > drill.compose.json
  jq -e --arg d "$drill" '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection|contains("Database="+$d+";")' drill.compose.json >/dev/null
  "${compose[@]}" -f drill.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > drill-migrate.log 2>&1
  verify_data drill import > drill-result.json 2> drill-error.log;jq -e '.passed' drill-result.json >/dev/null
  protected "$drill" > drill-protected.after.txt;cmp drill-protected.before.txt drill-protected.after.txt
  unchanged;date -u +%FT%TZ > drill-verified.txt;cat drill-result.json
  ;;
 promote)
  test -s drill-verified.txt;test ! -e deployed.txt;payload;baseline_targets;unchanged
  for database in coaching_db identity_db notification_db;do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before-promotion.backup";done
  sha256sum *.before-promotion.backup > final-backups.sha256
  protected coaching_db > protected.before.txt;accounts > accounts.before.txt;notifications > notifications.before.txt
  recover(){
   local status=$?;trap - EXIT
   if ((status!=0));then
    if [[ -e schema-applied.txt ]];then target=production.compose.json;else target=rollback.compose.json;fi
    "${compose[@]}" -f "$target" up -d --no-deps --pull never coaching-service > recovery.log 2>&1;ready coaching-service||true
    for service in admin-panel staff-portal;do "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "$service" >> recovery.log 2>&1;done
   fi;exit "$status"
  };trap recover EXIT
  "${compose[@]}" -f rollback.compose.json stop coaching-service > stop.log 2>&1
  "${compose[@]}" -f production.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > live-migrate.log 2>&1;date -u +%FT%TZ > schema-applied.txt
  verify_data live import > live-result.json 2> live-error.log;jq -e '.passed' live-result.json >/dev/null
  for service in "${services[@]}";do "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promote.log" 2>&1;ready "$service";done
  protected coaching_db > protected.after.txt;cmp protected.before.txt protected.after.txt
  accounts > accounts.after.txt;cmp accounts.before.txt accounts.after.txt
  notifications > notifications.after.txt;cmp notifications.before.txt notifications.after.txt
  unchanged;date -u +%FT%TZ > deployed.txt;trap - EXIT;cat live-result.json
  ;;
 verify)
  test -s deployed.txt;for service in "${services[@]}";do ready "$service";[[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]];done
  unchanged;verify_data live verify
  ;;
 *)echo 'Expected prepare, build, drill, promote, or verify' >&2;exit 2;;
esac
