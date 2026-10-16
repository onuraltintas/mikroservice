#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/var/lib/eduivme/releases/coaching-outcomes-20261016
cd "$release"
[[ "$PWD" == "$release" ]]
exec 9>/var/lock/eduivme-coaching-deploy.lock; flock -n 9
exec 8>/var/lock/eduivme-speed-reading-deploy.lock; flock -n 8
tag=coaching-outcomes-20261016
services=(coaching-service admin-panel staff-portal)
compose=(docker compose --project-name eduivme-production --project-directory /opt/eduivme --env-file /opt/eduivme/.env)
db_user=$(docker exec postgres printenv POSTGRES_USER)
drill=coaching_outcomes_drill_20261016
trap 'printf "OUTCOMES_FAILED stage=%s line=%s\n" "${1:-validate}" "$LINENO" >&2' ERR
verifyPayload() { sha256sum -c payload.sha256 > payload-verified.log; }
unchanged() {
  local name id image
  while read -r name id image; do
    case "$name" in eduivme-production-coaching-service-1|eduivme-production-admin-panel-1|eduivme-production-staff-portal-1) continue;; esac
    [[ $(docker inspect "$name" --format '{{.Id}}') == "$id" ]] || { echo "Unrelated service changed: $name" >&2; return 1; }
  done < baseline/containers.txt
}
baselineTargets() {
  local service
  for service in "${services[@]}"; do [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Id}}') == "$(<"previous-$service-container.txt")" ]]; done
}
waitReady() {
  local name=$1 service=$2 ip port=8080 path=/health/ready
  [[ "$service" != admin-panel ]] || { port=4000; path=/auth/login; }
  [[ "$service" != staff-portal ]] || { port=80; path=/; }
  for attempt in $(seq 1 40); do
    ip=$(docker inspect "$name" --format '{{range .NetworkSettings.Networks}}{{println .IPAddress}}{{end}}' | head -1)
    if [[ "$ip" =~ ^[0-9.]+$ ]] && curl -fsS --max-time 4 -H 'Host: eduivme.com' "http://$ip:$port$path" >/dev/null 2>&1; then return 0; fi
    sleep 2
  done
  docker inspect "$name" --format '{{.Name}} running={{.State.Running}} exit={{.State.ExitCode}} restarts={{.RestartCount}}' >&2; return 1
}
fingerprint() {
  docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U "$db_user" -d "$1" <<'SQL'
SELECT format('SELECT %L,count(*),md5(coalesce(string_agg(md5(to_jsonb(t)::text),%L ORDER BY md5(to_jsonb(t)::text)),%L)) FROM %I.%I t;',table_schema||'.'||table_name,'','',table_schema,table_name)
FROM information_schema.tables WHERE table_schema='coaching' AND table_type='BASE TABLE'
AND table_name NOT IN ('AdminAuditRecords','InboxState','OutboxMessage','OutboxState','__ef_migrations_history','__EFMigrationsHistory',
'official_curriculum_editions','official_curriculum_topics','study_catalog_outcomes','study_catalog_outcome_sources','study_catalog_topic_outcomes','outcome_duration_estimates','study_plan_task_outcomes')
ORDER BY table_name
\gexec
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
runVerifier() {
  local config=$1 scope=$2
  export VERIFY_CONNECTION VERIFY_SCOPE VERIFY_ADMIN_ID VERIFY_APPROVAL
  VERIFY_SCOPE=$scope; VERIFY_APPROVAL=user-request-20261016-lgs-live
  VERIFY_CONNECTION=$(jq -er '.ConnectionStrings__DefaultConnection' coaching-service.environment.json)
  [[ "$scope" != drill ]] || VERIFY_CONNECTION=$(jq -nr --arg connection "$VERIFY_CONNECTION" --arg database "$drill" '$connection|sub("Database=coaching_db;";"Database="+$database+";")')
  VERIFY_ADMIN_ID=$(docker exec postgres psql -X -qAt -U "$db_user" -d identity_db -c "SELECT DISTINCT u.\"Id\" FROM identity.users u JOIN identity.\"UserRoles\" ur ON ur.\"UserId\"=u.\"Id\" JOIN identity.\"Roles\" r ON r.\"Id\"=ur.\"RoleId\" WHERE u.\"Email\"='admin@eduivme.com' AND u.\"IsActive\" AND r.\"Name\"='SystemAdmin'")
  [[ "$VERIFY_ADMIN_ID" =~ ^[0-9a-f-]{36}$ ]]
  "${compose[@]}" -f "$config" run --rm --no-deps --pull never --entrypoint sh \
    -e VERIFY_CONNECTION -e VERIFY_SCOPE -e VERIFY_ADMIN_ID -e VERIFY_APPROVAL \
    -v "$release/payload/verification:/verification:ro" coaching-service \
    -c 'cp -a /verification /tmp/outcome-verification && cp /app/*.dll /tmp/outcome-verification/ && dotnet /tmp/outcome-verification/Verification.dll'
}
case "${1:-validate}" in
  prepare)
    test ! -e prepared.txt; verifyPayload
    mkdir -p baseline
    docker ps --format '{{.Names}} {{.ID}} {{.Image}}' > baseline/containers-short.txt
    docker inspect $(docker ps --format '{{.Names}}') --format '{{.Name}} {{.Id}} {{.Image}}' | sed 's#^/##' > baseline/containers.txt
    docker inspect eduivme-production-coaching-service-1 | jq -e '.[0].Image=="sha256:a03bd6a5d248cf1bf906f16f16ef01b8bf7477fc6a9198162f1a34906e886cf2"' >/dev/null
    docker inspect eduivme-production-admin-panel-1 | jq -e '.[0].Image=="sha256:e9feb4fc096236c0bb8b2b024cd1c78b85d0bc3cea28e05b2b17d6618fc9a491"' >/dev/null
    docker inspect eduivme-production-staff-portal-1 | jq -e '.[0].Image=="sha256:3c2266ae0af34601533d10df2b1bd45d808cb699de2df674b8b6b52dcb306e0c"' >/dev/null
    "${compose[@]}" -f /var/lib/eduivme/releases/coaching-learning-20261016/production.compose.json config --format json > base.compose.json
    docker inspect $(cut -d' ' -f1 baseline/containers.txt) | jq '{services:(map(select(.Config.Labels["com.docker.compose.service"] != null)|{key:.Config.Labels["com.docker.compose.service"],value:{image:.Image,pull_policy:"never"}})|from_entries)}' > images.json
    jq --slurpfile images images.json '. * $images[0]' base.compose.json > frozen.compose.json
    for service in "${services[@]}"; do
      docker inspect "eduivme-production-$service-1" --format '{{.Image}}' > "previous-$service-image.txt"
      docker inspect "eduivme-production-$service-1" --format '{{.Id}}' > "previous-$service-container.txt"
      docker inspect "eduivme-production-$service-1" | jq '.[0].Config.Env|map(capture("^(?<key>[^=]+)=(?<value>.*)$"))|from_entries' > "$service.environment.json"
      jq --arg service "$service" --slurpfile env "$service.environment.json" '.services[$service].environment=$env[0]' frozen.compose.json > next.json
      mv next.json frozen.compose.json
    done
    jq 'walk(if type=="string" then gsub("\\$";"$$") else . end)' frozen.compose.json > rollback.compose.json
    jq --arg tag "$tag" '.services["coaching-service"].image=("eduivme/coaching-service:"+$tag)|.services["admin-panel"].image=("eduivme/admin-panel:"+$tag)|.services["staff-portal"].image=("eduivme/staff-portal:"+$tag)' rollback.compose.json > production.compose.json
    "${compose[@]}" -f production.compose.json config --quiet
    accounts > baseline/accounts.txt; fingerprint coaching_db > baseline/coaching.txt
    unchanged; date -u +%FT%TZ > prepared.txt; echo 'Prepared: three target services; Identity and other services unchanged.'
    ;;
  build)
    test -s prepared.txt; test ! -e built.txt; verifyPayload; baselineTargets
    for service in "${services[@]}"; do
      docker image tag "$(<"previous-$service-image.txt")" "eduivme/$service:$tag-base"
      docker build --network none --build-arg "BASE_IMAGE=eduivme/$service:$tag-base" --build-arg "SOURCE_REVISION=$(<source-revision.txt)" -f "$service.Dockerfile" -t "eduivme/$service:$tag" . > "$service-build.log" 2>&1
      docker image inspect "eduivme/$service:$tag" --format '{{.Id}}' > "built-$service-image.txt"
    done
    unchanged; date -u +%FT%TZ > built.txt; echo 'Built: three images from exact live bases.'
    ;;
  drill)
    test -s built.txt; test ! -e drill-verified.txt; baselineTargets; verifyPayload
    [[ $(docker exec postgres psql -U "$db_user" -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='$drill'") == 0 ]]
    docker exec postgres pg_dump -U "$db_user" -d coaching_db -Fc > coaching.before-drill.backup
    connection=$(jq -er '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection' frozen.compose.json)
    username_regex='(^|;)Username=([A-Za-z_][A-Za-z0-9_]*)(;|$)'; [[ "$connection" =~ $username_regex ]]; owner=${BASH_REMATCH[2]}
    docker exec postgres createdb -U "$db_user" -O "$owner" "$drill"
    docker exec -i postgres pg_restore -U "$db_user" --role "$owner" -d "$drill" --exit-on-error --no-owner --no-privileges < coaching.before-drill.backup > drill-restore.log 2>&1
    fingerprint "$drill" > drill.before.txt
    jq --arg database "$drill" '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection |= sub("Database=coaching_db;";"Database="+$database+";")' production.compose.json > drill.compose.json
    jq -e --arg database "$drill" '.services["coaching-service"].environment.ConnectionStrings__DefaultConnection|contains("Database="+$database+";")' drill.compose.json >/dev/null
    "${compose[@]}" -f drill.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > drill-migration.log 2>&1
    "${compose[@]}" -f drill.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > drill-migration-replay.log 2>&1
    runVerifier drill.compose.json drill > drill-result.json 2> drill-error.log
    runVerifier drill.compose.json drill > drill-replay-result.json 2> drill-replay-error.log
    jq -e '.passed and .outcomes==52 and .approvedMappings==58 and .publishedDurations==52 and .drafts==0 and .preservedPlans==3 and .preservedCompleted==4 and .preservedActualMinutes==86' drill-result.json >/dev/null
    jq -e '.passed and .draftBootstrapRemoved==0' drill-replay-result.json >/dev/null
    fingerprint "$drill" > drill.after.txt; cmp drill.before.txt drill.after.txt
    docker exec postgres dropdb -U "$db_user" "$drill"
    sha256sum coaching.before-drill.backup > drill-backup.sha256
    unchanged; date -u +%FT%TZ > drill-verified.txt; cat drill-result.json
    ;;
  promote)
    test -s drill-verified.txt; test ! -e deployed.txt; verifyPayload; baselineTargets; unchanged
    for service in "${services[@]}"; do [[ $(docker image inspect "eduivme/$service:$tag" --format '{{.Id}}') == "$(<"built-$service-image.txt")" ]]; done
    rollback() {
      local status=$?; trap - EXIT
      if (( status != 0 )); then
        for service in "${services[@]}"; do "${compose[@]}" -f rollback.compose.json up -d --no-deps --pull never "$service" > "$service-rollback.log" 2>&1; waitReady "eduivme-production-$service-1" "$service" || exit 70; done
        echo 'Previous application images restored; additive schema, catalog records and backups retained.' >&2
      fi
      exit "$status"
    }; trap rollback EXIT
    docker stop --time 30 eduivme-production-admin-panel-1 eduivme-production-staff-portal-1 eduivme-production-coaching-service-1 > writer-stop.log
    for database in identity_db coaching_db; do docker exec postgres pg_dump -U "$db_user" -d "$database" -Fc > "$database.before-promotion.backup"; done
    sha256sum *.before-promotion.backup > live-backups.sha256
    accounts > accounts.live.before.txt; fingerprint coaching_db > coaching.live.before.txt
    "${compose[@]}" -f production.compose.json run --rm --no-deps --pull never coaching-service --migrate-only > live-migration.log 2>&1
    runVerifier production.compose.json live > live-result.json 2> live-error.log
    jq -e '.passed and .outcomes==52 and .approvedMappings==58 and .publishedDurations==52 and .drafts==0' live-result.json >/dev/null
    accounts > accounts.live.after-import.txt; fingerprint coaching_db > coaching.live.after-import.txt
    cmp accounts.live.before.txt accounts.live.after-import.txt; cmp coaching.live.before.txt coaching.live.after-import.txt
    for service in "${services[@]}"; do
      "${compose[@]}" -f production.compose.json up -d --no-deps --pull never "$service" > "$service-promotion.log" 2>&1
      waitReady "eduivme-production-$service-1" "$service"
    done
    accounts > accounts.live.after.txt; fingerprint coaching_db > coaching.live.after.txt
    cmp accounts.live.before.txt accounts.live.after.txt; cmp coaching.live.before.txt coaching.live.after.txt
    unchanged; date -u +%FT%TZ > deployed.txt; trap - EXIT; cat live-result.json
    ;;
  validate)
    test -s deployed.txt; verifyPayload
    for service in "${services[@]}"; do waitReady "eduivme-production-$service-1" "$service"; [[ $(docker inspect "eduivme-production-$service-1" --format '{{.Image}}') == "$(<"built-$service-image.txt")" ]]; docker inspect "eduivme-production-$service-1" --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}'; done
    unchanged; cat deployed.txt; cat live-result.json
    ;;
  *) exit 2;;
esac
